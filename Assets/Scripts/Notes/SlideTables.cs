#nullable enable

#region

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

#endregion

public struct SlideTableMetadata
{
    //public string Name { get; init; }
    public float Const { get; init; }
}

public struct SlideArea
{
    public SensorType Area0 { get; set; }
    public SensorType Area1 { get; set; }
    public int ArrowProgressWhenOn { get; init; }
    public int ArrowProgressWhenFinished { get; init; }
    public bool IsSkippable { get; set; }
    public bool IsLast { get; set; }

    public bool On { get; set; }
    public bool Off { get; set; }
    public readonly bool IsFinished
    {
        get
        {
            if (IsLast)
                return On;

            return On && Off;
        }
    }

    public void SetIsLast() => IsLast = true;
    public void SetNonLast() => IsLast = false;

    public void Mirror(SensorType baseLine)
    {
        Area0 = Area0.Mirror(baseLine);
        Area1 = Area1.Mirror(baseLine);
    }

    public void Diff(int diff)
    {
        Area0 = Area0.Diff(diff);
        Area1 = Area1.Diff(diff);
    }

    public void Judge(bool status)
    {
        if (status)
        {
            On = true;
        }
        else
        {
            if (On)
            {
                Off = true;
            }
        }
    }
}

public struct SlidePose
{
    public float X, Y, RotZ;
    public SlidePose(float x, float y, float rotZ)
    {
        X = x;
        Y = y;
        RotZ = rotZ;
    }
}

public static class SlideTables
{
    static readonly Dictionary<string, (SlideTableMetadata metadata,
                                        SlideArea[] judgeQueue,
                                        SlidePose[] slidePoses,
                                        SlidePose okPose)>
        SLIDE_TABLES = new()
        {
            ["circle1"] = (
            new SlideTableMetadata()
            {
                // Name = "circle1",
                Const = 0.058f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 3),
                BuildSlideArea(SensorType.A2, 7, 11),
                BuildSlideArea(SensorType.A3, 14, 19),
                BuildSlideArea(SensorType.A4, 23, 27),
                BuildSlideArea(SensorType.A5, 31, 35),
                BuildSlideArea(SensorType.A6, 39, 43),
                BuildSlideArea(SensorType.A7, 46, 51),
                BuildSlideArea(SensorType.A8, 54, 59),
                BuildSlideArea(SensorType.A1, 61, 63, true, true)
            },
            new SlidePose[]
            {
                new(2.254000f, 4.224000f, -208.395708f),
                new(2.659000f, 4.000000f, -212.194825f),
                new(3.023528f, 3.711283f, -218.362979f),
                new(3.383000f, 3.401000f, -222.744546f),
                new(3.694000f, 3.052000f, -228.946227f),
                new(3.977000f, 2.665000f, -234.852590f),
                new(4.232000f, 2.268000f, -239.447499f),
                new(4.415798f, 1.838709f, -245.721893f),
                new(4.585000f, 1.399000f, -251.327386f),
                new(4.697351f, 0.949199f, -255.515687f),
                new(4.765000f, 0.481000f, -262.627694f),
                new(4.803000f, -0.004000f, -267.956572f),
                new(4.775829f, -0.470666f, -271.969895f),
                new(4.701000f, -0.926801f, -279.067426f),
                new(4.593437f, -1.391986f, -283.441501f),
                new(4.432188f, -1.827000f, -289.874059f),
                new(4.228000f, -2.259000f, -295.464708f),
                new(3.979000f, -2.667000f, -301.208904f),
                new(3.694319f, -3.044558f, -306.658427f),
                new(3.389943f, -3.402089f, -311.747243f),
                new(3.038507f, -3.710000f, -317.739344f),
                new(2.658982f, -3.998366f, -323.449047f),
                new(2.256382f, -4.228069f, -328.612484f),
                new(1.825751f, -4.439820f, -334.924203f),
                new(1.385000f, -4.594000f, -340.919780f),
                new(0.930194f, -4.694385f, -347.035934f),
                new(0.462319f, -4.766201f, -354.143893f),
                new(-0.020632f, -4.795044f, -357.668368f),
                new(-0.485648f, -4.785855f, 357.453210f),
                new(-0.944000f, -4.716000f, 352.881362f),
                new(-1.402989f, -4.591456f, 346.209483f),
                new(-1.834251f, -4.442208f, 341.967259f),
                new(-2.258000f, -4.226000f, 334.038284f),
                new(-2.660000f, -3.994000f, 329.317722f),
                new(-3.035000f, -3.716000f, 322.251708f),
                new(-3.385000f, -3.399000f, 317.155464f),
                new(-3.700000f, -3.048000f, 310.953780f),
                new(-3.987000f, -2.663000f, 306.123136f),
                new(-4.221000f, -2.268000f, 298.972475f),
                new(-4.429000f, -1.836000f, 294.178117f),
                new(-4.586000f, -1.398000f, 288.753741f),
                new(-4.703000f, -0.936000f, 284.384321f),
                new(-4.774000f, -0.474000f, 277.298725f),
                new(-4.797000f, 0.001000f, 271.943431f),
                new(-4.775000f, 0.479000f, 267.930117f),
                new(-4.707000f, 0.931000f, 262.355551f),
                new(-4.591000f, 1.400000f, 256.458508f),
                new(-4.429000f, 1.831000f, 251.083736f),
                new(-4.234000f, 2.252000f, 244.435296f),
                new(-3.986000f, 2.674000f, 238.520882f),
                new(-3.707000f, 3.050000f, 233.241578f),
                new(-3.394000f, 3.403000f, 228.238555f),
                new(-3.048000f, 3.718000f, 222.887699f),
                new(-2.666000f, 3.996000f, 216.450957f),
                new(-2.249000f, 4.232000f, 211.287522f),
                new(-1.836000f, 4.436000f, 204.975803f),
                new(-1.388000f, 4.594000f, 198.319595f),
                new(-0.933000f, 4.704000f, 192.864073f),
                new(-0.465000f, 4.769000f, 187.684214f),
                new(0.009000f, 4.802000f, 182.231640f),
                new(0.477000f, 4.779000f, 178.287259f),
                new(0.943000f, 4.709000f, 172.463969f),
                new(1.406000f, 4.589000f, 166.111593f),
            },
            new SlidePose
                (0.044000f, 4.645000f, -0.000000f)
            ),
            ["circle2"] = (
            new SlideTableMetadata()
            {
                // Name = "circle2",
                Const = 0.465f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 3, false),
                BuildSlideArea(SensorType.A2, 5, 7, true, true)
            },
            new SlidePose[]
            {
                new(2.254000f, 4.224000f, -208.395708f),
                new(2.659000f, 4.000000f, -212.194825f),
                new(3.023528f, 3.711283f, -218.362979f),
                new(3.383000f, 3.401000f, -222.744546f),
                new(3.694000f, 3.052000f, -228.946227f),
                new(3.977000f, 2.665000f, -234.852591f),
                new(4.232000f, 2.268000f, -239.447499f),
            },
            new SlidePose
                (3.260000f, 3.260000f, 315.000092f)
            ),
            ["circle3"] = (
            new SlideTableMetadata()
            {
                // Name = "circle3",
                Const = 0.233f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 3),
                BuildSlideArea(SensorType.A2, 7, 11, false),
                BuildSlideArea(SensorType.A3, 13, 15, true, true)
            },
            new SlidePose[]
            {
                new(2.254000f, 4.224000f, -208.395708f),
                new(2.659000f, 4.000000f, -212.194825f),
                new(3.023528f, 3.711283f, -218.362979f),
                new(3.383000f, 3.401000f, -222.744546f),
                new(3.694000f, 3.052000f, -228.946227f),
                new(3.977000f, 2.665000f, -234.852591f),
                new(4.232000f, 2.268000f, -239.447499f),
                new(4.415798f, 1.838709f, -245.721893f),
                new(4.585000f, 1.399000f, -251.327386f),
                new(4.697351f, 0.949199f, -255.515687f),
                new(4.765000f, 0.481000f, -262.627694f),
                new(4.803000f, -0.004000f, -267.956572f),
                new(4.775829f, -0.470666f, -271.969895f),
                new(4.701000f, -0.926801f, -279.067426f),
                new(4.593437f, -1.391986f, -283.441500f),
            },
            new SlidePose
                (4.640000f, -0.010000f, 270.000002f)
            ),
            ["circle4"] = (
            new SlideTableMetadata()
            {
                // Name = "circle4",
                Const = 0.155f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 3),
                BuildSlideArea(SensorType.A2, 7, 11),
                BuildSlideArea(SensorType.A3, 14, 19),
                BuildSlideArea(SensorType.A4, 21, 23, true, true)
            },
            new SlidePose[]
            {
                new(2.254000f, 4.224000f, -208.395708f),
                new(2.659000f, 4.000000f, -212.194825f),
                new(3.023528f, 3.711283f, -218.362979f),
                new(3.383000f, 3.401000f, -222.744546f),
                new(3.694000f, 3.052000f, -228.946227f),
                new(3.977000f, 2.665000f, -234.852591f),
                new(4.232000f, 2.268000f, -239.447499f),
                new(4.415798f, 1.838709f, -245.721893f),
                new(4.585000f, 1.399000f, -251.327386f),
                new(4.697351f, 0.949199f, -255.515687f),
                new(4.765000f, 0.481000f, -262.627694f),
                new(4.803000f, -0.004000f, -267.956572f),
                new(4.775829f, -0.470666f, -271.969895f),
                new(4.701000f, -0.926801f, -279.067426f),
                new(4.593437f, -1.391986f, -283.441500f),
                new(4.432188f, -1.827000f, -289.874059f),
                new(4.228000f, -2.259000f, -295.464708f),
                new(3.979000f, -2.667000f, -301.208904f),
                new(3.694319f, -3.044558f, -306.658427f),
                new(3.389943f, -3.402089f, -311.747243f),
                new(3.038507f, -3.710000f, -317.739344f),
                new(2.658982f, -3.998366f, -323.449047f),
                new(2.256382f, -4.228069f, -328.612484f),
            },
            new SlidePose
                (3.250000f, -3.300000f, 225.000185f)
            ),
            ["circle5"] = (
            new SlideTableMetadata()
            {
                // Name = "circle5",
                Const = 0.116f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 3),
                BuildSlideArea(SensorType.A2, 7, 11),
                BuildSlideArea(SensorType.A3, 14, 19),
                BuildSlideArea(SensorType.A4, 23, 27),
                BuildSlideArea(SensorType.A5, 29, 31, true, true)
            },
            new SlidePose[]
            {
                new(2.254000f, 4.224000f, -208.395708f),
                new(2.659000f, 4.000000f, -212.194825f),
                new(3.023528f, 3.711283f, -218.362979f),
                new(3.383000f, 3.401000f, -222.744546f),
                new(3.694000f, 3.052000f, -228.946227f),
                new(3.977000f, 2.665000f, -234.852591f),
                new(4.232000f, 2.268000f, -239.447499f),
                new(4.415798f, 1.838709f, -245.721893f),
                new(4.585000f, 1.399000f, -251.327386f),
                new(4.697351f, 0.949199f, -255.515687f),
                new(4.765000f, 0.481000f, -262.627694f),
                new(4.803000f, -0.004000f, -267.956572f),
                new(4.775829f, -0.470666f, -271.969895f),
                new(4.701000f, -0.926801f, -279.067426f),
                new(4.593437f, -1.391986f, -283.441500f),
                new(4.432188f, -1.827000f, -289.874059f),
                new(4.228000f, -2.259000f, -295.464708f),
                new(3.979000f, -2.667000f, -301.208904f),
                new(3.694319f, -3.044558f, -306.658427f),
                new(3.389943f, -3.402089f, -311.747243f),
                new(3.038507f, -3.710000f, -317.739344f),
                new(2.658982f, -3.998366f, -323.449047f),
                new(2.256382f, -4.228069f, -328.612484f),
                new(1.825751f, -4.439820f, -334.924203f),
                new(1.385000f, -4.594000f, -340.919780f),
                new(0.930194f, -4.694385f, -347.035934f),
                new(0.462319f, -4.766201f, -354.143893f),
                new(-0.020632f, -4.795044f, -357.668368f),
                new(-0.485648f, -4.785855f, 357.453210f),
                new(-0.944000f, -4.716000f, 352.881362f),
                new(-1.402989f, -4.591456f, 346.209483f),
            },
            new SlidePose
                (-0.040000f, -4.650000f, 180.000159f)
            ),
            ["circle6"] = (
            new SlideTableMetadata()
            {
                // Name = "circle6",
                Const = 0.093f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 3),
                BuildSlideArea(SensorType.A2, 7, 11),
                BuildSlideArea(SensorType.A3, 14, 19),
                BuildSlideArea(SensorType.A4, 23, 27),
                BuildSlideArea(SensorType.A5, 31, 35),
                BuildSlideArea(SensorType.A6, 37, 39, true, true)
            },
            new SlidePose[]
            {
                new(2.254000f, 4.224000f, -208.395708f),
                new(2.659000f, 4.000000f, -212.194825f),
                new(3.023528f, 3.711283f, -218.362979f),
                new(3.383000f, 3.401000f, -222.744546f),
                new(3.694000f, 3.052000f, -228.946227f),
                new(3.977000f, 2.665000f, -234.852591f),
                new(4.232000f, 2.268000f, -239.447499f),
                new(4.415798f, 1.838709f, -245.721893f),
                new(4.585000f, 1.399000f, -251.327386f),
                new(4.697351f, 0.949199f, -255.515687f),
                new(4.765000f, 0.481000f, -262.627694f),
                new(4.803000f, -0.004000f, -267.956572f),
                new(4.775829f, -0.470666f, -271.969895f),
                new(4.701000f, -0.926801f, -279.067426f),
                new(4.593437f, -1.391986f, -283.441500f),
                new(4.432188f, -1.827000f, -289.874059f),
                new(4.228000f, -2.259000f, -295.464708f),
                new(3.979000f, -2.667000f, -301.208904f),
                new(3.694319f, -3.044558f, -306.658427f),
                new(3.389943f, -3.402089f, -311.747243f),
                new(3.038507f, -3.710000f, -317.739344f),
                new(2.658982f, -3.998366f, -323.449047f),
                new(2.256382f, -4.228069f, -328.612484f),
                new(1.825751f, -4.439820f, -334.924203f),
                new(1.385000f, -4.594000f, -340.919780f),
                new(0.930194f, -4.694385f, -347.035934f),
                new(0.462319f, -4.766201f, -354.143893f),
                new(-0.020632f, -4.795044f, -357.668368f),
                new(-0.485648f, -4.785855f, 357.453210f),
                new(-0.944000f, -4.716000f, 352.881362f),
                new(-1.402989f, -4.591456f, 346.209483f),
                new(-1.834251f, -4.442208f, 341.967259f),
                new(-2.258000f, -4.226000f, 334.038284f),
                new(-2.660000f, -3.994000f, 329.317722f),
                new(-3.035000f, -3.716000f, 322.251708f),
                new(-3.385000f, -3.399000f, 317.155464f),
                new(-3.700000f, -3.048000f, 310.953780f),
                new(-3.987000f, -2.663000f, 306.123136f),
                new(-4.221000f, -2.268000f, 298.972467f),
            },
            new SlidePose
                (-3.310000f, -3.240000f, 135.000364f)
            ),
            ["circle7"] = (
            new SlideTableMetadata()
            {
                // Name = "circle7",
                Const = 0.078f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 3),
                BuildSlideArea(SensorType.A2, 7, 11),
                BuildSlideArea(SensorType.A3, 14, 19),
                BuildSlideArea(SensorType.A4, 23, 27),
                BuildSlideArea(SensorType.A5, 31, 35),
                BuildSlideArea(SensorType.A6, 39, 43),
                BuildSlideArea(SensorType.A7, 45, 47, true, true)
            },
            new SlidePose[]
            {
                new(2.254000f, 4.224000f, -208.395708f),
                new(2.659000f, 4.000000f, -212.194825f),
                new(3.023528f, 3.711283f, -218.362979f),
                new(3.383000f, 3.401000f, -222.744546f),
                new(3.694000f, 3.052000f, -228.946227f),
                new(3.977000f, 2.665000f, -234.852591f),
                new(4.232000f, 2.268000f, -239.447499f),
                new(4.415798f, 1.838709f, -245.721893f),
                new(4.585000f, 1.399000f, -251.327386f),
                new(4.697351f, 0.949199f, -255.515687f),
                new(4.765000f, 0.481000f, -262.627694f),
                new(4.803000f, -0.004000f, -267.956572f),
                new(4.775829f, -0.470666f, -271.969895f),
                new(4.701000f, -0.926801f, -279.067426f),
                new(4.593437f, -1.391986f, -283.441500f),
                new(4.432188f, -1.827000f, -289.874059f),
                new(4.228000f, -2.259000f, -295.464708f),
                new(3.979000f, -2.667000f, -301.208904f),
                new(3.694319f, -3.044558f, -306.658427f),
                new(3.389943f, -3.402089f, -311.747243f),
                new(3.038507f, -3.710000f, -317.739344f),
                new(2.658982f, -3.998366f, -323.449047f),
                new(2.256382f, -4.228069f, -328.612484f),
                new(1.825751f, -4.439820f, -334.924203f),
                new(1.385000f, -4.594000f, -340.919780f),
                new(0.930194f, -4.694385f, -347.035934f),
                new(0.462319f, -4.766201f, -354.143893f),
                new(-0.020632f, -4.795044f, -357.668368f),
                new(-0.485648f, -4.785855f, 357.453210f),
                new(-0.944000f, -4.716000f, 352.881362f),
                new(-1.402989f, -4.591456f, 346.209483f),
                new(-1.834251f, -4.442208f, 341.967259f),
                new(-2.258000f, -4.226000f, 334.038284f),
                new(-2.660000f, -3.994000f, 329.317722f),
                new(-3.035000f, -3.716000f, 322.251708f),
                new(-3.385000f, -3.399000f, 317.155464f),
                new(-3.700000f, -3.048000f, 310.953780f),
                new(-3.987000f, -2.663000f, 306.123136f),
                new(-4.221000f, -2.268000f, 298.972467f),
                new(-4.429000f, -1.836000f, 294.178117f),
                new(-4.586000f, -1.398000f, 288.753741f),
                new(-4.703000f, -0.936000f, 284.384321f),
                new(-4.774000f, -0.474000f, 277.298725f),
                new(-4.797000f, 0.001000f, 271.943431f),
                new(-4.775000f, 0.479000f, 267.930117f),
                new(-4.707000f, 0.931000f, 262.355551f),
                new(-4.591000f, 1.400000f, 256.458508f),
            },
            new SlidePose
                (-4.620000f, 0.070000f, 89.999874f)
            ),
            ["circle8"] = (
            new SlideTableMetadata()
            {
                // Name = "circle8",
                Const = 0.066f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 3),
                BuildSlideArea(SensorType.A2, 7, 11),
                BuildSlideArea(SensorType.A3, 14, 19),
                BuildSlideArea(SensorType.A4, 23, 27),
                BuildSlideArea(SensorType.A5, 31, 35),
                BuildSlideArea(SensorType.A6, 39, 43),
                BuildSlideArea(SensorType.A7, 46, 51),
                BuildSlideArea(SensorType.A8, 53, 55, true, true)
            },
            new SlidePose[]
            {
                new(2.254000f, 4.224000f, -208.395708f),
                new(2.659000f, 4.000000f, -212.194825f),
                new(3.023528f, 3.711283f, -218.362979f),
                new(3.383000f, 3.401000f, -222.744546f),
                new(3.694000f, 3.052000f, -228.946227f),
                new(3.977000f, 2.665000f, -234.852591f),
                new(4.232000f, 2.268000f, -239.447499f),
                new(4.415798f, 1.838709f, -245.721893f),
                new(4.585000f, 1.399000f, -251.327386f),
                new(4.697351f, 0.949199f, -255.515687f),
                new(4.765000f, 0.481000f, -262.627694f),
                new(4.803000f, -0.004000f, -267.956572f),
                new(4.775829f, -0.470666f, -271.969895f),
                new(4.701000f, -0.926801f, -279.067426f),
                new(4.593437f, -1.391986f, -283.441500f),
                new(4.432188f, -1.827000f, -289.874059f),
                new(4.228000f, -2.259000f, -295.464708f),
                new(3.979000f, -2.667000f, -301.208904f),
                new(3.694319f, -3.044558f, -306.658427f),
                new(3.389943f, -3.402089f, -311.747243f),
                new(3.038507f, -3.710000f, -317.739344f),
                new(2.658982f, -3.998366f, -323.449047f),
                new(2.256382f, -4.228069f, -328.612484f),
                new(1.825751f, -4.439820f, -334.924203f),
                new(1.385000f, -4.594000f, -340.919780f),
                new(0.930194f, -4.694385f, -347.035934f),
                new(0.462319f, -4.766201f, -354.143893f),
                new(-0.020632f, -4.795044f, -357.668368f),
                new(-0.485648f, -4.785855f, 357.453210f),
                new(-0.944000f, -4.716000f, 352.881362f),
                new(-1.402989f, -4.591456f, 346.209483f),
                new(-1.834251f, -4.442208f, 341.967259f),
                new(-2.258000f, -4.226000f, 334.038284f),
                new(-2.660000f, -3.994000f, 329.317722f),
                new(-3.035000f, -3.716000f, 322.251708f),
                new(-3.385000f, -3.399000f, 317.155464f),
                new(-3.700000f, -3.048000f, 310.953780f),
                new(-3.987000f, -2.663000f, 306.123136f),
                new(-4.221000f, -2.268000f, 298.972467f),
                new(-4.429000f, -1.836000f, 294.178117f),
                new(-4.586000f, -1.398000f, 288.753741f),
                new(-4.703000f, -0.936000f, 284.384321f),
                new(-4.774000f, -0.474000f, 277.298725f),
                new(-4.797000f, 0.001000f, 271.943431f),
                new(-4.775000f, 0.479000f, 267.930117f),
                new(-4.707000f, 0.931000f, 262.355551f),
                new(-4.591000f, 1.400000f, 256.458508f),
                new(-4.429000f, 1.831000f, 251.083736f),
                new(-4.234000f, 2.252000f, 244.435296f),
                new(-3.986000f, 2.674000f, 238.520882f),
                new(-3.707000f, 3.050000f, 233.241578f),
                new(-3.394000f, 3.403000f, 228.238555f),
                new(-3.048000f, 3.718000f, 222.887699f),
                new(-2.666000f, 3.996000f, 216.450957f),
                new(-2.249000f, 4.232000f, 211.287522f),
            },
            new SlidePose
                (-3.240000f, 3.290000f, 45.000267f)
            ),

            ["line3"] = (
            new SlideTableMetadata()
            {
                // Name = "line3",
                Const = 0.182f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 3),
                BuildSlideArea(SensorType.A2, SensorType.B2, 6, 9, false),
                BuildSlideArea(SensorType.A3, 10, 13, true, true)
            },
            new SlidePose[]
            {
                new(2.019855f, 3.981425f, 112.499918f),
                new(2.205117f, 3.568474f, 112.499918f),
                new(2.379772f, 3.116633f, 112.499918f),
                new(2.560084f, 2.664792f, 112.499918f),
                new(2.745345f, 2.251841f, 112.499918f),
                new(2.920000f, 1.800000f, 112.499918f),
                new(3.108797f, 1.363715f, 112.499918f),
                new(3.294058f, 0.950764f, 112.499918f),
                new(3.468713f, 0.498923f, 112.499918f),
                new(3.643368f, 0.066880f, 112.499918f),
                new(3.828629f, -0.346069f, 112.499918f),
                new(4.003285f, -0.797912f, 112.499918f),
                new(4.178646f, -1.240561f, 112.499918f),
            },
            new SlidePose
                (3.450000f, 0.200000f, 292.646886f)
            ),
            ["line4"] = (
            new SlideTableMetadata()
            {
                // Name = "line4",
                Const = 0.19f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 4),
                BuildSlideArea(SensorType.B2, 6, 9),
                BuildSlideArea(SensorType.B3, 11, 14),
                BuildSlideArea(SensorType.A4, 15, 18, true, true)
            },
            new SlidePose[]
            {
                new(1.836000f, 3.962315f, 90.000000f),
                new(1.836000f, 3.509892f, 90.000000f),
                new(1.836000f, 3.025616f, 90.000000f),
                new(1.836000f, 2.539172f, 90.000000f),
                new(1.836000f, 2.086748f, 90.000000f),
                new(1.836000f, 1.602472f, 90.000000f),
                new(1.836000f, 1.127143f, 90.000000f),
                new(1.836000f, 0.674720f, 90.000000f),
                new(1.836000f, 0.190444f, 90.000000f),
                new(1.836000f, -0.275545f, 90.000000f),
                new(1.836000f, -0.727969f, 90.000000f),
                new(1.836000f, -1.212244f, 90.000000f),
                new(1.836000f, -1.688301f, 90.000000f),
                new(1.836000f, -2.140726f, 90.000000f),
                new(1.836000f, -2.625000f, 90.000000f),
                new(1.836000f, -3.118935f, 90.000000f),
                new(1.836000f, -3.571358f, 90.000000f),
                new(1.836000f, -4.055635f, 90.000000f),
            },
            new SlidePose
                (1.700000f, -2.430000f, 270.000065f)
            ),
            ["line5"] = (
            new SlideTableMetadata()
            {
                // Name = "line5",
                Const = 0.152f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 4),
                BuildSlideArea(SensorType.B1, 5, 7),
                BuildSlideArea(SensorType.C, 10, 12),
                BuildSlideArea(SensorType.B5, 13, 16),
                BuildSlideArea(SensorType.A5, 17, 19, true, true)
            },
            new SlidePose[]
            {
                new(1.657000f, 4.012000f, 67.499991f),
                new(1.496000f, 3.589000f, 67.499991f),
                new(1.300000f, 3.146000f, 67.499991f),
                new(1.108000f, 2.699000f, 67.499991f),
                new(0.947000f, 2.276000f, 67.499991f),
                new(0.751000f, 1.833000f, 67.499991f),
                new(0.576000f, 1.391000f, 67.499991f),
                new(0.415000f, 0.968000f, 67.499991f),
                new(0.219000f, 0.525000f, 67.499991f),
                new(0.037000f, 0.096000f, 67.499991f),
                new(-0.124000f, -0.327000f, 67.499991f),
                new(-0.320000f, -0.770000f, 67.499991f),
                new(-0.509000f, -1.207000f, 67.499991f),
                new(-0.670000f, -1.630000f, 67.499991f),
                new(-0.866000f, -2.073000f, 67.499991f),
                new(-1.051000f, -2.531000f, 67.499991f),
                new(-1.212000f, -2.954000f, 67.499991f),
                new(-1.408000f, -3.397000f, 67.499991f),
                new(-1.583000f, -3.834000f, 67.499991f),
            },
            new SlidePose
                (-1.080000f, -2.300000f, 247.204534f)
            ),
            ["line6"] = (
            new SlideTableMetadata()
            {
                // Name = "line6",
                Const = 0.19f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 4),
                BuildSlideArea(SensorType.B8, 6, 9),
                BuildSlideArea(SensorType.B7, 11, 14),
                BuildSlideArea(SensorType.A6, 15, 18, true, true)
            },
            new SlidePose[]
            {
                new(1.482278f, 4.120779f, 45.073449f),
                new(1.162777f, 3.800458f, 45.073449f),
                new(0.820781f, 3.457584f, 45.073449f),
                new(0.477255f, 3.113177f, 45.073449f),
                new(0.157753f, 2.792853f, 45.073449f),
                new(-0.184243f, 2.449981f, 45.073449f),
                new(-0.519920f, 2.113442f, 45.073449f),
                new(-0.839420f, 1.793122f, 45.073449f),
                new(-1.181416f, 1.450247f, 45.073449f),
                new(-1.510497f, 1.120323f, 45.073449f),
                new(-1.830000f, 0.800000f, 45.073449f),
                new(-2.171996f, 0.457126f, 45.073449f),
                new(-2.508184f, 0.120074f, 45.073449f),
                new(-2.827688f, -0.200250f, 45.073449f),
                new(-3.169682f, -0.543123f, 45.073449f),
                new(-3.518500f, -0.892837f, 45.073449f),
                new(-3.838001f, -1.213158f, 45.073449f),
                new(-4.179996f, -1.556030f, 45.073449f),
            },
            new SlidePose
                (-3.120000f, -0.320000f, 225.000185f)
            ),
            ["line7"] = (
            new SlideTableMetadata()
            {
                // Name = "line7",
                Const = 0.182f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 3),
                BuildSlideArea(SensorType.A8, SensorType.B8, 6, 9, false),
                BuildSlideArea(SensorType.A7, 10, 13, true, true)
            },
            new SlidePose[]
            {
                new(1.376632f, 4.170231f, 22.500074f),
                new(0.963681f, 3.984969f, 22.500074f),
                new(0.511840f, 3.810313f, 22.500074f),
                new(0.060000f, 3.630000f, 22.500074f),
                new(-0.352951f, 3.444738f, 22.500074f),
                new(-0.804791f, 3.270082f, 22.500074f),
                new(-1.241076f, 3.081284f, 22.500074f),
                new(-1.654026f, 2.896022f, 22.500074f),
                new(-2.105868f, 2.721366f, 22.500074f),
                new(-2.537911f, 2.546710f, 22.500074f),
                new(-2.950859f, 2.361448f, 22.500074f),
                new(-3.402701f, 2.186792f, 22.500074f),
                new(-3.845350f, 2.011429f, 22.500074f),
            },
            new SlidePose
                (3.450000f, 0.200000f, 292.646886f)
            ),
            ["v1"] = (
            new SlideTableMetadata()
            {
                // Name = "v1",
                Const = 0.185f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 3),
                BuildSlideArea(SensorType.B1, 4, 7),
                BuildSlideArea(SensorType.C, 8, 13),
                BuildSlideArea(SensorType.B1, 14, 16),
                BuildSlideArea(SensorType.A1, 17, 19, true, true)
            },
            new SlidePose[]
            {
                new(1.657000f, 4.012000f, 67.499991f),
                new(1.496000f, 3.589000f, 67.499991f),
                new(1.300000f, 3.146000f, 67.499991f),
                new(1.108000f, 2.699000f, 67.499991f),
                new(0.947000f, 2.276000f, 67.499991f),
                new(0.751000f, 1.833000f, 67.499991f),
                new(0.576000f, 1.391000f, 67.499991f),
                new(0.415000f, 0.968000f, 67.499991f),
                new(0.219000f, 0.525000f, 67.499991f),
                new(0.037000f, 0.096000f, 67.499991f),
                new(0.112998f, 0.268001f, 247.499966f),
                new(0.273998f, 0.691001f, 247.499966f),
                new(0.469998f, 1.134000f, 247.499966f),
                new(0.661999f, 1.581001f, 247.499966f),
                new(0.822999f, 2.004001f, 247.499966f),
                new(1.018999f, 2.447001f, 247.499966f),
                new(1.193999f, 2.889000f, 247.499966f),
                new(1.354999f, 3.312000f, 247.499966f),
                new(1.551000f, 3.755000f, 247.499966f),
                new(1.733000f, 4.184000f, 247.499966f),
            },
            new SlidePose
                (1.220000f, 2.630000f, 67.499991f)
            ),
            ["v2"] = (
            new SlideTableMetadata()
            {
                // Name = "v2",
                Const = 0.15f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 3),
                BuildSlideArea(SensorType.B1, 4, 7),
                BuildSlideArea(SensorType.C, 8, 13),
                BuildSlideArea(SensorType.B2, 14, 16),
                BuildSlideArea(SensorType.A2, 17, 19, true, true)
            },
            new SlidePose[]
            {
                new(1.657000f, 4.012000f, 67.499991f),
                new(1.496000f, 3.589000f, 67.499991f),
                new(1.300000f, 3.146000f, 67.499991f),
                new(1.108000f, 2.699000f, 67.499991f),
                new(0.947000f, 2.276000f, 67.499991f),
                new(0.751000f, 1.833000f, 67.499991f),
                new(0.576000f, 1.391000f, 67.499991f),
                new(0.415000f, 0.968000f, 67.499991f),
                new(0.219000f, 0.525000f, 67.499991f),
                new(0.037000f, 0.096000f, 67.499991f),
                new(0.317292f, 0.123534f, 202.995329f),
                new(0.767608f, 0.302090f, 202.995329f),
                new(1.208722f, 0.481273f, 202.995329f),
                new(1.620055f, 0.670099f, 202.995329f),
                new(2.070369f, 0.848655f, 202.995329f),
                new(2.523352f, 1.045619f, 202.995329f),
                new(2.934686f, 1.234444f, 202.995329f),
                new(3.385000f, 1.413000f, 202.995329f),
                new(3.816133f, 1.601996f, 202.995329f),
            },
            new SlidePose
                (2.370000f, 0.850000f, 23.028757f)
            ),
            ["v3"] = (
            new SlideTableMetadata()
            {
                // Name = "v3",
                Const = 0.158f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 3),
                BuildSlideArea(SensorType.B1, 4, 7),
                BuildSlideArea(SensorType.C, 8, 13),
                BuildSlideArea(SensorType.B3, 14, 16),
                BuildSlideArea(SensorType.A3, 17, 19, true, true)
            },
            new SlidePose[]
            {
                new(1.657000f, 4.012000f, 67.499991f),
                new(1.496000f, 3.589000f, 67.499991f),
                new(1.300000f, 3.146000f, 67.499991f),
                new(1.108000f, 2.699000f, 67.499991f),
                new(0.947000f, 2.276000f, 67.499991f),
                new(0.751000f, 1.833000f, 67.499991f),
                new(0.576000f, 1.391000f, 67.499991f),
                new(0.415000f, 0.968000f, 67.499991f),
                new(0.219000f, 0.525000f, 67.499991f),
                new(0.037000f, 0.096000f, 67.499991f),
                new(0.343688f, -0.164763f, 157.995508f),
                new(0.788368f, -0.356925f, 157.995508f),
                new(1.226984f, -0.542137f, 157.995508f),
                new(1.651362f, -0.699472f, 157.995508f),
                new(2.096041f, -0.891633f, 157.995508f),
                new(2.555623f, -1.072664f, 157.995508f),
                new(2.980000f, -1.230000f, 157.995508f),
                new(3.424679f, -1.422161f, 157.995508f),
                new(3.863177f, -1.593377f, 157.995508f),
            },
            new SlidePose
                (2.310000f, -1.150000f, 338.631804f)
            ),
            ["v4"] = (
            new SlideTableMetadata()
            {
                // Name = "v4",
                Const = 0.158f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 3),
                BuildSlideArea(SensorType.B1, 4, 7),
                BuildSlideArea(SensorType.C, 8, 13),
                BuildSlideArea(SensorType.B4, 14, 16),
                BuildSlideArea(SensorType.A4, 17, 19, true, true)
            },
            new SlidePose[]
            {
                new(1.657000f, 4.012000f, 67.499991f),
                new(1.496000f, 3.589000f, 67.499991f),
                new(1.300000f, 3.146000f, 67.499991f),
                new(1.108000f, 2.699000f, 67.499991f),
                new(0.947000f, 2.276000f, 67.499991f),
                new(0.751000f, 1.833000f, 67.499991f),
                new(0.576000f, 1.391000f, 67.499991f),
                new(0.415000f, 0.968000f, 67.499991f),
                new(0.219000f, 0.525000f, 67.499991f),
                new(0.037000f, 0.096000f, 67.499991f),
                new(0.191442f, -0.379685f, 112.995644f),
                new(0.370000f, -0.830000f, 112.995644f),
                new(0.549185f, -1.271113f, 112.995644f),
                new(0.738014f, -1.682446f, 112.995644f),
                new(0.916572f, -2.132760f, 112.995644f),
                new(1.113538f, -2.585741f, 112.995644f),
                new(1.302365f, -2.997074f, 112.995644f),
                new(1.480923f, -3.447386f, 112.995644f),
                new(1.669921f, -3.878520f, 112.995644f),
            },
            new SlidePose
                (0.920000f, -2.450000f, 292.716406f)
            ),
            ["v6"] = (
            new SlideTableMetadata()
            {
                // Name = "v6",
                Const = 0.158f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 3),
                BuildSlideArea(SensorType.B1, 4, 7),
                BuildSlideArea(SensorType.C, 8, 13),
                BuildSlideArea(SensorType.B6, 14, 16),
                BuildSlideArea(SensorType.A6, 17, 19, true, true)
            },
            new SlidePose[]
            {
                new(1.657000f, 4.012000f, 67.499991f),
                new(1.496000f, 3.589000f, 67.499991f),
                new(1.300000f, 3.146000f, 67.499991f),
                new(1.108000f, 2.699000f, 67.499991f),
                new(0.947000f, 2.276000f, 67.499991f),
                new(0.751000f, 1.833000f, 67.499991f),
                new(0.576000f, 1.391000f, 67.499991f),
                new(0.415000f, 0.968000f, 67.499991f),
                new(0.219000f, 0.525000f, 67.499991f),
                new(0.037000f, 0.096000f, 67.499991f),
                new(-0.346020f, -0.153444f, 22.994828f),
                new(-0.796337f, -0.331997f, 22.994828f),
                new(-1.237452f, -0.511176f, 22.994828f),
                new(-1.648787f, -0.700000f, 22.994828f),
                new(-2.099103f, -0.878553f, 22.994828f),
                new(-2.552087f, -1.075514f, 22.994828f),
                new(-2.963422f, -1.264336f, 22.994828f),
                new(-3.413737f, -1.442889f, 22.994828f),
                new(-3.844871f, -1.631882f, 22.994828f),
            },
            new SlidePose
                (-2.440000f, -0.920000f, 204.134120f)
            ),
            ["v7"] = (
            new SlideTableMetadata()
            {
                // Name = "v7",
                Const = 0.158f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 3),
                BuildSlideArea(SensorType.B1, 4, 7),
                BuildSlideArea(SensorType.C, 8, 13),
                BuildSlideArea(SensorType.B7, 14, 16),
                BuildSlideArea(SensorType.A7, 17, 19, true, true)
            },
            new SlidePose[]
            {
                new(1.657000f, 4.012000f, 67.499991f),
                new(1.496000f, 3.589000f, 67.499991f),
                new(1.300000f, 3.146000f, 67.499991f),
                new(1.108000f, 2.699000f, 67.499991f),
                new(0.947000f, 2.276000f, 67.499991f),
                new(0.751000f, 1.833000f, 67.499991f),
                new(0.576000f, 1.391000f, 67.499991f),
                new(0.415000f, 0.968000f, 67.499991f),
                new(0.219000f, 0.525000f, 67.499991f),
                new(0.037000f, 0.096000f, 67.499991f),
                new(-0.346020f, 0.152575f, 337.994928f),
                new(-0.790698f, 0.344740f, 337.994928f),
                new(-1.229313f, 0.529957f, 337.994928f),
                new(-1.653690f, 0.687295f, 337.994928f),
                new(-2.098367f, 0.879460f, 337.994928f),
                new(-2.557948f, 1.060496f, 337.994928f),
                new(-2.982323f, 1.217835f, 337.994928f),
                new(-3.427000f, 1.410000f, 337.994928f),
                new(-3.865496f, 1.581219f, 337.994928f),
            },
            new SlidePose
                (-2.300000f, 1.110000f, 158.528193f)
            ),
            ["v8"] = (
            new SlideTableMetadata()
            {
                // Name = "v8",
                Const = 0.185f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 3),
                BuildSlideArea(SensorType.B1, 4, 7),
                BuildSlideArea(SensorType.C, 8, 13),
                BuildSlideArea(SensorType.B8, 14, 16),
                BuildSlideArea(SensorType.A8, 17, 19, true, true)
            },
            new SlidePose[]
            {
                new(1.657000f, 4.012000f, 67.499991f),
                new(1.496000f, 3.589000f, 67.499991f),
                new(1.300000f, 3.146000f, 67.499991f),
                new(1.108000f, 2.699000f, 67.499991f),
                new(0.947000f, 2.276000f, 67.499991f),
                new(0.751000f, 1.833000f, 67.499991f),
                new(0.576000f, 1.391000f, 67.499991f),
                new(0.415000f, 0.968000f, 67.499991f),
                new(0.219000f, 0.525000f, 67.499991f),
                new(0.037000f, 0.096000f, 67.499991f),
                new(-0.121552f, 0.301153f, 292.995068f),
                new(-0.300106f, 0.751469f, 292.995068f),
                new(-0.479287f, 1.192584f, 292.995068f),
                new(-0.668112f, 1.603918f, 292.995068f),
                new(-0.846666f, 2.054233f, 292.995068f),
                new(-1.043628f, 2.507217f, 292.995068f),
                new(-1.232452f, 2.918551f, 292.995068f),
                new(-1.411006f, 3.368866f, 292.995068f),
                new(-1.600000f, 3.800000f, 292.995068f),
            },
            new SlidePose
                (-0.840000f, 2.350000f, 113.213124f)
            ),
            ["ppqq1"] = (
            new SlideTableMetadata()
            {
                // Name = "ppqq1",
                Const = 0.065f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 3),
                BuildSlideArea(SensorType.B1, 5, 7),
                BuildSlideArea(SensorType.C, 10, 13),
                BuildSlideArea(SensorType.B4, 15, 17),
                BuildSlideArea(SensorType.A3, 21, 26),
                BuildSlideArea(SensorType.A2, 29, 32),
                BuildSlideArea(SensorType.A1, 33, 35, true, true)
            },
            new SlidePose[]
            {
                new(1.637000f, 4.003000f, 64.888613f),
                new(1.430000f, 3.566000f, 64.888613f),
                new(1.235000f, 3.146000f, 64.888613f),
                new(1.028000f, 2.714000f, 64.888613f),
                new(0.833000f, 2.294000f, 64.888613f),
                new(0.632000f, 1.862000f, 64.888613f),
                new(0.425000f, 1.436000f, 64.888613f),
                new(0.230000f, 1.010000f, 67.298018f),
                new(0.053000f, 0.592000f, 70.884469f),
                new(-0.034000f, 0.115000f, 81.844648f),
                new(-0.011000f, -0.344000f, 93.601812f),
                new(0.101000f, -0.807000f, 104.539552f),
                new(0.320000f, -1.230000f, 116.546734f),
                new(0.600000f, -1.599000f, 126.933605f),
                new(0.964000f, -1.899000f, 140.576757f),
                new(1.390000f, -2.114000f, 152.971445f),
                new(1.839000f, -2.230000f, 165.935889f),
                new(2.312000f, -2.273000f, 175.633233f),
                new(2.778000f, -2.198000f, 188.772292f),
                new(3.213000f, -2.030000f, 201.426814f),
                new(3.602000f, -1.778000f, 213.403525f),
                new(3.931000f, -1.445000f, 224.649438f),
                new(4.194000f, -1.054000f, 236.253414f),
                new(4.357000f, -0.609000f, 248.386697f),
                new(4.421000f, -0.143000f, 261.242724f),
                new(4.405000f, 0.323000f, 273.089068f),
                new(4.275000f, 0.770000f, 286.900773f),
                new(4.038000f, 1.177000f, 295.343703f),
                new(3.780000f, 1.564000f, 300.345616f),
                new(3.511000f, 1.960000f, 300.345616f),
                new(3.246000f, 2.352000f, 301.882005f),
                new(2.988000f, 2.741000f, 301.882005f),
                new(2.725000f, 3.128000f, 304.001127f),
                new(2.457000f, 3.525000f, 304.001127f),
                new(2.192000f, 3.906000f, 304.001127f),
            },
            new SlidePose
                (3.190000f, 2.620000f, 123.461317f)
            ),
            ["ppqq2"] = (
            new SlideTableMetadata()
            {
                // Name = "ppqq2",
                Const = 0.086f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 3),
                BuildSlideArea(SensorType.B1, 5, 7),
                BuildSlideArea(SensorType.C, 9, 13),
                BuildSlideArea(SensorType.B4, 14, 17),
                BuildSlideArea(SensorType.A3, 20, 25),
                BuildSlideArea(SensorType.A2, 26, 28, true, true),
            },
            new SlidePose[]
            {
                new(1.637000f, 4.003000f, 64.888613f),
                new(1.430000f, 3.566000f, 64.888613f),
                new(1.235000f, 3.146000f, 64.888613f),
                new(1.028000f, 2.714000f, 64.888613f),
                new(0.833000f, 2.294000f, 64.888613f),
                new(0.632000f, 1.862000f, 64.888613f),
                new(0.425000f, 1.436000f, 64.888613f),
                new(0.230000f, 1.010000f, 67.298018f),
                new(0.048000f, 0.595000f, 70.884469f),
                new(-0.030000f, 0.119000f, 81.844648f),
                new(-0.019000f, -0.361000f, 92.088924f),
                new(0.092000f, -0.804000f, 104.539552f),
                new(0.313000f, -1.228000f, 116.546734f),
                new(0.598000f, -1.607000f, 126.933605f),
                new(0.959000f, -1.898000f, 140.576757f),
                new(1.382000f, -2.125000f, 152.971445f),
                new(1.847000f, -2.237000f, 165.935889f),
                new(2.309000f, -2.269000f, 175.633233f),
                new(2.767000f, -2.198000f, 187.617998f),
                new(3.206000f, -2.032000f, 201.426814f),
                new(3.602000f, -1.781000f, 213.403525f),
                new(3.931000f, -1.452000f, 224.649438f),
                new(4.188000f, -1.056000f, 236.253414f),
                new(4.350000f, -0.616000f, 248.386697f),
                new(4.428000f, -0.146000f, 260.698994f),
                new(4.433000f, 0.341000f, 265.672619f),
                new(4.439000f, 0.807000f, 268.803597f),
                new(4.440000f, 1.272000f, 269.327691f),
            },
            new SlidePose
                (4.564000f, -0.344000f, -270.672010f)
            ),
            ["ppqq3"] = (
            new SlideTableMetadata()
            {
                // Name = "ppqq3",
                Const = 0.157f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 3),
                BuildSlideArea(SensorType.B1, 4, 7),
                BuildSlideArea(SensorType.C, 9, 13),
                BuildSlideArea(SensorType.B4, 14, 17),
                BuildSlideArea(SensorType.A3, 19, 22, true, true),
            },
            new SlidePose[]
            {
                new(1.633000f, 4.019000f, 65.776749f),
                new(1.429233f, 3.599262f, 65.776749f),
                new(1.220000f, 3.162356f, 65.776749f),
                new(1.029000f, 2.736000f, 65.776749f),
                new(0.826000f, 2.318000f, 65.776749f),
                new(0.626000f, 1.885000f, 65.776749f),
                new(0.428000f, 1.458000f, 65.776749f),
                new(0.230000f, 1.030000f, 65.776749f),
                new(0.054000f, 0.587000f, 73.541907f),
                new(-0.039000f, 0.137000f, 83.213351f),
                new(-0.016000f, -0.336000f, 94.124731f),
                new(0.091000f, -0.792000f, 104.548407f),
                new(0.296000f, -1.221000f, 117.135416f),
                new(0.594000f, -1.596000f, 130.217383f),
                new(0.961000f, -1.894000f, 141.569868f),
                new(1.369000f, -2.110000f, 151.344926f),
                new(1.820000f, -2.238000f, 165.707089f),
                new(2.286000f, -2.250000f, 178.424267f),
                new(2.765000f, -2.175000f, 183.172660f),
                new(3.216000f, -2.094000f, 186.927120f),
                new(3.688000f, -2.006000f, 188.018929f),
                new(4.146000f, -1.894000f, 191.544684f),
            },
            new SlidePose
                (2.590000f, -2.310000f, 10.680415f)
            ),
            ["ppqq4"] = (
            new SlideTableMetadata()
            {
                // Name = "ppqq4",
                Const = 0.065f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 3),
                BuildSlideArea(SensorType.B1, 5, 7),
                BuildSlideArea(SensorType.C, 9, 13),
                BuildSlideArea(SensorType.B4, 14, 17),
                BuildSlideArea(SensorType.A3, 20, 25),
                BuildSlideArea(SensorType.A2, 28, 33),
                BuildSlideArea(SensorType.B1, 34, 37),
                BuildSlideArea(SensorType.C, 39, 43),
                BuildSlideArea(SensorType.B4, 44, 46),
                BuildSlideArea(SensorType.A4, 47, 49, true, true),
            },
            new SlidePose[]
            {
                new(1.639000f, 4.007000f, 64.999999f),
                new(1.437000f, 3.579000f, 64.999999f),
                new(1.241000f, 3.145000f, 64.999999f),
                new(1.032000f, 2.718000f, 64.999999f),
                new(0.839000f, 2.290000f, 64.999999f),
                new(0.643000f, 1.858000f, 64.999999f),
                new(0.442812f, 1.435000f, 64.999999f),
                new(0.244000f, 1.011000f, 64.440743f),
                new(0.069000f, 0.576000f, 72.205900f),
                new(-0.011000f, 0.117000f, 81.877340f),
                new(0.001000f, -0.349000f, 92.788740f),
                new(0.113000f, -0.822000f, 104.696712f),
                new(0.318000f, -1.251000f, 112.988478f),
                new(0.616000f, -1.611000f, 130.217383f),
                new(0.970000f, -1.896000f, 140.194945f),
                new(1.386000f, -2.120000f, 151.344926f),
                new(1.840000f, -2.238000f, 164.742826f),
                new(2.300000f, -2.281000f, 175.585010f),
                new(2.772000f, -2.212000f, 188.903293f),
                new(3.226000f, -2.031000f, 200.282255f),
                new(3.611000f, -1.776000f, 213.507880f),
                new(3.946000f, -1.446000f, 224.923452f),
                new(4.189000f, -1.061000f, 237.075771f),
                new(4.357000f, -0.620000f, 248.930355f),
                new(4.431000f, -0.135000f, 261.740728f),
                new(4.400000f, 0.325000f, 272.378011f),
                new(4.276000f, 0.785000f, 284.616851f),
                new(4.064000f, 1.189000f, 296.388028f),
                new(3.766000f, 1.549000f, 311.114019f),
                new(3.387000f, 1.853000f, 320.585445f),
                new(2.958000f, 2.058000f, 333.347702f),
                new(2.506000f, 2.163000f, 347.214195f),
                new(2.045000f, 2.182000f, 358.019621f),
                new(1.579000f, 2.102000f, 10.630310f),
                new(1.151000f, 1.920000f, 21.542533f),
                new(0.753000f, 1.636000f, 36.145303f),
                new(0.438000f, 1.304000f, 45.921350f),
                new(0.190000f, 0.917000f, 65.131863f),
                new(0.046000f, 0.462000f, 70.707672f),
                new(-0.014000f, -0.013000f, 84.737410f),
                new(0.022000f, -0.472000f, 94.927635f),
                new(0.150000f, -0.911000f, 107.374394f),
                new(0.350000f, -1.338000f, 111.745419f),
                new(0.566000f, -1.765000f, 111.745419f),
                new(0.778000f, -2.204000f, 112.657043f),
                new(0.966000f, -2.631000f, 112.657043f),
                new(1.178000f, -3.042000f, 116.499785f),
                new(1.374000f, -3.457000f, 116.499785f),
                new(1.578000f, -3.900000f, 116.499785f),
            },
            new SlidePose
                (0.790000f, -2.470000f, 293.878648f)
            ),
            ["ppqq5"] = (
            new SlideTableMetadata()
            {
                // Name = "ppqq5",
                Const = 0.065f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 3),
                BuildSlideArea(SensorType.B1, 5, 7),
                BuildSlideArea(SensorType.C, 9, 13),
                BuildSlideArea(SensorType.B4, 14, 17),
                BuildSlideArea(SensorType.A3, 20, 25),
                BuildSlideArea(SensorType.A2, 28, 33),
                BuildSlideArea(SensorType.B1, 34, 37),
                BuildSlideArea(SensorType.C, 39, 43),
                BuildSlideArea(SensorType.B5, 44, 46),
                BuildSlideArea(SensorType.A5, 47, 49, true, true),
            },
            new SlidePose[]
            {
                new(1.631000f, 4.005000f, 64.440743f),
                new(1.435000f, 3.597000f, 64.440743f),
                new(1.228291f, 3.159287f, 64.440743f),
                new(1.012709f, 2.723172f, 64.440743f),
                new(0.808000f, 2.319000f, 64.440743f),
                new(0.610000f, 1.877459f, 64.440743f),
                new(0.406000f, 1.455000f, 64.440743f),
                new(0.212848f, 1.042096f, 64.440743f),
                new(0.037848f, 0.607096f, 72.205900f),
                new(-0.051000f, 0.135000f, 81.877340f),
                new(-0.027000f, -0.349000f, 92.788740f),
                new(0.088000f, -0.801000f, 104.696712f),
                new(0.298000f, -1.209000f, 115.667658f),
                new(0.579000f, -1.587000f, 128.293688f),
                new(0.958000f, -1.884000f, 140.194945f),
                new(1.374000f, -2.114000f, 151.344926f),
                new(1.825000f, -2.239000f, 164.742826f),
                new(2.295000f, -2.278000f, 175.585010f),
                new(2.762000f, -2.201000f, 188.055265f),
                new(3.196000f, -2.041000f, 200.282255f),
                new(3.596000f, -1.780000f, 212.131976f),
                new(3.934000f, -1.448000f, 224.923452f),
                new(4.189000f, -1.061000f, 237.075771f),
                new(4.357000f, -0.620000f, 248.930355f),
                new(4.431000f, -0.135000f, 261.740728f),
                new(4.400000f, 0.325000f, 272.378011f),
                new(4.276000f, 0.785000f, 284.616851f),
                new(4.064000f, 1.189000f, 296.388028f),
                new(3.766000f, 1.549000f, 311.114019f),
                new(3.416000f, 1.853000f, 320.585445f),
                new(2.994000f, 2.058000f, 333.347702f),
                new(2.557000f, 2.171000f, 347.214195f),
                new(2.087000f, 2.192000f, 358.019621f),
                new(1.615000f, 2.126000f, 10.630310f),
                new(1.165000f, 1.946000f, 21.542533f),
                new(0.771000f, 1.669000f, 36.145303f),
                new(0.445000f, 1.355000f, 45.921350f),
                new(0.204000f, 0.939000f, 56.258354f),
                new(0.024000f, 0.520000f, 63.472250f),
                new(-0.150000f, 0.071000f, 66.995155f),
                new(-0.309000f, -0.363000f, 66.995155f),
                new(-0.475000f, -0.819000f, 67.570271f),
                new(-0.649000f, -1.260000f, 67.570271f),
                new(-0.815000f, -1.694000f, 67.570271f),
                new(-0.967000f, -2.150000f, 67.570271f),
                new(-1.141000f, -2.562000f, 67.570271f),
                new(-1.307000f, -3.011000f, 68.591903f),
                new(-1.481000f, -3.445000f, 68.591903f),
                new(-1.633000f, -3.908000f, 69.340511f),
            },
            new SlidePose
                (-1.210000f, -2.360000f, 249.891428f)
            ),
            ["ppqq6"] = (
            new SlideTableMetadata()
            {
                // Name = "ppqq6",
                Const = 0.067f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 3),
                BuildSlideArea(SensorType.B1, 5, 7),
                BuildSlideArea(SensorType.C, 9, 13),
                BuildSlideArea(SensorType.B4, 14, 17),
                BuildSlideArea(SensorType.A3, 20, 25),
                BuildSlideArea(SensorType.A2, 28, 33),
                BuildSlideArea(SensorType.B1, 34, 37),
                BuildSlideArea(SensorType.C, SensorType.B8, 38, 40),
                BuildSlideArea(SensorType.B7, SensorType.B6, 42, 44),
                BuildSlideArea(SensorType.A6, 46, 48, true, true),
            },
            new SlidePose[]
            {
                new(1.641637f, 3.995634f, 64.440743f),
                new(1.437000f, 3.590000f, 64.440743f),
                new(1.238928f, 3.149921f, 64.440743f),
                new(1.023346f, 2.713806f, 64.440743f),
                new(0.827000f, 2.303000f, 64.440743f),
                new(0.620637f, 1.868092f, 64.440743f),
                new(0.422297f, 1.436062f, 64.440743f),
                new(0.223486f, 1.032730f, 64.440743f),
                new(0.048486f, 0.597729f, 72.205900f),
                new(-0.031514f, 0.138729f, 81.877340f),
                new(-0.019514f, -0.327271f, 92.788740f),
                new(0.092486f, -0.800271f, 104.696712f),
                new(0.297486f, -1.229271f, 112.988478f),
                new(0.595486f, -1.589271f, 130.217383f),
                new(0.958000f, -1.880000f, 140.194945f),
                new(1.368000f, -2.101000f, 151.344926f),
                new(1.828000f, -2.222000f, 164.742826f),
                new(2.293000f, -2.265000f, 175.585010f),
                new(2.751486f, -2.190270f, 188.903293f),
                new(3.211000f, -2.015000f, 200.282255f),
                new(3.604000f, -1.760000f, 213.507880f),
                new(3.934000f, -1.430000f, 224.923452f),
                new(4.179000f, -1.039270f, 237.075771f),
                new(4.350000f, -0.593000f, 248.930355f),
                new(4.425000f, -0.125000f, 262.748899f),
                new(4.390000f, 0.339000f, 274.818449f),
                new(4.278000f, 0.795000f, 284.616851f),
                new(4.060000f, 1.205000f, 297.263804f),
                new(3.758000f, 1.567000f, 311.114019f),
                new(3.385000f, 1.868000f, 320.585445f),
                new(2.950000f, 2.073000f, 333.347702f),
                new(2.509000f, 2.185000f, 347.214195f),
                new(2.030486f, 2.184730f, 358.019621f),
                new(1.589000f, 2.101000f, 9.465000f),
                new(1.155000f, 1.912000f, 19.971996f),
                new(0.748179f, 1.645908f, 31.148343f),
                new(0.364000f, 1.385000f, 34.171470f),
                new(-0.026000f, 1.136000f, 34.171470f),
                new(-0.422000f, 0.878000f, 34.171470f),
                new(-0.819000f, 0.600000f, 34.171470f),
                new(-1.203000f, 0.348000f, 34.171470f),
                new(-1.594000f, 0.080000f, 34.171470f),
                new(-1.979000f, -0.178000f, 34.171470f),
                new(-2.374000f, -0.453000f, 34.171470f),
                new(-2.765000f, -0.714000f, 34.171470f),
                new(-3.160000f, -0.979000f, 34.171470f),
                new(-3.548000f, -1.235000f, 34.171470f),
                new(-3.937000f, -1.506000f, 34.171470f),
            },
            new SlidePose
                (-2.657000f, -0.513000f, 214.170997f)
            ),
            ["ppqq7"] = (
            new SlideTableMetadata()
            {
                // Name = "ppqq7",
                Const = 0.079f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 3),
                BuildSlideArea(SensorType.B1, 5, 7),
                BuildSlideArea(SensorType.C, 10, 13),
                BuildSlideArea(SensorType.B4, 15, 17),
                BuildSlideArea(SensorType.A3, 21, 26),
                BuildSlideArea(SensorType.A2, 29, 32),
                BuildSlideArea(SensorType.A1, 33, 35, true, true)
            },
            new SlidePose[]
            {
                new(1.641637f, 3.995634f, 64.440743f),
                new(1.434000f, 3.581829f, 64.440743f),
                new(1.238928f, 3.149921f, 64.440743f),
                new(1.023346f, 2.713806f, 64.440743f),
                new(0.840000f, 2.300000f, 64.440743f),
                new(0.620637f, 1.868092f, 64.440743f),
                new(0.422297f, 1.436062f, 64.440743f),
                new(0.223486f, 1.032730f, 64.440743f),
                new(0.048486f, 0.597729f, 72.205900f),
                new(-0.031514f, 0.138729f, 81.877340f),
                new(-0.019514f, -0.327271f, 92.788740f),
                new(0.092486f, -0.800271f, 104.696712f),
                new(0.297486f, -1.229271f, 112.988478f),
                new(0.595486f, -1.589271f, 130.217383f),
                new(0.949486f, -1.874271f, 140.194945f),
                new(1.365486f, -2.098270f, 151.344926f),
                new(1.819486f, -2.216270f, 164.742826f),
                new(2.279486f, -2.259270f, 175.585010f),
                new(2.751486f, -2.190270f, 188.903293f),
                new(3.205486f, -2.009270f, 200.282255f),
                new(3.590486f, -1.754271f, 213.507880f),
                new(3.925486f, -1.424270f, 224.923452f),
                new(4.187000f, -1.045000f, 237.075771f),
                new(4.362000f, -0.601000f, 248.930355f),
                new(4.427000f, -0.136000f, 261.740728f),
                new(4.390000f, 0.341000f, 274.740883f),
                new(4.270000f, 0.786000f, 284.616851f),
                new(4.058000f, 1.213000f, 297.118673f),
                new(3.754000f, 1.561000f, 309.935047f),
                new(3.395000f, 1.864000f, 320.585445f),
                new(2.966000f, 2.065000f, 333.347702f),
                new(2.515000f, 2.181000f, 345.790498f),
                new(2.039000f, 2.187000f, 355.203247f),
                new(1.577000f, 2.173000f, 0.000000f),
                new(1.098000f, 2.143000f, 1.010935f),
                new(0.637000f, 2.131000f, 3.023128f),
                new(0.163000f, 2.081000f, 3.023128f),
                new(-0.303000f, 2.071000f, 3.023128f),
                new(-0.777000f, 2.042000f, 3.023128f),
                new(-1.246000f, 2.019000f, 3.023128f),
                new(-1.720000f, 1.994000f, 3.023128f),
                new(-2.192000f, 1.965000f, 3.023128f),
                new(-2.659000f, 1.944000f, 3.023128f),
                new(-3.135000f, 1.917000f, 3.023128f),
                new(-3.596000f, 1.888000f, 3.023128f),
                new(-4.070000f, 1.865000f, 3.023128f),
            },
            new SlidePose
                (-2.472000f, 2.055000f, 182.645819f)
            ),
            ["ppqq8"] = (
            new SlideTableMetadata()
            {
                // Name = "ppqq8",
                Const = 0.0626f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 3),
                BuildSlideArea(SensorType.B1, 5, 7),
                BuildSlideArea(SensorType.C, 9, 13),
                BuildSlideArea(SensorType.B4, 14, 17),
                BuildSlideArea(SensorType.A3, 20, 25),
                BuildSlideArea(SensorType.A2, 28, 33),
                BuildSlideArea(SensorType.B1, SensorType.A1, 35, 37),
                BuildSlideArea(SensorType.A8, 38, 41, true, true),
            },
            new SlidePose[]
            {
                new(1.630000f, 4.004000f, 64.440743f),
                new(1.431000f, 3.586000f, 64.440743f),
                new(1.238928f, 3.149921f, 64.440743f),
                new(1.023346f, 2.713806f, 64.440743f),
                new(0.840000f, 2.300000f, 64.440743f),
                new(0.620637f, 1.868092f, 64.440743f),
                new(0.422297f, 1.436062f, 64.440743f),
                new(0.223486f, 1.032730f, 64.440743f),
                new(0.048486f, 0.597729f, 72.205900f),
                new(-0.031514f, 0.138729f, 81.877340f),
                new(-0.019514f, -0.327271f, 92.788740f),
                new(0.092486f, -0.800271f, 104.696712f),
                new(0.297486f, -1.229271f, 112.988478f),
                new(0.595486f, -1.589271f, 130.217383f),
                new(0.949486f, -1.874271f, 140.194945f),
                new(1.371000f, -2.102000f, 151.344926f),
                new(1.823000f, -2.224000f, 164.742826f),
                new(2.287000f, -2.259270f, 175.585010f),
                new(2.751486f, -2.190270f, 188.903293f),
                new(3.207000f, -2.021000f, 200.282255f),
                new(3.602000f, -1.766000f, 213.507880f),
                new(3.935000f, -1.438000f, 224.923452f),
                new(4.182000f, -1.047000f, 237.075771f),
                new(4.352000f, -0.602000f, 248.930355f),
                new(4.420000f, -0.135000f, 262.332961f),
                new(4.387000f, 0.337000f, 274.091142f),
                new(4.257000f, 0.791000f, 285.728630f),
                new(4.038000f, 1.195000f, 296.388028f),
                new(3.733000f, 1.549000f, 311.114019f),
                new(3.360000f, 1.849000f, 320.585445f),
                new(2.950000f, 2.073000f, 327.656352f),
                new(2.540000f, 2.272000f, 332.214222f),
                new(2.109091f, 2.486689f, 334.049055f),
                new(1.679401f, 2.689180f, 334.049055f),
                new(1.266331f, 2.882470f, 334.049055f),
                new(0.830000f, 3.100000f, 334.049055f),
                new(0.412000f, 3.324000f, 334.049055f),
                new(-0.004000f, 3.528000f, 334.049055f),
                new(-0.424000f, 3.733000f, 334.049055f),
                new(-0.846000f, 3.941000f, 334.049055f),
                new(-1.277606f, 4.155066f, 334.049055f),
            },
            new SlidePose
                (0.222000f, 3.547000f, 154.018168f)
            ),
            ["L2"] = (
            new SlideTableMetadata()
            {
                // Name = "L2",
                Const = 0.1f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 3),
                BuildSlideArea(SensorType.B8, SensorType.A8, 6, 10, false),
                BuildSlideArea(SensorType.A7, 12, 19),
                BuildSlideArea(SensorType.B8, 21, 24),
                BuildSlideArea(SensorType.B1, 25, 28),
                BuildSlideArea(SensorType.A2, 29, 32, true, true),
            },
            new SlidePose[]
            {
                new(1.389000f, 4.254000f, 22.500074f),
                new(0.976050f, 4.068738f, 22.500074f),
                new(0.524208f, 3.894082f, 22.500074f),
                new(0.072368f, 3.713769f, 22.500074f),
                new(-0.340583f, 3.528507f, 22.500074f),
                new(-0.792423f, 3.353851f, 22.500074f),
                new(-1.228708f, 3.165053f, 22.500074f),
                new(-1.641658f, 2.979791f, 22.500074f),
                new(-2.093500f, 2.805135f, 22.500074f),
                new(-2.525542f, 2.630479f, 22.500074f),
                new(-2.938491f, 2.445217f, 22.500074f),
                new(-3.390333f, 2.270561f, 22.500074f),
                new(-3.832982f, 2.095198f, 22.500074f),
                new(-4.253632f, 1.916769f, 22.500074f),
                new(-4.149000f, 1.845000f, -0.000000f),
                new(-3.696578f, 1.845000f, -0.000000f),
                new(-3.212301f, 1.845000f, -0.000000f),
                new(-2.725857f, 1.845000f, -0.000000f),
                new(-2.273433f, 1.845000f, -0.000000f),
                new(-1.789157f, 1.845000f, -0.000000f),
                new(-1.313828f, 1.845000f, -0.000000f),
                new(-0.861405f, 1.845000f, -0.000000f),
                new(-0.377129f, 1.845000f, -0.000000f),
                new(0.088860f, 1.845000f, -0.000000f),
                new(0.541284f, 1.845000f, -0.000000f),
                new(1.025559f, 1.845000f, -0.000000f),
                new(1.501616f, 1.844999f, -0.000000f),
                new(1.954041f, 1.844999f, -0.000000f),
                new(2.438315f, 1.845000f, -0.000000f),
                new(2.932251f, 1.844999f, -0.000000f),
                new(3.384673f, 1.844999f, -0.000000f),
                new(3.868950f, 1.844999f, -0.000000f),
            },
            new SlidePose
                (2.250767f, 1.728999f, -0.000000f)
            ),
            ["L3"] = (
            new SlideTableMetadata()
            {
                // Name = "L3",
                Const = 0.104f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 3),
                BuildSlideArea(SensorType.B8, SensorType.A8, 6, 10, false),
                BuildSlideArea(SensorType.A7, 12, 18),
                BuildSlideArea(SensorType.B7, 20, 22),
                BuildSlideArea(SensorType.C, 25, 27),
                BuildSlideArea(SensorType.B3, 28, 31),
                BuildSlideArea(SensorType.A3, 32, 34, true, true),
            },
            new SlidePose[]
            {
                new(1.405000f, 4.259000f, 22.500074f),
                new(0.992049f, 4.073738f, 22.500074f),
                new(0.540208f, 3.899081f, 22.500074f),
                new(0.088368f, 3.718769f, 22.500074f),
                new(-0.324583f, 3.533506f, 22.500074f),
                new(-0.776423f, 3.358850f, 22.500074f),
                new(-1.212708f, 3.170053f, 22.500074f),
                new(-1.625658f, 2.984790f, 22.500074f),
                new(-2.077500f, 2.810134f, 22.500074f),
                new(-2.509542f, 2.635479f, 22.500074f),
                new(-2.922491f, 2.450217f, 22.500074f),
                new(-3.374333f, 2.275560f, 22.500074f),
                new(-3.816982f, 2.100198f, 22.500074f),
                new(-4.252000f, 1.927000f, 22.500074f),
                new(-4.162000f, 1.736000f, 157.499992f),
                new(-3.749000f, 1.559000f, 157.499992f),
                new(-3.326000f, 1.398000f, 157.499992f),
                new(-2.882999f, 1.202000f, 157.499992f),
                new(-2.435999f, 1.010000f, 157.499992f),
                new(-2.012999f, 0.849000f, 157.499992f),
                new(-1.569999f, 0.653000f, 157.499992f),
                new(-1.127999f, 0.478000f, 157.499992f),
                new(-0.704999f, 0.317001f, 157.499992f),
                new(-0.261999f, 0.121000f, 157.499992f),
                new(0.167001f, -0.061000f, 157.499992f),
                new(0.590001f, -0.222000f, 157.499992f),
                new(1.033001f, -0.418000f, 157.499992f),
                new(1.470001f, -0.607000f, 157.499992f),
                new(1.893001f, -0.768000f, 157.499992f),
                new(2.336001f, -0.964000f, 157.499992f),
                new(2.794001f, -1.148999f, 157.499992f),
                new(3.217001f, -1.309999f, 157.499992f),
                new(3.660002f, -1.505999f, 157.499992f),
                new(4.097002f, -1.680999f, 157.499992f),
            },
            new SlidePose
                (2.543001f, -1.188000f, 337.287404f)
            ),
            ["L4"] = (
            new SlideTableMetadata()
            {
                // Name = "L4",
                Const = 0.098f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 3),
                BuildSlideArea(SensorType.B8, SensorType.A8, 6, 10, false),
                BuildSlideArea(SensorType.A7, 12, 19),
                BuildSlideArea(SensorType.B6, 21, 24),
                BuildSlideArea(SensorType.B5, 25, 28),
                BuildSlideArea(SensorType.A4, 29, 32, true, true),
            },
            new SlidePose[]
            {
                new(1.392000f, 4.260000f, 22.500074f),
                new(0.979049f, 4.074738f, 22.500074f),
                new(0.527208f, 3.900082f, 22.500074f),
                new(0.075368f, 3.719769f, 22.500074f),
                new(-0.337583f, 3.534507f, 22.500074f),
                new(-0.789423f, 3.359851f, 22.500074f),
                new(-1.225708f, 3.171053f, 22.500074f),
                new(-1.638658f, 2.985791f, 22.500074f),
                new(-2.090500f, 2.811135f, 22.500074f),
                new(-2.522543f, 2.636479f, 22.500074f),
                new(-2.935491f, 2.451217f, 22.500074f),
                new(-3.387333f, 2.276561f, 22.500074f),
                new(-3.829982f, 2.101198f, 22.500074f),
                new(-4.248000f, 1.932000f, 22.500074f),
                new(-4.255490f, 1.650000f, 135.135608f),
                new(-3.920386f, 1.300181f, 135.135608f),
                new(-3.580062f, 0.952539f, 135.135608f),
                new(-3.250141f, 0.642700f, 135.135608f),
                new(-2.915038f, 0.292883f, 135.135608f),
                new(-2.572870f, -0.037132f, 135.135608f),
                new(-2.242947f, -0.346972f, 135.135608f),
                new(-1.907845f, -0.696788f, 135.135608f),
                new(-1.580364f, -1.028331f, 135.135608f),
                new(-1.250439f, -1.338171f, 135.135608f),
                new(-0.915337f, -1.687989f, 135.135608f),
                new(-0.583121f, -2.029047f, 135.135608f),
                new(-0.253197f, -2.338887f, 135.135608f),
                new(0.081905f, -2.688704f, 135.135608f),
                new(0.435063f, -3.034054f, 135.135608f),
                new(0.764987f, -3.343892f, 135.135608f),
                new(1.100089f, -3.693703f, 135.135608f),
                new(1.437634f, -4.021820f, 135.135608f),
            },
            new SlidePose
                (0.219028f, -2.970532f, 314.755154f)
            ),
            ["L5"] = (
            new SlideTableMetadata()
            {
                // Name = "L5",
                Const = 0.105f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 3),
                BuildSlideArea(SensorType.B8, SensorType.A8, 6, 10, false),
                BuildSlideArea(SensorType.A7, 12, 18),
                BuildSlideArea(SensorType.B6, SensorType.A6, 21, 24, false),
                BuildSlideArea(SensorType.A5, 27, 28, true, true),
            },
            new SlidePose[]
            {
                new(1.392000f, 4.259000f, 22.500074f),
                new(0.979049f, 4.073738f, 22.500074f),
                new(0.527208f, 3.899081f, 22.500074f),
                new(0.075368f, 3.718769f, 22.500074f),
                new(-0.337583f, 3.533506f, 22.500074f),
                new(-0.789423f, 3.358850f, 22.500074f),
                new(-1.225708f, 3.170053f, 22.500074f),
                new(-1.638658f, 2.984790f, 22.500074f),
                new(-2.090500f, 2.810134f, 22.500074f),
                new(-2.522543f, 2.635479f, 22.500074f),
                new(-2.935491f, 2.450217f, 22.500074f),
                new(-3.387333f, 2.275560f, 22.500074f),
                new(-3.829982f, 2.100198f, 22.500074f),
                new(-4.252000f, 1.919000f, 22.500074f),
                new(-4.307262f, 1.609354f, 112.217994f),
                new(-4.134832f, 1.156658f, 112.217994f),
                new(-3.948183f, 0.719450f, 112.217994f),
                new(-3.764955f, 0.305592f, 112.217994f),
                new(-3.592526f, -0.147103f, 112.217994f),
                new(-3.420000f, -0.580000f, 112.217994f),
                new(-3.236773f, -0.993857f, 112.217994f),
                new(-3.064344f, -1.446553f, 112.217994f),
                new(-2.891161f, -1.890059f, 112.217994f),
                new(-2.707934f, -2.303916f, 112.217994f),
                new(-2.535505f, -2.756612f, 112.217994f),
                new(-2.344705f, -3.212224f, 112.217994f),
                new(-2.161477f, -3.626081f, 112.217994f),
                new(-1.989045f, -4.078771f, 112.217994f),
            },
            new SlidePose
                (-2.710768f, -2.611632f, 291.712337f)
            ),
            ["s"] = (
            new SlideTableMetadata()
            {
                // Name = "s",
                Const = 0.13f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 4),
                BuildSlideArea(SensorType.B8, 7, 9),
                BuildSlideArea(SensorType.B7, 10, 12),
                BuildSlideArea(SensorType.C, 14, 17),
                BuildSlideArea(SensorType.B3, 19, 21),
                BuildSlideArea(SensorType.B4, 22, 25),
                BuildSlideArea(SensorType.A5, 27, 30, true, true),
            },
            new SlidePose[]
            {
                new(1.508000f, 4.102000f, 44.999915f),
                new(1.198000f, 3.789000f, 44.999915f),
                new(0.877000f, 3.478000f, 44.999915f),
                new(0.569000f, 3.156000f, 44.999915f),
                new(0.238000f, 2.838000f, 44.999915f),
                new(-0.085000f, 2.512000f, 44.999915f),
                new(-0.397000f, 2.204000f, 44.999915f),
                new(-0.707000f, 1.886000f, 44.999915f),
                new(-1.022000f, 1.582000f, 44.999915f),
                new(-1.336000f, 1.264000f, 44.999915f),
                new(-1.642000f, 0.955000f, 44.999915f),
                new(-1.422751f, 0.587173f, 157.642426f),
                new(-0.983141f, 0.408655f, 157.642426f),
                new(-0.552914f, 0.224056f, 157.642426f),
                new(-0.111000f, 0.053000f, 157.642426f),
                new(0.321690f, -0.134906f, 157.642426f),
                new(0.761298f, -0.313424f, 157.642426f),
                new(1.207000f, -0.498000f, 157.642426f),
                new(1.636000f, -0.666000f, 157.642426f),
                new(1.660000f, -0.933000f, 44.999915f),
                new(1.326000f, -1.270000f, 44.999915f),
                new(0.990000f, -1.596000f, 44.999915f),
                new(0.662000f, -1.938000f, 44.999915f),
                new(0.322000f, -2.265000f, 44.999915f),
                new(-0.012000f, -2.602000f, 44.999915f),
                new(-0.348000f, -2.928000f, 44.999915f),
                new(-0.676000f, -3.270000f, 44.999915f),
                new(-1.013000f, -3.596000f, 44.999915f),
                new(-1.342000f, -3.929000f, 44.999915f),
                new(-1.676000f, -4.266000f, 44.999915f),
            },
            new SlidePose
                (-0.610000f, -3.030000f, 225.612390f)
            ),
            ["pq1"] = (
            new SlideTableMetadata()
            {
                // Name = "pq1",
                Const = 0.095f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 4),
                BuildSlideArea(SensorType.B8, 5, 8),
                BuildSlideArea(SensorType.B7, 9, 11),
                BuildSlideArea(SensorType.B6, 12, 14),
                BuildSlideArea(SensorType.B5, 15, 17),
                BuildSlideArea(SensorType.B4, 19, 21),
                BuildSlideArea(SensorType.B3, 22, 24),
                BuildSlideArea(SensorType.B2, 25, 29),
                BuildSlideArea(SensorType.A1, 30, 33, true, true),
            },
            new SlidePose[]
            {
                new(1.503000f, 4.103000f, 45.000130f),
                new(1.174000f, 3.768000f, 45.000130f),
                new(0.840655f, 3.440653f, 45.000130f),
                new(0.496689f, 3.096684f, 45.000130f),
                new(0.176778f, 2.776772f, 45.000130f),
                new(-0.165656f, 2.434336f, 45.000130f),
                new(-0.501764f, 2.098226f, 45.000130f),
                new(-0.830670f, 1.756592f, 45.000130f),
                new(-1.159473f, 1.440514f, 45.000130f),
                new(-1.472014f, 1.086960f, 52.615428f),
                new(-1.707479f, 0.664817f, 63.784038f),
                new(-1.829100f, 0.217925f, 76.539282f),
                new(-1.820614f, -0.253007f, 91.102763f),
                new(-1.690505f, -0.699899f, 107.027023f),
                new(-1.467059f, -1.118506f, 119.928173f),
                new(-1.126233f, -1.450845f, 135.221024f),
                new(-0.732374f, -1.690554f, 148.867804f),
                new(-0.289725f, -1.816417f, 164.980169f),
                new(0.184744f, -1.836923f, 178.895433f),
                new(0.639413f, -1.723077f, 192.327918f),
                new(1.070000f, -1.491000f, 208.436500f),
                new(1.410159f, -1.180018f, 224.118299f),
                new(1.659000f, -0.773000f, 237.773363f),
                new(1.797000f, -0.344000f, 253.584752f),
                new(1.841000f, 0.120000f, 259.748498f),
                new(1.831590f, 0.600478f, 264.976774f),
                new(1.830532f, 1.052033f, 269.688847f),
                new(1.833172f, 1.538470f, 269.688847f),
                new(1.835629f, 1.990887f, 269.688847f),
                new(1.838259f, 2.475156f, 269.688847f),
                new(1.840839f, 2.950478f, 269.688847f),
                new(1.834412f, 3.424663f, 269.688847f),
                new(1.845889f, 3.880608f, 269.688847f),
            },
            new SlidePose
                (1.974722f, 2.261541f, 89.999817f)
            ),
            ["pq2"] = (
            new SlideTableMetadata()
            {
                // Name = "pq2",
                Const = 0.112f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 4),
                BuildSlideArea(SensorType.B8, 5, 8),
                BuildSlideArea(SensorType.B7, 9, 11),
                BuildSlideArea(SensorType.B6, 12, 14),
                BuildSlideArea(SensorType.B5, 16, 18),
                BuildSlideArea(SensorType.B4, 19, 21),
                BuildSlideArea(SensorType.B3, 22, 26),
                BuildSlideArea(SensorType.A2, 27, 30, true, true),
            },
            new SlidePose[]
            {
                new(1.509000f, 4.107000f, 45.000130f),
                new(1.176000f, 3.781000f, 45.000130f),
                new(0.846655f, 3.444652f, 45.000130f),
                new(0.502688f, 3.100684f, 45.000130f),
                new(0.182778f, 2.780771f, 45.000130f),
                new(-0.159656f, 2.438335f, 45.000130f),
                new(-0.495764f, 2.102226f, 45.000130f),
                new(-0.824670f, 1.760592f, 45.000130f),
                new(-1.153473f, 1.444514f, 45.000130f),
                new(-1.466014f, 1.090960f, 52.615428f),
                new(-1.701479f, 0.668816f, 63.784038f),
                new(-1.823100f, 0.221925f, 76.539282f),
                new(-1.814614f, -0.249008f, 91.102763f),
                new(-1.684505f, -0.695899f, 107.027023f),
                new(-1.461059f, -1.114506f, 119.928173f),
                new(-1.120233f, -1.446846f, 135.221024f),
                new(-0.726374f, -1.686554f, 148.867804f),
                new(-0.283725f, -1.812418f, 164.980169f),
                new(0.190744f, -1.832923f, 178.895433f),
                new(0.645413f, -1.719078f, 192.327918f),
                new(1.063000f, -1.506000f, 205.936500f),
                new(1.416159f, -1.176018f, 215.949200f),
                new(1.750000f, -0.849000f, 220.476284f),
                new(2.082000f, -0.528000f, 221.524027f),
                new(2.413000f, -0.192000f, 223.115651f),
                new(2.746722f, 0.137540f, 221.524027f),
                new(3.085722f, 0.482540f, 224.405021f),
                new(3.419723f, 0.816540f, 224.405021f),
                new(3.745000f, 1.140000f, 224.405021f),
                new(4.086722f, 1.489540f, 224.290915f),
            },
            new SlidePose
                (3.028000f, 0.252000f, 44.999916f)
            ),
            ["pq3"] = (
            new SlideTableMetadata()
            {
                // Name = "pq3",
                Const = 0.125f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 4),
                BuildSlideArea(SensorType.B8, 5, 8),
                BuildSlideArea(SensorType.B7, 9, 11),
                BuildSlideArea(SensorType.B6, 12, 14),
                BuildSlideArea(SensorType.B5, 16, 18),
                BuildSlideArea(SensorType.B4, 20, 23),
                BuildSlideArea(SensorType.A3, 25, 27, true, true),
            },
            new SlidePose[]
            {
                new(1.506000f, 4.098000f, 45.000130f),
                new(1.178000f, 3.772000f, 45.000130f),
                new(0.843655f, 3.435653f, 45.000130f),
                new(0.499689f, 3.091684f, 45.000130f),
                new(0.179778f, 2.771771f, 45.000130f),
                new(-0.162656f, 2.429336f, 45.000130f),
                new(-0.498764f, 2.093226f, 45.000130f),
                new(-0.827670f, 1.751592f, 45.000130f),
                new(-1.156473f, 1.435514f, 45.000130f),
                new(-1.479000f, 1.086000f, 52.615428f),
                new(-1.714000f, 0.671000f, 64.627289f),
                new(-1.826100f, 0.212925f, 76.539282f),
                new(-1.817614f, -0.258008f, 91.102763f),
                new(-1.687505f, -0.704899f, 107.027023f),
                new(-1.464059f, -1.123506f, 119.928173f),
                new(-1.133000f, -1.452000f, 135.221024f),
                new(-0.729374f, -1.695554f, 148.867804f),
                new(-0.286725f, -1.821417f, 164.980169f),
                new(0.187744f, -1.841923f, 172.498610f),
                new(0.651000f, -1.832000f, 177.680135f),
                new(1.121722f, -1.841923f, 178.895433f),
                new(1.593000f, -1.843000f, 180.000000f),
                new(2.076000f, -1.837000f, 180.000000f),
                new(2.537000f, -1.839000f, 180.000000f),
                new(2.999000f, -1.835000f, 180.000000f),
                new(3.471000f, -1.841000f, 180.000000f),
                new(3.949000f, -1.841000f, 180.000000f),
            },
            new SlidePose
                (2.345000f, -1.956000f, -0.000000f)
            ),
            ["pq4"] = (
            new SlideTableMetadata()
            {
                // Name = "pq4",
                Const = 0.139f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 4),
                BuildSlideArea(SensorType.B8, 5, 8),
                BuildSlideArea(SensorType.B7, 9, 11),
                BuildSlideArea(SensorType.B6, 12, 14),
                BuildSlideArea(SensorType.B5, 16, 20),
                BuildSlideArea(SensorType.A4, 22, 24, true, true),
            },
            new SlidePose[]
            {
                new(1.508000f, 4.100000f, 45.000130f),
                new(1.178000f, 3.770000f, 45.000130f),
                new(0.845655f, 3.437652f, 45.000130f),
                new(0.501689f, 3.093684f, 45.000130f),
                new(0.181778f, 2.773771f, 45.000130f),
                new(-0.160656f, 2.431335f, 45.000130f),
                new(-0.496764f, 2.095226f, 45.000130f),
                new(-0.825670f, 1.753592f, 45.000130f),
                new(-1.154473f, 1.437514f, 45.000130f),
                new(-1.467014f, 1.083960f, 52.615428f),
                new(-1.702479f, 0.661817f, 63.784038f),
                new(-1.824100f, 0.214925f, 76.539282f),
                new(-1.815614f, -0.256008f, 91.102763f),
                new(-1.685505f, -0.702899f, 107.027023f),
                new(-1.462059f, -1.121506f, 119.928173f),
                new(-1.154374f, -1.460554f, 128.085146f),
                new(-0.815374f, -1.801554f, 130.099189f),
                new(-0.473374f, -2.134554f, 134.080069f),
                new(-0.158413f, -2.456706f, 135.184627f),
                new(0.167487f, -2.780511f, 135.184627f),
                new(0.509887f, -3.120712f, 135.184627f),
                new(0.844441f, -3.453116f, 135.184627f),
                new(1.173598f, -3.780160f, 135.184627f),
                new(1.515382f, -4.119747f, 135.184627f),
            },
            new SlidePose
                (0.296626f, -3.064554f, 315.000092f)
            ),
            ["pq5"] = (
            new SlideTableMetadata()
            {
                // Name = "pq5",
                Const = 0.160f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 4),
                BuildSlideArea(SensorType.B8, 5, 8),
                BuildSlideArea(SensorType.B7, 9, 12),
                BuildSlideArea(SensorType.B6, 14, 17),
                BuildSlideArea(SensorType.A5, 19, 21, true, true),
            },
            new SlidePose[]
            {
                new(1.501000f, 4.104000f, 45.000130f),
                new(1.181090f, 3.784089f, 45.000130f),
                new(0.838655f, 3.441653f, 45.000130f),
                new(0.494689f, 3.097684f, 45.000130f),
                new(0.174778f, 2.777772f, 45.000130f),
                new(-0.167656f, 2.435336f, 45.000130f),
                new(-0.503764f, 2.099226f, 45.000130f),
                new(-0.832670f, 1.757592f, 45.000130f),
                new(-1.161473f, 1.441514f, 45.000130f),
                new(-1.474014f, 1.087960f, 52.615428f),
                new(-1.709479f, 0.665817f, 63.784038f),
                new(-1.831100f, 0.218925f, 76.539282f),
                new(-1.846000f, -0.252000f, 82.295330f),
                new(-1.847000f, -0.713000f, 85.692230f),
                new(-1.844000f, -1.193000f, 88.814759f),
                new(-1.846852f, -1.677217f, 90.000000f),
                new(-1.841949f, -2.129614f, 90.000000f),
                new(-1.836699f, -2.613862f, 90.000000f),
                new(-1.831547f, -3.089164f, 90.000000f),
                new(-1.834000f, -3.559000f, 90.000000f),
                new(-1.834000f, -4.021000f, 90.000000f),
            },
            new SlidePose
                (-1.972909f, -2.402302f, 270.000065f)
            ),
            ["pq6"] = (
            new SlideTableMetadata()
            {
                // Name = "pq6",
                Const = 0.080f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 4),
                BuildSlideArea(SensorType.B8, 5, 8),
                BuildSlideArea(SensorType.B7, 9, 11),
                BuildSlideArea(SensorType.B6, 13, 15),
                BuildSlideArea(SensorType.B5, 16, 18),
                BuildSlideArea(SensorType.B4, 19, 21),
                BuildSlideArea(SensorType.B3, 22, 24),
                BuildSlideArea(SensorType.B2, 25, 27),
                BuildSlideArea(SensorType.B1, 28, 30),
                BuildSlideArea(SensorType.B8, 31, 33),
                BuildSlideArea(SensorType.B7, 35, 38),
                BuildSlideArea(SensorType.A6, 40, 42, true, true),
            },
            new SlidePose[]
            {
                new(1.503000f, 4.097000f, 45.000130f),
                new(1.183090f, 3.777088f, 45.000130f),
                new(0.840655f, 3.434652f, 45.000130f),
                new(0.496689f, 3.090684f, 45.000130f),
                new(0.176778f, 2.770771f, 45.000130f),
                new(-0.165656f, 2.428335f, 45.000130f),
                new(-0.501764f, 2.092226f, 45.000130f),
                new(-0.830670f, 1.750592f, 45.000130f),
                new(-1.159473f, 1.434514f, 45.000130f),
                new(-1.472014f, 1.080960f, 52.615428f),
                new(-1.707479f, 0.658817f, 63.784038f),
                new(-1.829100f, 0.211925f, 76.539282f),
                new(-1.820614f, -0.259008f, 91.102763f),
                new(-1.690505f, -0.705899f, 107.027023f),
                new(-1.467059f, -1.124506f, 119.928173f),
                new(-1.126233f, -1.456845f, 135.221024f),
                new(-0.732374f, -1.696554f, 148.867804f),
                new(-0.289725f, -1.822417f, 164.980169f),
                new(0.184744f, -1.842923f, 178.895433f),
                new(0.639413f, -1.729078f, 192.327918f),
                new(1.056606f, -1.514116f, 208.051395f),
                new(1.410159f, -1.186018f, 224.118299f),
                new(1.658352f, -0.800644f, 236.498362f),
                new(1.804722f, -0.354459f, 252.592875f),
                new(1.828763f, 0.107989f, 265.953529f),
                new(1.738959f, 0.562658f, 283.522673f),
                new(1.527534f, 0.984801f, 296.084120f),
                new(1.234790f, 1.366638f, 310.546950f),
                new(0.838103f, 1.619074f, 326.291276f),
                new(0.403232f, 1.785243f, 340.031210f),
                new(-0.063459f, 1.822012f, 354.441216f),
                new(-0.534392f, 1.739986f, 11.050720f),
                new(-0.968555f, 1.541995f, 26.155557f),
                new(-1.333422f, 1.257737f, 33.794851f),
                new(-1.649498f, 0.924689f, 39.147840f),
                new(-1.985372f, 0.604369f, 41.768613f),
                new(-2.326196f, 0.263543f, 44.067135f),
                new(-2.652778f, -0.060771f, 45.000130f),
                new(-2.972689f, -0.380683f, 45.000130f),
                new(-3.315123f, -0.723119f, 45.000130f),
                new(-3.659090f, -1.067088f, 45.000130f),
                new(-3.979001f, -1.387000f, 45.000130f),
            },
            new SlidePose
                (-2.921237f, -0.147015f, 225.000250f)
            ),
            ["pq7"] = (
            new SlideTableMetadata()
            {
                // Name = "pq7",
                Const = 0.084f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 4),
                BuildSlideArea(SensorType.B8, 7, 9),
                BuildSlideArea(SensorType.B7, 10, 12),
                BuildSlideArea(SensorType.B6, 13, 15),
                BuildSlideArea(SensorType.B5, 16, 18),
                BuildSlideArea(SensorType.B4, 20, 22),
                BuildSlideArea(SensorType.B3, 23, 25),
                BuildSlideArea(SensorType.B2, 26, 28),
                BuildSlideArea(SensorType.B1, 30, 32),
                BuildSlideArea(SensorType.B8, 33, 36),
                BuildSlideArea(SensorType.A7, 37, 40, true, true),
            },
            new SlidePose[]
            {
                new(1.498000f, 4.112000f, 46.640586f),
                new(1.165000f, 3.774000f, 45.772505f),
                new(0.831000f, 3.448000f, 45.772505f),
                new(0.495000f, 3.108000f, 45.772505f),
                new(0.168000f, 2.781000f, 45.772505f),
                new(-0.168000f, 2.440000f, 44.930370f),
                new(-0.505000f, 2.098000f, 44.350333f),
                new(-0.840000f, 1.768000f, 45.772505f),
                new(-1.169000f, 1.443000f, 45.772505f),
                new(-1.485000f, 1.093000f, 53.250904f),
                new(-1.710000f, 0.693000f, 64.988177f),
                new(-1.833000f, 0.231000f, 77.442358f),
                new(-1.824000f, -0.240000f, 90.404219f),
                new(-1.707000f, -0.692000f, 105.121325f),
                new(-1.481000f, -1.097000f, 119.154841f),
                new(-1.147000f, -1.437000f, 135.075526f),
                new(-0.737000f, -1.687000f, 149.185279f),
                new(-0.299000f, -1.811000f, 164.099152f),
                new(0.185000f, -1.832000f, 178.126597f),
                new(0.647000f, -1.718000f, 192.716590f),
                new(1.049000f, -1.502000f, 207.009455f),
                new(1.396000f, -1.183000f, 221.542821f),
                new(1.656000f, -0.785000f, 238.326641f),
                new(1.804000f, -0.346000f, 251.712358f),
                new(1.826000f, 0.140000f, 266.781466f),
                new(1.731000f, 0.597000f, 281.581342f),
                new(1.534000f, 1.006000f, 293.697062f),
                new(1.226000f, 1.373000f, 309.935807f),
                new(0.853000f, 1.647000f, 325.665431f),
                new(0.383000f, 1.790000f, 341.389209f),
                new(-0.071000f, 1.835000f, -9.598009f),
                new(-0.541000f, 1.847000f, -5.952530f),
                new(-1.012000f, 1.829000f, -1.581022f),
                new(-1.476000f, 1.842000f, 0.000000f),
                new(-1.950000f, 1.829000f, 0.000000f),
                new(-2.422000f, 1.836000f, 0.000000f),
                new(-2.906000f, 1.838000f, 0.000000f),
                new(-3.373000f, 1.832000f, 0.000000f),
                new(-3.843000f, 1.829000f, 0.000000f),
            },
            new SlidePose
                (-2.218574f, 1.944000f, 180.000193f)
            ),
            ["pq8"] = (
            new SlideTableMetadata()
            {
                // Name = "pq8",
                Const = 0.0895f
            },
            new SlideArea[]
            {
                BuildSlideArea(SensorType.A1, 0, 4),
                BuildSlideArea(SensorType.B8, 5, 8),
                BuildSlideArea(SensorType.B7, 9, 11),
                BuildSlideArea(SensorType.B6, 12, 14),
                BuildSlideArea(SensorType.B5, 15, 17),
                BuildSlideArea(SensorType.B4, 19, 21),
                BuildSlideArea(SensorType.B3, 22, 24),
                BuildSlideArea(SensorType.B2, 25, 27),
                BuildSlideArea(SensorType.B1, 28, 32),
                BuildSlideArea(SensorType.A8, 33, 36, true, true),
            },
            new SlidePose[]
            {
                new(1.498000f, 4.112000f, 46.640586f),
                new(1.165000f, 3.774000f, 45.772505f),
                new(0.830000f, 3.438000f, 45.772505f),
                new(0.495000f, 3.108000f, 45.772505f),
                new(0.168000f, 2.781000f, 45.772505f),
                new(-0.180000f, 2.438000f, 45.772505f),
                new(-0.507000f, 2.100000f, 45.772505f),
                new(-0.840000f, 1.768000f, 45.772505f),
                new(-1.178000f, 1.446000f, 45.772505f),
                new(-1.485000f, 1.093000f, 53.250904f),
                new(-1.708000f, 0.693000f, 66.464049f),
                new(-1.833000f, 0.231000f, 77.442358f),
                new(-1.843000f, -0.231000f, 90.404219f),
                new(-1.724000f, -0.683000f, 104.030419f),
                new(-1.495000f, -1.104000f, 119.154841f),
                new(-1.147000f, -1.437000f, 135.075526f),
                new(-0.746000f, -1.692000f, 150.466696f),
                new(-0.299000f, -1.811000f, 164.099152f),
                new(0.185000f, -1.832000f, 178.126597f),
                new(0.647000f, -1.718000f, 192.716590f),
                new(1.042000f, -1.505000f, 205.687419f),
                new(1.396000f, -1.183000f, 221.542821f),
                new(1.656000f, -0.785000f, 238.326641f),
                new(1.794000f, -0.349000f, 250.155257f),
                new(1.826000f, 0.140000f, 266.781466f),
                new(1.731000f, 0.597000f, 279.971020f),
                new(1.534000f, 1.006000f, 293.697062f),
                new(1.234000f, 1.371000f, 306.530735f),
                new(0.891000f, 1.703000f, 308.464790f),
                new(0.565000f, 2.027000f, 314.820166f),
                new(0.229000f, 2.371000f, 314.949011f),
                new(-0.105000f, 2.733000f, 314.949011f),
                new(-0.439000f, 3.040000f, 314.949011f),
                new(-0.764000f, 3.373000f, 314.949011f),
                new(-1.106000f, 3.710000f, 313.461033f),
                new(-1.436000f, 4.042000f, 314.949011f),
            },
            new SlidePose
                (-0.203000f, 2.983000f, 135.341247f)
            ),
        };

    static readonly (SlideTableMetadata metadata,
                    SlideArea[] judgeQueueL,
                    SlideArea[] judgeQueueC,
                    SlideArea[] judgeQueueR,
                    SlidePose[] slidePoses,
                    SlidePose okPose) WIFI_TABLE =
    (
        new SlideTableMetadata()
        {
            // Name = "wifi",
            Const = 0.162870f
        },
        new SlideArea[]
        {
            BuildSlideArea(SensorType.A1,0),
            BuildSlideArea(SensorType.B8,2),
            BuildSlideArea(SensorType.B7,4),
            BuildSlideArea(SensorType.A6 , SensorType.D6,7,true,true)
        },
        new SlideArea[] // Center
        {
            BuildSlideArea(SensorType.A1,0),
            BuildSlideArea(SensorType.B1,2),
            BuildSlideArea(SensorType.C,4),
            BuildSlideArea(SensorType.A5 , SensorType.B5,7,true,true)
        },
        new SlideArea[] // R
        {
            BuildSlideArea(SensorType.A1,0),
            BuildSlideArea(SensorType.B2,2),
            BuildSlideArea(SensorType.B3,4),
            BuildSlideArea(SensorType.A4 , SensorType.D5,7,true,true)
        },
        new SlidePose[]
        {
            new(1.578000f, 3.894000f, -0.000000f),
            new(1.389000f, 3.392000f, -0.000000f),
            new(1.156191f, 2.811000f, -0.000000f),
            new(0.911000f, 2.204000f, -0.000000f),
            new(0.641000f, 1.597000f, -0.000000f),
            new(0.329000f, 0.841000f, -0.000000f),
            new(0.017000f, 0.113000f, -0.000000f),
            new(-0.280000f, -0.610000f, -0.000000f),
            new(-0.577000f, -1.321000f, -0.000000f),
            new(-0.864000f, -2.050000f, -0.000000f),
            new(-1.225000f, -2.868000f, -0.000000f),
        },
        new SlidePose
            (-1.610000f, -3.870000f, 157.429571f)
    );

    static SlideArea BuildSlideArea(SensorType type,
    int arrowProgress,
    bool isSkippable = true, bool isLast = false)
    {
        return new SlideArea()
        {
            Area0 = type,
            ArrowProgressWhenOn = arrowProgress,
            ArrowProgressWhenFinished = arrowProgress,
            IsSkippable = isSkippable,
            IsLast = isLast
        };
    }

    static SlideArea BuildSlideArea(SensorType type,
        int progressWhenOn, int progressWhenFinished,
        bool isSkippable = true, bool isLast = false)
    {
        return new SlideArea()
        {
            Area0 = type,
            ArrowProgressWhenOn = progressWhenOn,
            ArrowProgressWhenFinished = progressWhenFinished,
            IsSkippable = isSkippable,
            IsLast = isLast
        };
    }

    static SlideArea BuildSlideArea(SensorType type0, SensorType type1,
        int arrowProgress,
        bool isSkippable = true, bool isLast = false)
    {
        return new SlideArea()
        {
            Area0 = type0,
            Area1 = type1,
            ArrowProgressWhenOn = arrowProgress,
            ArrowProgressWhenFinished = arrowProgress,
            IsSkippable = isSkippable,
            IsLast = isLast
        };
    }

    static SlideArea BuildSlideArea(SensorType type0, SensorType type1,
        int progressWhenOn, int progressWhenFinished,
        bool isSkippable = true, bool isLast = false)
    {
        return new SlideArea()
        {
            Area0 = type0,
            Area1 = type1,
            ArrowProgressWhenOn = progressWhenOn,
            ArrowProgressWhenFinished = progressWhenFinished,
            IsSkippable = isSkippable,
            IsLast = isLast
        };
    }

    public static (SlideTableMetadata metadata,
                    SlideArea[] judgeQueue,
                    SlidePose[] slidePoses,
                    SlidePose okPose)
        GetSlideTableByName(string shape) => (
            SLIDE_TABLES[shape].metadata,
            (SlideArea[])SLIDE_TABLES[shape].judgeQueue.Clone(),
            (SlidePose[])SLIDE_TABLES[shape].slidePoses.Clone(),
            SLIDE_TABLES[shape].okPose
        );


    public static (SlideTableMetadata metadata,
                    SlideArea[] judgeQueueL,
                    SlideArea[] judgeQueueC,
                    SlideArea[] judgeQueueR,
                    SlidePose[] slidePoses,
                    SlidePose okPose)
        GetWifiTable() => (
            WIFI_TABLE.metadata,
            (SlideArea[])WIFI_TABLE.judgeQueueL.Clone(),
            (SlideArea[])WIFI_TABLE.judgeQueueC.Clone(),
            (SlideArea[])WIFI_TABLE.judgeQueueR.Clone(),
            (SlidePose[])WIFI_TABLE.slidePoses.Clone(),
            WIFI_TABLE.okPose
        );
}