using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

public struct Circle
{
    public float2 Center;
    public float Radius;
}

public struct Group
{
    public int[] PointIndices;
}

public enum CoverMode
{
    None,
    SingleCircleDirect,
    SingleCircleGroup,
    DoubleCircleDirect,
    DoubleCircleGroup,
    DoubleCircleSlide
}

public struct CoverResult
{
    public CoverMode Mode;
    public Circle Circle1;
    public Circle Circle2;
    public float2 Circle1End;
    public float2 Circle2End;
}

public struct NotePoint
{
    public float2 Position;
}

public delegate List<Group> GroupBuilder(IReadOnlyList<NotePoint> notes);

public static class CoverageSolver
{
    internal struct BurstGroup
    {
        public ulong PointMask;
        public int RequiredCount;
    }

    internal struct SwipeCandidate
    {
        public Circle Start;
        public float2 End;
        public float LengthSq;
    }

    public static CoverResult Solve(
        IReadOnlyList<float2> points,
        IReadOnlyList<float> pointRadii,
        IReadOnlyList<Group> groups,
        float maxRadius = InputManager.DJAUTO_HAND_MAX_RADIUS,
        bool allowSlide = false)
    {
        int n = points.Count;
        if (n == 0) return new CoverResult { Mode = CoverMode.None };
        if (n > 64) throw new ArgumentException("Points count must be <= 64 to fit in ulong mask.");
        if (pointRadii == null || pointRadii.Count != n)
            throw new ArgumentException("Point radii count must match points count.", nameof(pointRadii));

        var nativePoints = new NativeArray<float2>(n, Allocator.TempJob);
        var nativePointRadii = new NativeArray<float>(n, Allocator.TempJob);
        var nativeGroups = new NativeArray<BurstGroup>(groups.Count, Allocator.TempJob);
        var resultArr = new NativeArray<CoverResult>(1, Allocator.TempJob);

        try
        {
            for (int i = 0; i < n; i++)
            {
                nativePoints[i] = points[i];
                nativePointRadii[i] = pointRadii[i];
            }

            for (int i = 0; i < groups.Count; i++)
            {
                ulong mask = 0;
                if (groups[i].PointIndices != null)
                {
                    foreach (int idx in groups[i].PointIndices)
                    {
                        if (idx >= 0 && idx < n)
                            mask |= (1ul << idx);
                    }
                }
                int total = groups[i].PointIndices?.Length ?? 0;
                nativeGroups[i] = new BurstGroup
                {
                    PointMask = mask,
                    RequiredCount = total / 2 + 1
                };
            }

            var job = new SolveJob
            {
                Points = nativePoints,
                PointRadii = nativePointRadii,
                Groups = nativeGroups,
                MaxRadius = maxRadius,
                AllowSlide = allowSlide,
                Result = resultArr
            };
            job.Run();

            return resultArr[0];
        }
        finally
        {
            nativePoints.Dispose();
            nativePointRadii.Dispose();
            nativeGroups.Dispose();
            resultArr.Dispose();
        }
    }

    [BurstCompile]
    private struct SolveJob : IJob
    {
        [ReadOnly] public NativeArray<float2> Points;
        [ReadOnly] public NativeArray<float> PointRadii;
        [ReadOnly] public NativeArray<BurstGroup> Groups;
        public float MaxRadius;
        public bool AllowSlide;

        public NativeArray<CoverResult> Result;

        public void Execute()
        {
            int n = Points.Length;
            ulong targetMask = n == 64 ? ulong.MaxValue : (1ul << n) - 1;

            var candidates = new NativeList<Circle>(57, Allocator.Temp);

            // A1~A8
            for (int i = 1; i <= 8; i++) candidates.Add(new Circle { Center = MajPos.RingPos(4.1f, i, false), Radius = MaxRadius });
            // B1~B8
            for (int i = 1; i <= 8; i++) candidates.Add(new Circle { Center = MajPos.RingPos(2.3f, i, false), Radius = MaxRadius });
            // C
            candidates.Add(new Circle { Center = float2.zero, Radius = MaxRadius });
            // A-B Intersections
            for (int i = 1; i <= 8; i++) candidates.Add(new Circle { Center = MajPos.RingPos(3.2f, i, false), Radius = MaxRadius });
            // B-B Intersections
            for (int i = 1; i <= 8; i++) candidates.Add(new Circle { Center = MajPos.RingPos(2.3f, i, true), Radius = MaxRadius });
            // B-C Intersections
            for (int i = 1; i <= 8; i++) candidates.Add(new Circle { Center = MajPos.RingPos(1.15f, i, false), Radius = MaxRadius });
            // D1~D8
            for (int i = 1; i <= 8; i++) candidates.Add(new Circle { Center = MajPos.RingPos(4.1f, i, true), Radius = MaxRadius });
            // E1~E8
            for (int i = 1; i <= 8; i++) candidates.Add(new Circle { Center = MajPos.RingPos(3.0f, i, true), Radius = MaxRadius });

            int maxCandidates = candidates.Length * (Points.Length + 1);

            var maskToCircle = new NativeHashMap<ulong, Circle>(maxCandidates, Allocator.Temp);
            var uniqueMasks = new NativeList<ulong>(maxCandidates, Allocator.Temp);

            // 对每个圆心枚举从普通指尖到最大手掌之间所有会改变覆盖结果的半径。
            // 这样单个 TouchHold 不再无条件占用 1.8 的大圆。
            for (int i = 0; i < candidates.Length; i++)
            {
                AddRadiusVariants(candidates[i].Center, maskToCircle, uniqueMasks);
            }

            // 按操作复杂度依次尝试：单圆、双圆、双圆滑动。
            // Slide 依赖整个 Perfect 窗口内的累计覆盖，所以只作为最后的兜底方案。
            CoverResult result;
            bool found = TrySolveSingleCircle(maskToCircle, uniqueMasks, targetMask, out result) ||
                         TrySolveDoubleCircle(maskToCircle, uniqueMasks, targetMask, out result);
            if (!found && AllowSlide)
                found = TrySolveDoubleCircleSlide(candidates, targetMask, out result);

            Result[0] = found ? result : new CoverResult { Mode = CoverMode.None };
            candidates.Dispose();
            maskToCircle.Dispose();
            uniqueMasks.Dispose();
        }

        private bool TrySolveSingleCircle(
            NativeHashMap<ulong, Circle> maskToCircle,
            NativeList<ulong> uniqueMasks,
            ulong targetMask,
            out CoverResult result)
        {
            // Direct 要求实际覆盖全部点，比 Group 的多数通过更严格，因此优先返回 Direct。
            if (maskToCircle.TryGetValue(targetMask, out Circle directCircle))
            {
                result = new CoverResult
                {
                    Mode = CoverMode.SingleCircleDirect,
                    Circle1 = directCircle
                };
                return true;
            }

            bool found = false;
            Circle bestCircle = default;
            for (int i = 0; i < uniqueMasks.Length; i++)
            {
                ulong mask = uniqueMasks[i];
                if (ExpandGroups(mask, Groups) != targetMask)
                    continue;

                Circle candidate = maskToCircle[mask];
                if (!found || IsBetterCircle(candidate, bestCircle))
                {
                    found = true;
                    bestCircle = candidate;
                }
            }

            result = found
                ? new CoverResult { Mode = CoverMode.SingleCircleGroup, Circle1 = bestCircle }
                : default;
            return found;
        }

        private bool TrySolveDoubleCircle(
            NativeHashMap<ulong, Circle> maskToCircle,
            NativeList<ulong> uniqueMasks,
            ulong targetMask,
            out CoverResult result)
        {
            bool foundDirect = false;
            bool foundGroup = false;
            CoverResult directResult = default;
            CoverResult groupResult = default;

            for (int i = 0; i < uniqueMasks.Length; i++)
            {
                ulong mask1 = uniqueMasks[i];
                Circle circle1 = maskToCircle[mask1];

                for (int j = i; j < uniqueMasks.Length; j++)
                {
                    ulong mask2 = uniqueMasks[j];
                    ulong combined = mask1 | mask2;

                    if (combined == targetMask)
                    {
                        var candidate = new CoverResult
                        {
                            Mode = CoverMode.DoubleCircleDirect,
                            Circle1 = circle1,
                            Circle2 = maskToCircle[mask2]
                        };
                        if (!foundDirect || IsBetterPair(candidate, directResult))
                        {
                            directResult = candidate;
                            foundDirect = true;
                        }
                        continue;
                    }

                    // 同一个合并掩码也可能来自不同圆心，必须全部比较，
                    // 才能选到实际压住传感区最少的圆对。
                    if (ExpandGroups(combined, Groups) != targetMask)
                        continue;

                    var groupCandidate = new CoverResult
                    {
                        Mode = CoverMode.DoubleCircleGroup,
                        Circle1 = circle1,
                        Circle2 = maskToCircle[mask2]
                    };
                    if (!foundGroup || IsBetterPair(groupCandidate, groupResult))
                    {
                        groupResult = groupCandidate;
                        foundGroup = true;
                    }
                }
            }

            result = foundDirect ? directResult : groupResult;
            return foundDirect || foundGroup;
        }

        private bool TrySolveDoubleCircleSlide(
            NativeList<Circle> candidates,
            ulong targetMask,
            out CoverResult result)
        {
            // 在 -2 帧时落下两只手，并在 0.2 秒内完成滑动。
            // 包含起点和终点，共采样 0~12 帧的 13 个位置，与实际播放保持一致。
            const int sampleCount = 13;

            int maxSwipeCount = candidates.Length * candidates.Length;
            var swipeByMask = new NativeHashMap<ulong, SwipeCandidate>(maxSwipeCount, Allocator.Temp);
            var swipeMasks = new NativeList<ulong>(maxSwipeCount, Allocator.Temp);

            for (int startIndex = 0; startIndex < candidates.Length; startIndex++)
            {
                Circle start = candidates[startIndex];
                for (int endIndex = 0; endIndex < candidates.Length; endIndex++)
                {
                    float2 end = candidates[endIndex].Center;
                    ulong mask = 0;
                    for (int sample = 0; sample < sampleCount; sample++)
                    {
                        float t = sample / (float)(sampleCount - 1);
                        mask |= GetCoveredMask(math.lerp(start.Center, end, t), MaxRadius);
                    }

                    if (mask == 0) continue;

                    var candidate = new SwipeCandidate
                    {
                        Start = start,
                        End = end,
                        LengthSq = math.distancesq(start.Center, end)
                    };

                    if (swipeByMask.TryGetValue(mask, out var existing))
                    {
                        if (candidate.LengthSq < existing.LengthSq)
                            swipeByMask[mask] = candidate;
                    }
                    else
                    {
                        swipeByMask.Add(mask, candidate);
                        swipeMasks.Add(mask);
                    }
                }
            }

            bool found = false;
            float bestScore = float.MaxValue;
            result = default;

            for (int i = 0; i < swipeMasks.Length; i++)
            {
                ulong mask1 = swipeMasks[i];
                SwipeCandidate swipe1 = swipeByMask[mask1];

                for (int j = i; j < swipeMasks.Length; j++)
                {
                    ulong combined = mask1 | swipeMasks[j];
                    if (combined != targetMask && ExpandGroups(combined, Groups) != targetMask)
                        continue;

                    SwipeCandidate swipe2 = swipeByMask[swipeMasks[j]];
                    // 先压低两根手指中较长的滑动距离，再用总距离打破平局，
                    // 避免求解器选出一根手指几乎不动、另一根手指横跨全屏的方案。
                    float score = math.max(swipe1.LengthSq, swipe2.LengthSq) +
                                  0.1f * (swipe1.LengthSq + swipe2.LengthSq);
                    if (score >= bestScore) continue;

                    bestScore = score;
                    found = true;
                    result = new CoverResult
                    {
                        Mode = CoverMode.DoubleCircleSlide,
                        Circle1 = swipe1.Start,
                        Circle2 = swipe2.Start,
                        Circle1End = swipe1.End,
                        Circle2End = swipe2.End
                    };
                }
            }

            swipeByMask.Dispose();
            swipeMasks.Dispose();
            return found;
        }

        private ulong ExpandGroups(ulong coveredMask, NativeArray<BurstGroup> groups)
        {
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int i = 0; i < groups.Length; i++)
                {
                    BurstGroup g = groups[i];
                    if (g.PointMask != 0 && (coveredMask & g.PointMask) != g.PointMask)
                    {
                        ulong intersect = coveredMask & g.PointMask;
                        if (math.countbits(intersect) >= g.RequiredCount)
                        {
                            coveredMask |= g.PointMask;
                            changed = true;
                        }
                    }
                }
            }
            return coveredMask;
        }

        private void AddRadiusVariants(
            float2 center,
            NativeHashMap<ulong, Circle> maskToCircle,
            NativeList<ulong> uniqueMasks)
        {
            float radius = math.min(InputManager.DJAUTO_TOUCH_COVER_MIN_RADIUS, MaxRadius);
            ulong previousMask = ulong.MaxValue;

            for (int step = 0; step <= Points.Length; step++)
            {
                ulong mask = GetCoveredMask(center, radius);
                if (mask != previousMask)
                {
                    var candidate = new Circle { Center = center, Radius = radius };
                    if (maskToCircle.TryGetValue(mask, out Circle existing))
                    {
                        if (IsBetterCircle(candidate, existing))
                            maskToCircle[mask] = candidate;
                    }
                    else
                    {
                        maskToCircle.Add(mask, candidate);
                        uniqueMasks.Add(mask);
                    }
                    previousMask = mask;
                }

                float nextRadius = float.MaxValue;
                for (int pointIndex = 0; pointIndex < Points.Length; pointIndex++)
                {
                    if ((mask & (1ul << pointIndex)) != 0) continue;

                    float requiredRadius = math.max(0f,
                        math.distance(center, Points[pointIndex]) - PointRadii[pointIndex]);
                    if (requiredRadius <= MaxRadius + 1e-4f)
                        nextRadius = math.min(nextRadius, requiredRadius);
                }

                if (nextRadius == float.MaxValue) break;
                radius = math.min(MaxRadius, math.max(radius + 1e-4f, nextRadius));
            }
        }

        private bool IsBetterCircle(Circle candidate, Circle existing)
        {
            int candidateCount = math.countbits(GetAllSensorMask(candidate));
            int existingCount = math.countbits(GetAllSensorMask(existing));
            if (candidateCount != existingCount)
                return candidateCount < existingCount;
            return candidate.Radius < existing.Radius;
        }

        private bool IsBetterPair(CoverResult candidate, CoverResult existing)
        {
            ulong candidateMask = GetAllSensorMask(candidate.Circle1) | GetAllSensorMask(candidate.Circle2);
            ulong existingMask = GetAllSensorMask(existing.Circle1) | GetAllSensorMask(existing.Circle2);
            int candidateCount = math.countbits(candidateMask);
            int existingCount = math.countbits(existingMask);
            if (candidateCount != existingCount)
                return candidateCount < existingCount;

            float candidateMaxRadius = math.max(candidate.Circle1.Radius, candidate.Circle2.Radius);
            float existingMaxRadius = math.max(existing.Circle1.Radius, existing.Circle2.Radius);
            if (candidateMaxRadius != existingMaxRadius)
                return candidateMaxRadius < existingMaxRadius;

            return candidate.Circle1.Radius + candidate.Circle2.Radius <
                   existing.Circle1.Radius + existing.Circle2.Radius;
        }

        private ulong GetAllSensorMask(Circle circle)
        {
            ulong mask = 0;
            for (int i = 0; i < MajCtx.SENSOR_COUNT; i++)
            {
                float combinedRadius = circle.Radius + MajPos.GetSensorRadius((SensorType)i);
                if (math.distancesq(circle.Center, MajPos.GetSensorWorldPos((SensorType)i)) <=
                    combinedRadius * combinedRadius + 1e-4f)
                {
                    mask |= 1ul << i;
                }
            }
            return mask;
        }

        private ulong GetCoveredMask(float2 center, float radius)
        {
            ulong mask = 0;
            for (int i = 0; i < Points.Length; i++)
            {
                float combinedRadius = radius + PointRadii[i];
                if (math.distancesq(center, Points[i]) <=
                    combinedRadius * combinedRadius + 1e-4f)
                {
                    mask |= (1ul << i);
                }
            }
            return mask;
        }
    }
}
