using System;
using System.Collections.Generic;
using System.Numerics;

namespace Notes.SlideUtils
{
    public readonly struct SlideArrowData
    {
        public readonly Complex Point;
        public readonly Complex Direction;
        public readonly double PathLength;

        public SlideArrowData(Complex point, Complex direction, double pathLength)
        {
            Point = point;
            Direction = direction;
            PathLength = pathLength;
        }
    }

    public readonly struct SlideAreaData
    {
        /// <summary>
        /// 判定段激活以后 slide 完成的长度
        /// </summary>
        public readonly double LengthAfterPush;

        /// <summary>
        /// 判定段完成以后 slide 完成的长度
        /// </summary>
        public readonly double LengthAfterFinish;

        public readonly int[] SensorAreas;

        public SlideAreaData(double lengthAfterPush, double lengthAfterFinish, int[] areas)
        {
            LengthAfterPush = lengthAfterPush;
            LengthAfterFinish = lengthAfterFinish;
            SensorAreas = areas;
        }
    }


    /// <summary>
    /// <p>用于生成 slide 相关 metadata 的工具类</p>
    /// <p>目前包含箭头数据和判定区数据</p>
    /// </summary>
    public static class SlideDataBuilder
    {
        public static List<SlideArrowData> BuildArrowData(ParametricSlidePath path)
        {
            var result = new List<SlideArrowData>();
            var totalLength = path.GetPathLength();
            var totalSegCount = path.Segments.Length;

            var currentLength = 0.0;
            var segIdx = 0;

            while (currentLength < totalLength)
            {
                var t = currentLength / totalLength;
                var pt = path.GetPointAt(t);
                var tg = path.GetTangentAt(t);

                if (path.GetSegmentAt(t, out _).IsCurve)
                {
                    // 在官机中，圆弧形 slide 箭头的朝向是“上个箭头 → 当前箭头”的延长线方向
                    // （严格来说前 4 ~ 6 个箭头有一个收敛过程，但我不想复刻这个）

                    var lastPoint = result[^1].Point;
                    tg = pt - lastPoint;
                    tg /= tg.Magnitude;
                }

                result.Add(new SlideArrowData(pt, tg, currentLength));

                // 计算下一个箭头放在哪里
                var nextLength = currentLength + path.Segments[segIdx].ArrowDistance;

                if (segIdx < totalSegCount - 1 && nextLength >= path.AccumulatedLengths[segIdx])
                {
                    // 即将切换 Segment
                    if (path.Segments[segIdx + 1].ParseMarker == SlideParseMarker.SmoothAlign)
                    {
                        // SmoothAlign 标志的意思是，调节本段箭头间距使得结束时箭头位置恰好在本段终点
                        // P.S. 这种情况出现在 ppqq 圈进入判定线大圆，可以把转移轨道的箭头间距微调一下，让大圆的箭头对齐
                        var delta = path.AccumulatedLengths[segIdx + 1] - currentLength;
                        var n = Math.Round(delta / MajGeometry.DefaultDistance);
                        path.Segments[segIdx + 1].SetArrowDistance(delta / n);
                        nextLength = currentLength + delta / n;
                    }

                    if (path.Segments[segIdx].ParseMarker == SlideParseMarker.ForceAlign)
                    {
                        // ForceAlign 标志的意思是，结束时把当前的位置强制对齐到本段的终点
                        // 于是下一个箭头应该出现在转折点之后一个间隔处
                        // P.S. 这种情况一般是出现在一条直线连接到判定线大圆，这个处理是为了让大圆的箭头对齐
                        nextLength = path.AccumulatedLengths[segIdx] + path.Segments[segIdx + 1].ArrowDistance;
                    }

                    segIdx++;
                }

                currentLength = nextLength;
            }

            // 把路径终点补上
            result.Add(new SlideArrowData(path.GetPointAt(1.0), path.GetTangentAt(1.0), totalLength));

            return result;
        }

        /// <summary>
        /// <p>key 是两个 5 bit 整数拼起来，表示高 5 位的判定区前往低 5 位的判定区</p>
        /// <p>5 bit 整数与判定区的对应符合<c>SensorType</c>的定义，可取范围 0~16</p>
        /// <p>value 记录这一段路径如何被各个判定段切割</p>
        /// <p><c>LengthAfterPush</c>是“判定区出点”的位置</p>
        /// <p><c>LengthAfterFinish</c>是“下个判定区入点”的位置</p>
        /// <p>这个字典只能应付由官机 slide 片段生成的 slide 形状，无法处理过于超前的形状</p>
        /// </summary>
        public static readonly Dictionary<int, SlideAreaData[]> HitAreasLookup = new();

        /// <summary>
        /// 初始化判定区探测算法，给<c>HitAreasLookup</c>打表
        /// </summary>
        public static void InitializeHitAreasLookup()
        {
            for (var i = 0; i < 8; i++)
            {
                for (var j = 0; j < 8; j++)
                {
                    AddHitAreasLookupEntries(i, j);
                }
            }
        }

        private static void AddHitAreasLookupEntries(int i, int j)
        {
            // 这里的各种 magic number 都是实测的数据，不要动
            var diff = (j - i) & 7; // 其实就是 % 8 ... 某种对负数的兼容性
            int tmp, tmp2;

            // Ai -> Aj
            var key = (i << 5) | j;
            switch (diff)
            {
                // 只需要考虑 1/2/6/7
                // 相隔 3/4/5 个键位的 A 区不可能直接到达而不接触其他判定区
                case 1:
                case 7:
                {
                    HitAreasLookup[key] = new[]
                    {
                        new SlideAreaData(0.32, 0.68, new[] { i }),
                        new SlideAreaData(1.00, 1.00, new[] { j })
                    };
                    break;
                }
                case 2:
                case 6:
                {
                    tmp = (diff == 2) ? (i + 1) & 7 : (i - 1) & 7;
                    HitAreasLookup[key] = new[]
                    {
                        new SlideAreaData(0.20, 0.38, new[] { i }),
                        new SlideAreaData(0.62, 0.80, new[] { tmp, tmp | 8 }),
                        new SlideAreaData(1.00, 1.00, new[] { j })
                    };
                    break;
                }
            }

            // Bi -> Bj
            key = ((i | 8) << 5) | (j | 8);
            switch (diff)
            {
                // 1~7 中除了 4 都有可能
                // 相隔 4 个键位的 B 区之间必然经过 C 区
                case 1:
                case 7:
                {
                    HitAreasLookup[key] = new[]
                    {
                        new SlideAreaData(0.44, 0.56, new[] { i | 8 }),
                        new SlideAreaData(1.00, 1.00, new[] { j | 8 })
                    };
                    break;
                }
                case 2:
                case 6:
                {
                    tmp = (diff == 2) ? (i + 1) & 7 : (i - 1) & 7;
                    HitAreasLookup[key] = new[]
                    {
                        new SlideAreaData(0.22, 0.35, new[] { i | 8 }),
                        new SlideAreaData(0.65, 0.78, new[] { tmp | 8, 16 }),
                        new SlideAreaData(1.00, 1.00, new[] { j | 8 })
                    };
                    break;
                }
                case 3:
                case 5:
                {
                    tmp = (diff == 3) ? (i + 1) & 7 : (i - 1) & 7;
                    tmp2 = (diff == 3) ? (i + 2) & 7 : (i - 2) & 7;
                    HitAreasLookup[key] = new[]
                    {
                        new SlideAreaData(0.15, 0.28, new[] { i | 8 }),
                        new SlideAreaData(0.48, 0.52, new[] { tmp | 8, 16 }),
                        new SlideAreaData(0.72, 0.85, new[] { tmp2 | 8, 16 }),
                        new SlideAreaData(1.00, 1.00, new[] { j | 8 })
                    };
                    break;
                }
            }

            // Ai <-> Bj
            key = (i << 5) | (j | 8);
            var key2 = ((j | 8) << 5) | i;
            switch (diff)
            {
                // 2/4/6 是不可能的，剩下 0/1/3/5/7
                case 0:
                {
                    HitAreasLookup[key] = new[]
                    {
                        new SlideAreaData(0.60, 0.75, new[] { i }),
                        new SlideAreaData(1.00, 1.00, new[] { j | 8 })
                    };
                    HitAreasLookup[key2] = new[]
                    {
                        new SlideAreaData(0.25, 0.40, new[] { j | 8 }),
                        new SlideAreaData(1.00, 1.00, new[] { i })
                    };
                    break;
                }
                case 1:
                case 7:
                {
                    HitAreasLookup[key] = new[]
                    {
                        new SlideAreaData(0.45, 0.77, new[] { i }),
                        new SlideAreaData(1.00, 1.00, new[] { j | 8 })
                    };
                    HitAreasLookup[key2] =
                        new[]
                        {
                            new SlideAreaData(0.23, 0.55, new[] { j | 8 }),
                            new SlideAreaData(1.00, 1.00, new[] { i })
                        };
                    break;
                }
                case 3:
                case 5:
                {
                    tmp = (diff == 3) ? (i + 1) & 7 : (i - 1) & 7;
                    tmp2 = (diff == 3) ? (i + 2) & 7 : (i - 2) & 7;
                    HitAreasLookup[key] = new[]
                    {
                        new SlideAreaData(0.25, 0.34, new[] { i }),
                        new SlideAreaData(0.54, 0.68, new[] { i | 8, tmp | 8 }),
                        new SlideAreaData(0.85, 0.90, new[] { tmp2 | 8, 16 }),
                        new SlideAreaData(1.00, 1.00, new[] { j | 8 })
                    };
                    HitAreasLookup[key2] = new[]
                    {
                        new SlideAreaData(0.10, 0.15, new[] { j | 8 }),
                        new SlideAreaData(0.32, 0.46, new[] { tmp2 | 8, 16 }),
                        new SlideAreaData(0.66, 0.75, new[] { i | 8, tmp | 8 }),
                        new SlideAreaData(1.00, 1.00, new[] { i })
                    };
                    break;
                }
            }

            // C <-> Bj
            // C 区不可能绕过 B 区直接去 A 区
            key = (16 << 5) | (j | 8);
            key2 = ((j | 8) << 5) | 16;
            HitAreasLookup[key] = new[]
            {
                new SlideAreaData(0.50, 0.70, new[] { 16 }),
                new SlideAreaData(1.00, 1.00, new[] { j | 8 })
            };
            HitAreasLookup[key2] = new[]
            {
                new SlideAreaData(0.30, 0.50, new[] { j | 8 }),
                new SlideAreaData(1.00, 1.00, new[] { 16 })
            };
        }

        // 判定区探测算法的参数，不要动
        public static readonly double HitAreaCalcStep = MajGeometry.MainRadius / 48.0;

        public static readonly double HitAreaARadius = MajGeometry.MainRadius * 80.0 / 480.0;
        public static readonly double HitAreaADistance = MajGeometry.MainRadius * 440.0 / 480.0;
        public static readonly double HitAreaBRadius = MajGeometry.MainRadius * 45.0 / 480.0;
        public static readonly double HitAreaBDistance = MajGeometry.MainRadius * 210.0 / 480.0;
        public static readonly double HitAreaCRadius = MajGeometry.MainRadius * 55.0 / 480.0;

        public static readonly double LastDistanceCircle = MajGeometry.MainRadius * 175.0 / 480.0;
        public static readonly double LastDistanceShort = MajGeometry.MainRadius * 130.0 / 480.0;
        public static readonly double LastDistanceLong = MajGeometry.MainRadius * 159.0 / 480.0;

        /// <summary>
        /// 计算指定 slide 路径的判定区序列
        /// </summary>
        /// <param name="path">参数化 slide 路径对象</param>
        /// <returns>判定区序列</returns>
        public static List<SlideAreaData> BuildHitAreas(ParametricSlidePath path)
        {
            // 第一步，计算 slide 路径上“最接近每个判定区的点”
            // 不考虑 OR 判定区，只看恰好经过一个判定区的情况

            var nodeList = new List<Tuple<int, double>>(); // 保存判定区以及最接近它的点（用经过的路径长度表示）
            var totalLength = path.GetPathLength();
            var count = (int)Math.Round(totalLength / HitAreaCalcStep);

            int? lastNode = null;
            var enterLength = 0.0;

            for (var i = 0; i < count; i++)
            {
                // 计算现在的位置
                var t = (double)i / count;
                var pt = path.GetPointAt(t);
                int? node = null;

                // 检查是否落在某个判定区的中心领域内
                if (pt.Magnitude < HitAreaCRadius)
                {
                    node = 16;
                }
                else
                    for (var j = 0; j < 8; j++)
                    {
                        var phi = Math.PI * (3.0 / 8.0 - j / 4.0);
                        if ((pt - Complex.FromPolarCoordinates(HitAreaADistance, phi)).Magnitude < HitAreaARadius)
                        {
                            node = j;
                            break;
                        }

                        if ((pt - Complex.FromPolarCoordinates(HitAreaBDistance, phi)).Magnitude < HitAreaBRadius)
                        {
                            node = j | 8;
                            break;
                        }
                    }
                // node 可能为 null，也可能为 0 ~ 16 中的某个

                if (lastNode != node)
                {
                    // 进入或离开了某个判定区的中心领域
                    var length = t * totalLength;
                    if (lastNode == null)
                    {
                        // 进入的情况，记录路径进入时的长度
                        enterLength = length;
                    }
                    else
                    {
                        // 离开的情况，则认为进入和离开位置的中点是“最接近该判定区的点”
                        nodeList.Add(new Tuple<int, double>(lastNode.Value, (length + enterLength) / 2.0));

                        if (node != null)
                        {
                            // 正好又进入了新一个判定区的领域
                            enterLength = length;
                        }
                    }
                }

                lastNode = node;
            }

            // 补上最后一个区，此时必然位于某个 A 区领域内，所以 lastNode 必不为 null
            nodeList.Add(new Tuple<int, double>(lastNode!.Value, totalLength));
            // 把第一个判定区的“最接近点”设为 slide 起点
            // 因为按照上述算法，如果不这么做，“最接近点”将会是 起点~离开第一个区 这段路径的中点
            nodeList[0] = new Tuple<int, double>(nodeList[0].Item1, 0.0);

            // ========== ========== ========== ========== ========== ========== ==========
            // 第二步，生成判定区列表，以及相应的 push 和 finish
            // OR 判定区在这一步根据预先打好的表生成

            // ReSharper disable once UseObjectOrCollectionInitializer
            var result = new List<SlideAreaData>();
            result.Add(new SlideAreaData(0.0, 0.0, new[] { nodeList[0].Item1 }));

            for (var i = 1; i < nodeList.Count; i++)
            {
                // 生成查表的 key：高 5 位是上一判定区，低 5 位是下一判定区
                var key = (nodeList[i - 1].Item1 << 5) | nodeList[i].Item1;
                // 这是“最接近”两判定区的点之间的距离
                var lastLength = nodeList[i - 1].Item2;
                var segmentLength = nodeList[i].Item2 - lastLength;

                var data = HitAreasLookup[key];
                var area = result[^1];

                // 按照预先打表的性质，此时上一个区的 push 和 finish 应该都是“判定区中心”
                // 那么这里再给 push 加上“中心到出点”，finish 加上“中心到下一个入点”
                result[^1] = new SlideAreaData(
                    lastLength + segmentLength * data[0].LengthAfterPush,
                    lastLength + segmentLength * data[0].LengthAfterFinish,
                    area.SensorAreas
                );

                // 根据预先打好的表生成判定区
                for (var j = 1; j < data.Length; j++)
                {
                    result.Add(new SlideAreaData(
                        lastLength + segmentLength * data[j].LengthAfterPush,
                        lastLength + segmentLength * data[j].LengthAfterFinish,
                        data[j].SensorAreas
                    ));
                }
            }

            // ========== ========== ========== ========== ========== ========== ==========
            // 第三步，修正最后一个区的入点，使之差不多吻合官机的尾判时机

            var lastDistance = 0.0;

            if (path.Segments[^1] is LineSegment)
            {
                // 最后一个区是直线进入
                var diff = nodeList[^1].Item1 - nodeList[^2].Item1;
                diff &= 7;
                lastDistance = diff switch
                {
                    // A2->A1, A3->A1, B2->A1
                    1 or 2 or 6 or 7 => LastDistanceShort,
                    // B1->A1, B4->A1
                    _ => LastDistanceLong
                };
            }
            else
            {
                // 最后一个区是圆弧进入
                lastDistance = LastDistanceCircle;
            }

            // 然后修正倒数第二区和最后一区的 push 和 finish
            // ReSharper disable once InconsistentNaming
            var last2ndArea = result[^2];
            var lastArea = result[^1];
            result[^2] = new SlideAreaData(last2ndArea.LengthAfterPush, totalLength - lastDistance,
                last2ndArea.SensorAreas);
            result[^1] = new SlideAreaData(totalLength, totalLength, lastArea.SensorAreas);

            return result;
        }
    }
}