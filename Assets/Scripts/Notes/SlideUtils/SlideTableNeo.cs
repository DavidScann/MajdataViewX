using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using JetBrains.Annotations;

namespace Notes.SlideUtils
{
    /// <summary>
    /// slide 判定段信息
    /// </summary>
    public readonly struct SlideArea
    {
        /// <summary>
        /// 判定段激活以后消除的箭头总数
        /// </summary>
        public readonly int ArrowProgressPush;

        /// <summary>
        /// 判定段完成以后消除的箭头总数
        /// </summary>
        public readonly int ArrowProgressFinish;

        /// <summary>
        /// 判定段包含的判定区
        /// </summary>
        public readonly SensorType[] Sensors;

        public SlideArea(int progressPush, int progressFinish, SensorType[] sensors)
        {
            ArrowProgressPush = progressPush;
            ArrowProgressFinish = progressFinish;
            Sensors = sensors;
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
        public readonly float SlideConst;
        public readonly bool ConditionalLastArrow;
        
        public readonly SlideArea[] JudgeAreaQueue;
        public readonly SlidePose[] ArrowPoses;
        public readonly SlidePose OkPose;
        
        public readonly SlideOkType OkType;
        
        [CanBeNull] public readonly SlideArea[] JudgeAreaQueueL;
        [CanBeNull] public readonly SlideArea[] JudgeAreaQueueR;

        public SlideTableEntry(
            SlideArea[] judgeAreaQueue, 
            float slideConst, 
            SlidePose[] arrowPoses,
            bool conditionalLastArrow, 
            SlidePose okPose, 
            SlideOkType okType
            )
        {
            SlideConst = slideConst;
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
            SlidePose[] arrowPoses, 
            bool conditionalLastArrow, 
            SlidePose okPose, 
            SlideOkType okType
            )
        {
            SlideConst = slideConst;
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
            for (var i = 1; i <= arrowRawData.Length - 2; i++)
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
            SensorType[] sensors;
            for (var i = 0; i <= areaRawData.Length - 2; i++) // 最后一个判定段要特殊处理
            {
                while (arrowRawData[arrowIdx].PathLength <= areaRawData[i].LengthAfterPush) arrowIdx++;
                var push = Math.Max(arrowIdx - 2, 0);
                while (arrowRawData[arrowIdx].PathLength <= areaRawData[i].LengthAfterFinish) arrowIdx++;
                var finish = Math.Max(arrowIdx - 2, 0);
                sensors = areaRawData[i].SensorAreas.Cast<SensorType>().ToArray();
                areaList.Add(new SlideArea(push, finish, sensors));
            }

            sensors = areaRawData[^1].SensorAreas.Cast<SensorType>().ToArray();
            areaList.Add(new SlideArea(arrowCount, arrowCount, sensors));

            var slideConst = (float)(1.0 - areaRawData[^2].LengthAfterFinish / slidePath.GetPathLength());

            // ========== ========== ========== ========== ========== ========== ==========
            // 生成 slideOk

            var endShape = slidePath.GetEndShape();
            var endButton = areaRawData[^1].SensorAreas[0] + 1;   // 直接从判定队列里抠出最后一个区的键位
            SlideOkType okType;
            SlidePose okPose;
            
            switch (endShape)
            {
                case SlideEndShape.CircleL:
                {
                    okType = SlideOkType.CircleL;
                    okPose = CalcCircleOkPose(endButton, true);
                    break;
                }
                case SlideEndShape.CircleR:
                {
                    okType = SlideOkType.CircleR;
                    okPose = CalcCircleOkPose(endButton, false);
                    break;
                }
                default:
                {
                    var isLeft = (endButton > 4);
                    okType = isLeft ? SlideOkType.StraightL : SlideOkType.StraightR;
                    okPose = CalcStraightOkPose(arrowRawData[^1], isLeft);
                    break;
                }
            }

            return new SlideTableEntry(areaList.ToArray(), slideConst, arrowPoseList.ToArray(), 
                conditionalLastArrow, okPose, okType);
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
                    new[] { (SensorType)((start - 1) & 7) }), // A1=0
                new(WifiArrow[2], WifiArrow[3],
                    new[] { (SensorType)((start - 1) & 7 | 8) }), // B1=8
                new(WifiArrow[4], WifiArrow[5],
                    new[] { SensorType.C }),
                new(WifiArrow[6], WifiArrow[6],
                    new[] { (SensorType)((start + 3) & 7), (SensorType)((start + 3) & 7 | 8) }), // B5=12, A5=4
            };
            var judgeR = new SlideArea[]    // 1w5 的 1-4 部分
            {
                new(WifiArrow[0], WifiArrow[1],
                    new[] { (SensorType)((start - 1) & 7) }), // A1=0
                new(WifiArrow[2], WifiArrow[3],
                    new[] { (SensorType)(start & 7 | 8) }), // B2=9
                new(WifiArrow[4], WifiArrow[5],
                    new[] { (SensorType)((start + 1) & 7 | 8) }), // B3=10
                new(WifiArrow[6], WifiArrow[6],
                    new[] { (SensorType)((start + 2) & 7), (SensorType)((start + 3) & 7 + 17) }), // A4=3, D5=21
            };
            var judgeL = new SlideArea[]    // 1w5 的 1-6 部分
            {
                new(WifiArrow[0], WifiArrow[1],
                    new[] { (SensorType)((start - 1) & 7) }), // A1=0
                new(WifiArrow[2], WifiArrow[3],
                    new[] { (SensorType)((start - 2) & 7 | 8) }), // B8=15
                new(WifiArrow[4], WifiArrow[5],
                    new[] { (SensorType)((start - 3) & 7 | 8) }), // B7=14
                new(WifiArrow[6], WifiArrow[6],
                    new[] { (SensorType)((start + 3) & 7), (SensorType)((start + 3) & 7 + 17) }), // A6=5, D6=22
            };
            
            return new SlideTableEntry(
                judgeL, judgeC, judgeR, 0.162870f, 
                arrowPoseList.ToArray(), false,
                okPose, okType
                );
        }
    }
}