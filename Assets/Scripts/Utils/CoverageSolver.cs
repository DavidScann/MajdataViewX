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
        public uint PointMask;
        public int RequiredCount;
    }

    public static CoverResult Solve(
        IReadOnlyList<float2> points,
        IReadOnlyList<Group> groups,
        float maxRadius = 1.8f)
    {
        int n = points.Count;
        if (n == 0) return new CoverResult { Mode = CoverMode.None };
        if (n > 32) throw new ArgumentException("Points count must be < 33 to fit in uint mask.");

        var nativePoints = new NativeArray<float2>(n, Allocator.TempJob);
        var nativeGroups = new NativeArray<BurstGroup>(groups.Count, Allocator.TempJob);
        var resultArr = new NativeArray<CoverResult>(1, Allocator.TempJob);

        try
        {
            for (int i = 0; i < n; i++) nativePoints[i] = points[i];

            for (int i = 0; i < groups.Count; i++)
            {
                uint mask = 0;
                if (groups[i].PointIndices != null)
                {
                    foreach (int idx in groups[i].PointIndices)
                    {
                        if (idx >= 0 && idx < n)
                            mask |= (1u << idx);
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
            uint targetMask = n == 32 ? uint.MaxValue : (1u << n) - 1;

            Circle mec = MinimumEnclosingCircle(Points);
            if (mec.Radius <= MaxRadius + 1e-4f)
            {
                Result[0] = new CoverResult
                {
                    Mode = CoverMode.SingleCircleDirect,
                    Circle1 = mec
                };
                return;
            }

            int maxCandidates = n + n * (n - 1);
            var candidates = new NativeList<Circle>(maxCandidates, Allocator.Temp);

            float radiusSq = MaxRadius * MaxRadius;

            for (int i = 0; i < n; i++)
            {
                candidates.Add(new Circle { Center = Points[i], Radius = MaxRadius });

                for (int j = i + 1; j < n; j++)
                {
                    float2 p1 = Points[i];
                    float2 p2 = Points[j];
                    float distSq = math.distancesq(p1, p2);

                    if (distSq <= (MaxRadius * 2) * (MaxRadius * 2) + 1e-4f)
                    {
                        float2 mid = (p1 + p2) * 0.5f;
                        float dToMidSq = distSq * 0.25f;
                        float hSq = math.max(0f, radiusSq - dToMidSq);
                        float h = math.sqrt(hSq);

                        float2 dir = p2 - p1;
                        float dist = math.sqrt(distSq);
                        if (dist > 1e-5f)
                        {
                            dir /= dist;
                            float2 normal = new float2(-dir.y, dir.x);

                            candidates.Add(new Circle { Center = mid + normal * h, Radius = MaxRadius });
                            candidates.Add(new Circle { Center = mid - normal * h, Radius = MaxRadius });
                        }
                    }
                }
            }

            var maskToCircle = new NativeHashMap<uint, Circle>(maxCandidates, Allocator.Temp);
            var uniqueMasks = new NativeList<uint>(maxCandidates, Allocator.Temp);

            bool foundSingleGroup = false;
            Circle bestSingleGroupCircle = default;

            for (int i = 0; i < candidates.Length; i++)
            {
                Circle c = candidates[i];
                uint mask = GetCoveredMask(c.Center, radiusSq, Points);

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
                        uint expanded = ExpandGroups(mask, Groups);
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
            var checkedMasks = new NativeHashSet<uint>(4096, Allocator.Temp);

            int numMasks = uniqueMasks.Length;
            for (int i = 0; i < numMasks; i++)
            {
                uint m1 = uniqueMasks[i];
                Circle c1 = maskToCircle[m1];

                for (int j = i; j < numMasks; j++)
                {
                    uint m2 = uniqueMasks[j];
                    uint combined = m1 | m2;

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
                            uint expanded = ExpandGroups(combined, Groups);
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

        private void Cleanup(NativeList<Circle> c, NativeHashMap<uint, Circle> m, NativeList<uint> u)
        {
            c.Dispose();
            m.Dispose();
            u.Dispose();
        }

        private uint ExpandGroups(uint coveredMask, NativeArray<BurstGroup> groups)
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
                        uint intersect = coveredMask & g.PointMask;
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

        private uint GetCoveredMask(float2 center, float radiusSq, NativeArray<float2> points)
        {
            uint mask = 0;
            for (int i = 0; i < points.Length; i++)
            {
                if (math.distancesq(center, points[i]) <= radiusSq + 1e-3f)
                {
                    mask |= (1u << i);
                }
            }
            return mask;
        }

        private Circle MinimumEnclosingCircle(NativeArray<float2> points)
        {
            int n = points.Length;
            if (n == 0) return new Circle { Center = float2.zero, Radius = 0f };
            if (n == 1) return new Circle { Center = points[0], Radius = 0f };

            Circle minCircle = new Circle { Center = float2.zero, Radius = float.MaxValue };
            bool found = false;

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    float2 p1 = points[i];
                    float2 p2 = points[j];
                    float2 center = (p1 + p2) * 0.5f;
                    float rSq = math.distancesq(p1, p2) * 0.25f;
                    float radius = math.sqrt(rSq);

                    if (radius < minCircle.Radius && EnclosesAll(center, rSq, points))
                    {
                        minCircle = new Circle { Center = center, Radius = radius };
                        found = true;
                    }
                }
            }

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    for (int k = j + 1; k < n; k++)
                    {
                        Circle c = Circumcircle(points[i], points[j], points[k]);
                        if (c.Radius >= 0 && c.Radius < minCircle.Radius)
                        {
                            float rSq = c.Radius * c.Radius;
                            if (EnclosesAll(c.Center, rSq, points))
                            {
                                minCircle = c;
                                found = true;
                            }
                        }
                    }
                }
            }
            return found ? minCircle : new Circle { Center = float2.zero, Radius = 0f };
        }

        private bool EnclosesAll(float2 center, float radiusSq, NativeArray<float2> points)
        {
            float tolerance = 1e-4f;
            for (int i = 0; i < points.Length; i++)
            {
                if (math.distancesq(center, points[i]) > radiusSq + tolerance)
                    return false;
            }
            return true;
        }

        private Circle Circumcircle(float2 a, float2 b, float2 c)
        {
            float2 d = b - a;
            float2 e = c - a;
            float bl = math.lengthsq(d);
            float cl = math.lengthsq(e);
            float det = d.x * e.y - d.y * e.x;

            if (math.abs(det) < 1e-5f)
            {
                return new Circle { Center = float2.zero, Radius = -1f };
            }

            float2 offset = new float2(
                (e.y * bl - d.y * cl) / (2f * det),
                (d.x * cl - e.x * bl) / (2f * det)
            );
            return new Circle { Center = a + offset, Radius = math.length(offset) };
        }
    }
}
