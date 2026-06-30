using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Notes.SlideUtils
{
    /// <summary>
    /// slide 判定段信息
    /// </summary>
    public readonly struct SlideArea
    {
        /// <summary>
        /// 判定段按下以后应当消除的箭头总数，注意计数包含了路径起终点，所以一般比实际显示的箭头数目多一个
        /// （最后一个区多两个）
        /// </summary>
        public readonly int ArrowProgressPush;

        /// <summary>
        /// 判定段完成以后应当消除的箭头总数，注意计数包含了路径起终点，所以一般比实际显示的箭头数目多一个
        /// （最后一个区多两个）
        /// </summary>
        public readonly int ArrowProgressFinish;

        /// <summary>
        /// 判定段包含的第一个判定区
        /// </summary>
        public readonly SensorType SensorA;
        
        /// <summary>
        /// 判定段包含的第二个判定区
        /// </summary>
        public readonly SensorType SensorB;

        public SlideArea(int progressPush, int progressFinish, SensorType sensorA, SensorType sensorB)
        {
            ArrowProgressPush = progressPush;
            ArrowProgressFinish = progressFinish;
            SensorA = sensorA;
            SensorB = sensorB;
        }
    }

    /// <summary>
    /// slide 箭头信息
    /// </summary>
    public readonly struct SlidePose
    {
        public readonly float X, Y, RotZ;
        
        public SlidePose(float x, float y, float rotZ)
        {
            X = x;
            Y = y;
            RotZ = rotZ;
        }
    }
    
    public enum SlideOkType
    {
        StraightL,
        StraightR,
        CircleL,
        CircleR,
        WifiU,
        WifiD,
    }

    public readonly struct SlideTableEntry
    {
        /// <summary>slide 最后一个区占全长的比例，0~1 间的小数</summary>
        public readonly float SlideConst;
        /// <summary>slide 路径全长，单位取决于<c>MajGeometry.MainRadius</c></summary>
        public readonly float SlideLength;
        /// <summary>这个参数为 true 表示最后一个箭头距离终点太近，只在 conn-slide 的非最终段显示</summary>
        public readonly bool ConditionalLastArrow;
        /// <summary>判定队列，若为 Wifi 则表示中间一支的判定队列</summary>
        public readonly SlideArea[] JudgeAreaQueue;
        
        /// <summary>
        /// <para>slide 每个箭头的位置与方向</para>
        /// <para>注意数组中包含了路径起点和终点，真正应该显示的箭头需要去掉这两项
        /// （对<c>ConditionalLastArrow = true</c>的情况还要视情况去掉倒数第二项）</para>
        /// <para>普通 slide 可以直接拿这个数组来更新引导星星，虽然箭头间距不一定均匀但大差不差。
        /// Wifi 就别用这个了</para>
        /// </summary>
        public readonly SlidePose[] ArrowPoses;
        
        /// <summary>slideOK 的位置与方向，以图片中点为锚点。
        /// 请注意素材的尺寸：直线和圆弧形 slideOK 为 410x140，Wifi 为 668x200</summary>
        public readonly SlidePose OkPose;
        /// <summary>slideOK 的种类，告诉你该用哪张素材</summary>
        public readonly SlideOkType OkType;
        
        /// <summary>Wifi 左边一支的判定队列（终点在中间支的顺时针方向）</summary>
        public readonly SlideArea[] JudgeAreaQueueL;
        /// <summary>Wifi 右边一支的判定队列（终点在中间支的逆时针方向）</summary>
        public readonly SlideArea[] JudgeAreaQueueR;

        public SlideTableEntry(
            SlideArea[] judgeAreaQueue, 
            float slideConst,
            float slideLength,
            SlidePose[] arrowPoses,
            bool conditionalLastArrow, 
            SlidePose okPose, 
            SlideOkType okType
            )
        {
            SlideConst = slideConst;
            SlideLength = slideLength;
            JudgeAreaQueue = judgeAreaQueue;
            ArrowPoses = arrowPoses;
            OkPose = okPose;
            ConditionalLastArrow = conditionalLastArrow;
            OkType = okType;
            JudgeAreaQueueL = null;
            JudgeAreaQueueR = null;
        }
        
        public SlideTableEntry(
            SlideArea[] judgeAreaQueueL,
            SlideArea[] judgeAreaQueueC,
            SlideArea[] judgeAreaQueueR,
            float slideConst,
            float slideLength,
            SlidePose[] arrowPoses, 
            bool conditionalLastArrow, 
            SlidePose okPose, 
            SlideOkType okType
            )
        {
            SlideConst = slideConst;
            SlideLength = slideLength;
            JudgeAreaQueue = judgeAreaQueueC;
            ArrowPoses = arrowPoses;
            OkPose = okPose;
            ConditionalLastArrow = conditionalLastArrow;
            OkType = okType;
            JudgeAreaQueueL = judgeAreaQueueL;
            JudgeAreaQueueR = judgeAreaQueueR;
        }
    }

    public static class SlideTableNeo
    {
        public static SlidePose CalcArrowPose(SlideArrowRawData rawData)
        {
            var x = (float)rawData.Point.Real;
            var y = (float)rawData.Point.Imaginary;
            var phase = rawData.Direction.Phase;  // -pi ~ pi
            
            // rawData 中 phase 是弧度，0 度是箭头朝正右，逆时针为正
            // View 中 rotZ 是角度，0 度是箭头朝正左，逆时针为正
            var rotZ = (float)(180 + phase * 180 / Math.PI);    // should be 0 ~ 360
            
            return new SlidePose(x, y, rotZ);
        }

        /// <summary>需要贴图尺寸 410x140</summary>
        public const double CircleOkRadius = MajGeometry.MainRadius * 462.0 / 480.0;
        /// <summary>需要贴图尺寸 410x140</summary>
        public const double StraightOkDistance = MajGeometry.MainRadius * 205.0 / 480.0;
        
        /// <summary>
        /// 获取圆弧 slide 的 slideOK 姿势
        /// </summary>
        /// <param name="endButton">1-based idx，1~8</param>
        /// <param name="isCcw">true 表示是逆时针 slide</param>
        public static SlidePose CalcCircleOkPose(int endButton, bool isCcw)
        {
            if (isCcw)
            {
                var rotZ = (float)(360 - 45 * endButton);
                var pos = Complex.FromPolarCoordinates(CircleOkRadius, Math.PI * (2 - endButton) / 4.0);
                return new SlidePose((float)pos.Real, (float)pos.Imaginary, rotZ);
            }
            else
            {
                var rotZ = (float)(405 - 45 * endButton);
                var pos = Complex.FromPolarCoordinates(CircleOkRadius, Math.PI * (3 - endButton) / 4.0);
                return new SlidePose((float)pos.Real, (float)pos.Imaginary, rotZ);
            }
        }

        /// <summary>
        /// 获取直线 slide 的 slideOK 姿势
        /// </summary>
        /// <param name="finalArrow">直接把生成的最后一个箭头数据代进来</param>
        /// <param name="isLeft">true 表示是朝左的 Ok</param>
        public static SlidePose CalcStraightOkPose(SlideArrowRawData finalArrow, bool isLeft)
        {
            var pos = finalArrow.Point - finalArrow.Direction * StraightOkDistance;
            var rotZ = (float)(finalArrow.Direction.Phase * 180.0 / Math.PI);    // should be -180 ~ 180
            if (isLeft)
            {
                rotZ += 180f;
            }
            return new SlidePose((float)pos.Real, (float)pos.Imaginary, rotZ);
        }

        /// <summary>
        /// 使用指定的 slide 路径打表
        /// </summary>
        public static SlideTableEntry CreateTableEntry(ParametricSlidePath slidePath)
        {
            // 整理箭头数据
            
            var arrowRawData = SlideDataBuilder.BuildArrowData(slidePath);
            var arrowPoseList = new List<SlidePose>();

            // 注意要去除路径起点和终点
            for (var i = 0; i < arrowRawData.Length; i++)
            {
                arrowPoseList.Add(CalcArrowPose(arrowRawData[i]));
            }

            var arrowCount = arrowPoseList.Count;

            // 如果最后一个箭头距离终点太近，就只在 conn-slide 里显示
            var conditionalLastArrow =
                arrowRawData[^1].PathLength - arrowRawData[^2].PathLength <= MajGeometry.DefaultDistance / 2.0;

            // ========== ========== ========== ========== ========== ========== ==========
            // 整理判定区数据
            
            var areaRawData = SlideDataBuilder.BuildSlideAreas(slidePath);
            var areaList = new List<SlideArea>();

            var arrowIdx = 1;
            SensorType sensorA;
            SensorType sensorB;
            for (var i = 0; i <= areaRawData.Length - 2; i++) // 最后一个判定段要特殊处理
            {
                while (arrowRawData[arrowIdx].PathLength <= areaRawData[i].LengthAfterPush) arrowIdx++;
                var push = arrowIdx - 1;   // 单纯是因为想多留 1 个箭头而已
                while (arrowRawData[arrowIdx].PathLength <= areaRawData[i].LengthAfterFinish) arrowIdx++;
                var finish = arrowIdx - 1;
                sensorA = (SensorType)areaRawData[i].SensorA;
                sensorB = (SensorType)areaRawData[i].SensorB;
                areaList.Add(new SlideArea(push, finish, sensorA, sensorB));
            }

            sensorA = (SensorType)areaRawData[^1].SensorA;
            sensorB = (SensorType)areaRawData[^1].SensorB;
            areaList.Add(new SlideArea(arrowCount, arrowCount, sensorA,  sensorB));

            var slideLength = slidePath.GetPathLength();
            var slideConst = (float)(1.0 - areaRawData[^2].LengthAfterFinish / slideLength);

            // ========== ========== ========== ========== ========== ========== ==========
            // 生成 slideOk

            var endShape = slidePath.GetEndShape();
            var endButton = areaRawData[^1].SensorA + 1;   // 直接从判定队列里抠出最后一个区的键位
            SlideOkType okType;
            SlidePose okPose;
            
            switch (endShape)
            {
                case SlideEndShape.CircleCCW:
                {
                    okType = SlideOkType.CircleL;
                    okPose = CalcCircleOkPose(endButton, true);
                    break;
                }
                case SlideEndShape.CircleCW:
                {
                    okType = SlideOkType.CircleR;
                    okPose = CalcCircleOkPose(endButton, false);
                    break;
                }
                case SlideEndShape.Straight:
                default:
                {
                    var isLeft = (endButton > 4);
                    okType = isLeft ? SlideOkType.StraightL : SlideOkType.StraightR;
                    okPose = CalcStraightOkPose(arrowRawData[^1], isLeft);
                    break;
                }
            }

            return new SlideTableEntry(
                areaList.ToArray(), slideConst, (float)slideLength,
                arrowPoseList.ToArray(), conditionalLastArrow,
                okPose, okType
                );
        }

        public static readonly double[] WifiPos =
        {
            4.279, 3.658, 3.010, 2.337, 1.637, 0.911, 0.158, -0.621, -1.426, -2.257, -3.115
        };

        public static readonly int[] WifiArrow =
        {
            1, 2, 4, 5, 7, 8, 11
        };
        
        /// <summary>需要贴图尺寸 668x200</summary>
        public const double WifiOkRadius = MajGeometry.MainRadius * 424.0 / 480.0;

        /// <summary>
        /// Wifi 打表
        /// </summary>
        /// <param name="start">起点键位，1-based，1~8</param>
        public static SlideTableEntry CreateWifiEntry(int start)
        {
            var arrowPoseList = new List<SlidePose>();
            var startPoint = MajGeometry.PointGroupA(start);
            var phase = startPoint.Phase;
            
            for (var i = 0; i < 11; i++)
            {
                // // magic
                // var l = 57.63636 + 23.13636 * i + 0.5 * i * i;
                // var y = 5.79371 - 2.62793 * l / 100;
                // var radius = y / 4.8 * MajGeometry.MainRadius;
                var radius = WifiPos[i] / 4.8 * MajGeometry.MainRadius;
                var pos = Complex.FromPolarCoordinates(radius, phase);
                var rotZ = (float)(360 - 45 * start);    // should be 0 ~ 360
                arrowPoseList.Add(new SlidePose((float)pos.Real, (float)pos.Imaginary, rotZ));
            }
            
            var okType = (start is 3 or 4 or 5 or 6) ? SlideOkType.WifiD : SlideOkType.WifiU;
            var okPos = Complex.FromPolarCoordinates(-WifiOkRadius, phase);
            var okRotZ = 157.5f - 45 * start;
            if (okType == SlideOkType.WifiD) okRotZ += 180;
            var okPose = new SlidePose((float)okPos.Real, (float)okPos.Imaginary, okRotZ);

            var judgeC = new SlideArea[]    // 1w5 的 1-5 部分，注意 start 是 1~8 而不是 0~7
            {
                new(WifiArrow[0], WifiArrow[1],
                    (SensorType)((start - 1) & 7), SensorType.Invalid), // A1=0
                new(WifiArrow[2], WifiArrow[3],
                    (SensorType)((start - 1) & 7 | 8), SensorType.Invalid), // B1=8
                new(WifiArrow[4], WifiArrow[5],
                    SensorType.C, SensorType.Invalid),
                new(WifiArrow[6], WifiArrow[6],
                    (SensorType)((start + 3) & 7), (SensorType)((start + 3) & 7 | 8)), // B5=12, A5=4
            };
            var judgeR = new SlideArea[]    // 1w5 的 1-4 部分
            {
                new(WifiArrow[0], WifiArrow[1],
                    (SensorType)((start - 1) & 7), SensorType.Invalid), // A1=0
                new(WifiArrow[2], WifiArrow[3],
                    (SensorType)(start & 7 | 8), SensorType.Invalid), // B2=9
                new(WifiArrow[4], WifiArrow[5],
                    (SensorType)((start + 1) & 7 | 8), SensorType.Invalid), // B3=10
                new(WifiArrow[6], WifiArrow[6],
                    (SensorType)((start + 2) & 7), (SensorType)((start + 3) & 7 + 17)), // A4=3, D5=21
            };
            var judgeL = new SlideArea[]    // 1w5 的 1-6 部分
            {
                new(WifiArrow[0], WifiArrow[1],
                    (SensorType)((start - 1) & 7), SensorType.Invalid), // A1=0
                new(WifiArrow[2], WifiArrow[3],
                    (SensorType)((start - 2) & 7 | 8), SensorType.Invalid), // B8=15
                new(WifiArrow[4], WifiArrow[5],
                    (SensorType)((start - 3) & 7 | 8), SensorType.Invalid), // B7=14
                new(WifiArrow[6], WifiArrow[6],
                    (SensorType)((start - 4) & 7), (SensorType)((start - 4) & 7 + 17)), // A6=5, D6=22
            };
            
            return new SlideTableEntry(
                judgeL, judgeC, judgeR, 0.162870f, (float)(MajGeometry.MainRadius * 2), 
                arrowPoseList.ToArray(), false,
                okPose, okType
                );
        }

        // ReSharper disable once InconsistentNaming
        public static readonly Dictionary<string, SlideTableEntry> SLIDE_TABLE = new();
        // ReSharper disable once InconsistentNaming
        public static readonly Dictionary<string, SlideTableEntry> WIFI_TABLE = new();

        /// <summary>
        /// 打表所有标准 slide 以及 Wifi
        /// </summary>
        public static void InitializeStandardSlideTable()
        {
            SlideDataBuilder.InitializeSlideAreaLookup();
            
            SLIDE_TABLE.Clear();
            WIFI_TABLE.Clear();
            
            for (var diff = 2; diff <= 6; diff++)
            {
                // 直线形
                var path = SlidePathConstructor.BeginAt((int)SensorType.A1)
                    .LineToPoint(diff)
                    .GeneratePath();
                
                for (var i = 0; i <= 7; i++)
                {
                    var start = 1 + i;
                    var end = 1 + ((i + diff) & 7);
                    var key = $"{start}-{end}";
                    path.SetRotoreflection(false, i);
                    SLIDE_TABLE.Add(key, CreateTableEntry(path));
                }
            }

            for (var diff = 0; diff <= 7; diff++)
            {
                // 大圆逆时针
                var phaseNudge = (diff is 1 or 5 or 7) ? -0.001 : 0.001;
                // 官机上 1<2 1<6 1<8 会在 conn-slide 里有缝，剩下 5 种无缝，还原一下
                var pathCircle = SlidePathConstructor.BeginAt((int)SensorType.A1)
                    .ArcToAngle(9, MajGeometry.GetPoint(diff).Phase + phaseNudge, true, false)
                    .GeneratePath();
                // p slide
                ParametricSlidePath pathP;
                if (diff == 5)
                {
                    pathP = SlidePathConstructor.BeginAt((int)SensorType.A1)
                        .TangentToCircle(0, true)
                        .FullCircle(0, true)
                        .LineToPoint(diff)
                        .GeneratePath();
                }
                else
                {
                    pathP = SlidePathConstructor.BeginAt((int)SensorType.A1)
                        .TangentToCircle(0, true)
                        .ArcToTangentTowards(diff, 0, true)
                        .LineToPoint(diff)
                        .GeneratePath();
                }
                // pp slide
                ParametricSlidePath pathPP;
                if (diff == 3 || diff == 4)
                {
                    pathPP = SlidePathConstructor.BeginAt((int)SensorType.A1)
                        .TangentToCircle(3, true)
                        .FullCircle(3, true)
                        .ArcToTangentTowards(diff, 3, true)
                        .LineToPoint(diff)
                        .GeneratePath();
                }
                else
                {
                    pathPP = SlidePathConstructor.BeginAt((int)SensorType.A1)
                        .TangentToCircle(3, true)
                        .ArcToTangentTowards(diff, 3, true)
                        .LineToPoint(diff)
                        .GeneratePath();
                }
                
                for (var i = 0; i <= 7; i++)
                {
                    var start = 1 + i;
                    var end = 1 + ((i + diff) & 7);
                    var key = (start is 3 or 4 or 5 or 6) ? $"{start}>{end}" :  $"{start}<{end}";
                    pathCircle.SetRotoreflection(false, i);
                    SLIDE_TABLE.Add(key, CreateTableEntry(pathCircle));
                    key = $"{start}p{end}";
                    pathP.SetRotoreflection(false, i);
                    SLIDE_TABLE.Add(key, CreateTableEntry(pathP));
                    key = $"{start}pp{end}";
                    pathPP.SetRotoreflection(false, i);
                    SLIDE_TABLE.Add(key, CreateTableEntry(pathPP));
                    
                    end = 1 + ((i - diff) & 7);
                    key = (start is 3 or 4 or 5 or 6) ? $"{start}<{end}" :  $"{start}>{end}";
                    pathCircle.SetRotoreflection(true, i);
                    SLIDE_TABLE.Add(key, CreateTableEntry(pathCircle));
                    key = $"{start}q{end}";
                    pathP.SetRotoreflection(true, i);
                    SLIDE_TABLE.Add(key, CreateTableEntry(pathP));
                    key = $"{start}qq{end}";
                    pathPP.SetRotoreflection(true, i);
                    SLIDE_TABLE.Add(key, CreateTableEntry(pathPP));
                }
            }

            for (var diff = 0; diff <= 7; diff++)
            {
                if (diff == 4) continue;
                // v slide
                var path = SlidePathConstructor.BeginAt((int)SensorType.A1)
                    .LineToPoint((int)SensorType.C)
                    .LineToPoint(diff)
                    .GeneratePath();
                
                for (var i = 0; i <= 7; i++)
                {
                    var start = 1 + i;
                    var end = 1 + ((i + diff) & 7);
                    var key = $"{start}v{end}";
                    path.SetRotoreflection(false, i);
                    SLIDE_TABLE.Add(key, CreateTableEntry(path));
                }
            }

            {
                // zigzag
                var path = SlidePathConstructor.BeginAt((int)SensorType.A1)
                    .LineToPoint((int)SensorType.B7)
                    .LineToPoint((int)SensorType.B3)
                    .LineToPoint((int)SensorType.A5)
                    .GeneratePath();
                
                for (var i = 0; i <= 7; i++)
                {
                    var start = 1 + i;
                    var end = 1 + ((i + 4) & 7);
                    var key = $"{start}s{end}";
                    path.SetRotoreflection(false, i);
                    SLIDE_TABLE.Add(key, CreateTableEntry(path));
                    key = $"{start}z{end}";
                    path.SetRotoreflection(true, i);
                    SLIDE_TABLE.Add(key, CreateTableEntry(path));
                }
            }
            
            for (var diff = 1; diff <= 4; diff++)
            {
                // L slide
                var path = SlidePathConstructor.BeginAt((int)SensorType.A1)
                    .LineToPoint((int)SensorType.A7)
                    .LineToPoint(diff)
                    .GeneratePath();
                
                for (var i = 0; i <= 7; i++)
                {
                    var start = 1 + i;
                    var mid = 1 + ((i - 2) & 7);
                    var end = 1 + ((i + diff) & 7);
                    var key = $"{start}V{mid}{end}";
                    path.SetRotoreflection(false, i);
                    SLIDE_TABLE.Add(key, CreateTableEntry(path));
                    
                    mid = 1 + ((i + 2) & 7);
                    end = 1 + ((i - diff) & 7);
                    key = $"{start}V{mid}{end}";
                    path.SetRotoreflection(true, i);
                    SLIDE_TABLE.Add(key, CreateTableEntry(path));
                }
            }

            // Wifi
            for (var i = 0; i <= 7; i++)
            {
                var start = 1 + i;
                var end = 1 + ((i + 4) & 7);
                var key = $"{start}w{end}";
                WIFI_TABLE.Add(key, CreateWifiEntry(start));
            }

        }
        
    }
}