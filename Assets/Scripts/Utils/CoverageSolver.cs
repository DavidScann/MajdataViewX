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
    DoubleCircleGroup
}

public struct CoverResult
{
    public CoverMode Mode;
    public Circle Circle1;
    public Circle Circle2;
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

    public static CoverResult Solve(
        IReadOnlyList<float2> points,
        IReadOnlyList<Group> groups,
        float maxRadius = 1.8f)
    {
        int n = points.Count;
        if (n == 0) return new CoverResult { Mode = CoverMode.None };
        if (n > 64) throw new ArgumentException("Points count must be <= 64 to fit in ulong mask.");

        var nativePoints = new NativeArray<float2>(n, Allocator.TempJob);
        var nativeGroups = new NativeArray<BurstGroup>(groups.Count, Allocator.TempJob);
        var resultArr = new NativeArray<CoverResult>(1, Allocator.TempJob);

        try
        {
            for (int i = 0; i < n; i++) nativePoints[i] = points[i];

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
                    RequiredCount = (total + 1) / 2
                };
            }

            var job = new SolveJob
            {
                Points = nativePoints,
                Groups = nativeGroups,
                MaxRadius = maxRadius,
                Result = resultArr
            };
            job.Run();

            return resultArr[0];
        }
        finally
        {
            nativePoints.Dispose();
            nativeGroups.Dispose();
            resultArr.Dispose();
        }
    }

    [BurstCompile]
    private struct SolveJob : IJob
    {
        [ReadOnly] public NativeArray<float2> Points;
        [ReadOnly] public NativeArray<BurstGroup> Groups;
        public float MaxRadius;

        public NativeArray<CoverResult> Result;

        public void Execute()
        {
            int n = Points.Length;
            ulong targetMask = n == 64 ? ulong.MaxValue : (1ul << n) - 1;

            var candidates = new NativeList<Circle>(57, Allocator.Temp);

            // A1~A8
            for (int i = 1; i <= 8; i++) candidates.Add(new Circle { Center = RingPos(4.1f, i, false), Radius = MaxRadius });
            // B1~B8
            for (int i = 1; i <= 8; i++) candidates.Add(new Circle { Center = RingPos(2.3f, i, false), Radius = MaxRadius });
            // C
            candidates.Add(new Circle { Center = float2.zero, Radius = MaxRadius });
            // A-B Intersections
            for (int i = 1; i <= 8; i++) candidates.Add(new Circle { Center = RingPos(3.2f, i, false), Radius = MaxRadius });
            // B-B Intersections
            for (int i = 1; i <= 8; i++) candidates.Add(new Circle { Center = RingPos(2.3f, i, true), Radius = MaxRadius });
            // B-C Intersections
            for (int i = 1; i <= 8; i++) candidates.Add(new Circle { Center = RingPos(1.15f, i, false), Radius = MaxRadius });
            // D1~D8
            for (int i = 1; i <= 8; i++) candidates.Add(new Circle { Center = RingPos(4.1f, i, true), Radius = MaxRadius });
            // E1~E8
            for (int i = 1; i <= 8; i++) candidates.Add(new Circle { Center = RingPos(3.0f, i, true), Radius = MaxRadius });

            float radiusSq = MaxRadius * MaxRadius;
            int maxCandidates = 57;

            var maskToCircle = new NativeHashMap<ulong, Circle>(maxCandidates, Allocator.Temp);
            var uniqueMasks = new NativeList<ulong>(maxCandidates, Allocator.Temp);

            bool foundSingleGroup = false;
            Circle bestSingleGroupCircle = default;

            for (int i = 0; i < candidates.Length; i++)
            {
                Circle c = candidates[i];
                ulong mask = GetCoveredMask(c.Center, radiusSq, Points);

                if (mask == targetMask)
                {
                    Result[0] = new CoverResult { Mode = CoverMode.SingleCircleDirect, Circle1 = c };
                    Cleanup(candidates, maskToCircle, uniqueMasks);
                    return;
                }

                if (!maskToCircle.ContainsKey(mask))
                {
                    maskToCircle.Add(mask, c);
                    uniqueMasks.Add(mask);

                    if (!foundSingleGroup)
                    {
                        ulong expanded = ExpandGroups(mask, Groups);
                        if (expanded == targetMask)
                        {
                            foundSingleGroup = true;
                            bestSingleGroupCircle = c;
                        }
                    }
                }
            }

            if (foundSingleGroup)
            {
                Result[0] = new CoverResult { Mode = CoverMode.SingleCircleGroup, Circle1 = bestSingleGroupCircle };
                Cleanup(candidates, maskToCircle, uniqueMasks);
                return;
            }

            bool foundDoubleGroup = false;
            Circle bestD1 = default, bestD2 = default;
            var checkedMasks = new NativeHashSet<ulong>(4096, Allocator.Temp);

            int numMasks = uniqueMasks.Length;
            for (int i = 0; i < numMasks; i++)
            {
                ulong m1 = uniqueMasks[i];
                Circle c1 = maskToCircle[m1];

                for (int j = i; j < numMasks; j++)
                {
                    ulong m2 = uniqueMasks[j];
                    ulong combined = m1 | m2;

                    if (combined == targetMask)
                    {
                        Result[0] = new CoverResult
                        {
                            Mode = CoverMode.DoubleCircleDirect,
                            Circle1 = c1,
                            Circle2 = maskToCircle[m2]
                        };
                        checkedMasks.Dispose();
                        Cleanup(candidates, maskToCircle, uniqueMasks);
                        return;
                    }

                    if (!foundDoubleGroup)
                    {
                        if (checkedMasks.Add(combined))
                        {
                            ulong expanded = ExpandGroups(combined, Groups);
                            if (expanded == targetMask)
                            {
                                foundDoubleGroup = true;
                                bestD1 = c1;
                                bestD2 = maskToCircle[m2];
                            }
                        }
                    }
                }
            }

            checkedMasks.Dispose();
            Cleanup(candidates, maskToCircle, uniqueMasks);

            if (foundDoubleGroup)
            {
                Result[0] = new CoverResult
                {
                    Mode = CoverMode.DoubleCircleGroup,
                    Circle1 = bestD1,
                    Circle2 = bestD2
                };
                return;
            }

            Result[0] = new CoverResult { Mode = CoverMode.None };
        }

        private void Cleanup(NativeList<Circle> c, NativeHashMap<ulong, Circle> m, NativeList<ulong> u)
        {
            c.Dispose();
            m.Dispose();
            u.Dispose();
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

        private ulong GetCoveredMask(float2 center, float radiusSq, NativeArray<float2> points)
        {
            ulong mask = 0;
            for (int i = 0; i < points.Length; i++)
            {
                if (math.distancesq(center, points[i]) <= radiusSq + 1e-3f)
                {
                    mask |= (1ul << i);
                }
            }
            return mask;
        }

        private float2 RingPos(float radius, int index1, bool altAngle)
        {
            float a = altAngle
                ? (index1 * -2f + 6f) * 0.125f * math.PI
                : (index1 * -2f + 5f) * 0.125f * math.PI;
            return new float2(radius * math.cos(a), radius * math.sin(a));
        }
    }
}
