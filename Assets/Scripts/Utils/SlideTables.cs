using System;
using System.Collections.Generic;

#nullable enable

public class SlideArea
{
    public SensorArea[] Areas { get; init; }
    public int ArrowProgressWhenOn { get; init; }
    public int ArrowProgressWhenFinished { get; init; }
    public bool IsSkippable { get; set; }
    public bool IsLast { get; set; }
    
    public bool On { get; set; }
    public bool Off { get; set; }
    public bool IsFinished
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
    
    public void Judge(SensorStatus status)
    {
        if (status == SensorStatus.Off)
        {
            if (On)
                Off = true;
        }
        else
            On = true;
    }
}

public class SlideTable
{
    public string Name { get; init; } = string.Empty;
    public SlideArea[] JudgeQueue { get; init; } = Array.Empty<SlideArea>();
    public float Const { get; init; } = 0f;
}

public class WifiTable
{
    public string Name { get; init; } = string.Empty;
    public SlideArea[] Left { get; init; } = Array.Empty<SlideArea>();
    public SlideArea[] Center { get; init; } = Array.Empty<SlideArea>();
    public SlideArea[] Right { get; init; } = Array.Empty<SlideArea>();
    public float Const { get; init; } = 0f;
}

public static class SlideTables
{
    static readonly SlideTable[] SLIDE_TABLES = new SlideTable[]
    {
        new SlideTable()
        {
            Name = "circle2",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 3, false),
                BuildSlideArea(SensorArea.A2, 5, 7, true, true)
            },
            Const = 0.465f
        },
        new SlideTable()
        {
            Name = "circle3",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 3),
                BuildSlideArea(SensorArea.A2, 7, 11, false),
                BuildSlideArea(SensorArea.A3, 13, 15, true, true)
            },
            Const = 0.233f
        },
        new SlideTable()
        {
            Name = "circle4",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 3),
                BuildSlideArea(SensorArea.A2, 7, 11),
                BuildSlideArea(SensorArea.A3, 14, 19),
                BuildSlideArea(SensorArea.A4, 21, 23, true, true)
            },
            Const = 0.155f
        },
        new SlideTable()
        {
            Name = "circle5",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 3),
                BuildSlideArea(SensorArea.A2, 7, 11),
                BuildSlideArea(SensorArea.A3, 14, 19),
                BuildSlideArea(SensorArea.A4, 23, 27),
                BuildSlideArea(SensorArea.A5, 29, 31, true, true)
            },
            Const = 0.116f
        },
        new SlideTable()
        {
            Name = "circle6",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 3),
                BuildSlideArea(SensorArea.A2, 7, 11),
                BuildSlideArea(SensorArea.A3, 14, 19),
                BuildSlideArea(SensorArea.A4, 23, 27),
                BuildSlideArea(SensorArea.A5, 31, 35),
                BuildSlideArea(SensorArea.A6, 37, 39, true, true)
            },
            Const = 0.093f
        },
        new SlideTable()
        {
            Name = "circle7",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 3),
                BuildSlideArea(SensorArea.A2, 7, 11),
                BuildSlideArea(SensorArea.A3, 14, 19),
                BuildSlideArea(SensorArea.A4, 23, 27),
                BuildSlideArea(SensorArea.A5, 31, 35),
                BuildSlideArea(SensorArea.A6, 39, 43),
                BuildSlideArea(SensorArea.A7, 45, 47, true, true)
            },
            Const = 0.078f
        },
        new SlideTable()
        {
            Name = "circle8",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 3),
                BuildSlideArea(SensorArea.A2, 7, 11),
                BuildSlideArea(SensorArea.A3, 14, 19),
                BuildSlideArea(SensorArea.A4, 23, 27),
                BuildSlideArea(SensorArea.A5, 31, 35),
                BuildSlideArea(SensorArea.A6, 39, 43),
                BuildSlideArea(SensorArea.A7, 46, 51),
                BuildSlideArea(SensorArea.A8, 53, 55, true, true)
            },
            Const = 0.066f
        },
        new SlideTable()
        {
            Name = "circle1",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 3),
                BuildSlideArea(SensorArea.A2, 7, 11),
                BuildSlideArea(SensorArea.A3, 14, 19),
                BuildSlideArea(SensorArea.A4, 23, 27),
                BuildSlideArea(SensorArea.A5, 31, 35),
                BuildSlideArea(SensorArea.A6, 39, 43),
                BuildSlideArea(SensorArea.A7, 46, 51),
                BuildSlideArea(SensorArea.A8, 54, 59),
                BuildSlideArea(SensorArea.A1, 61, 63, true, true)
            },
            Const = 0.058f
        },
        new SlideTable()
        {
            Name = "line3",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 3),
                BuildSlideArea(new SensorArea[] { SensorArea.A2, SensorArea.B2 }, 6, 9, false),
                BuildSlideArea(SensorArea.A3, 10, 13, true, true)
            },
            Const = 0.182f
        },
        new SlideTable()
        {
            Name = "line4",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 4),
                BuildSlideArea(SensorArea.B2, 6, 9),
                BuildSlideArea(SensorArea.B3, 11, 14),
                BuildSlideArea(SensorArea.A4, 15, 18, true, true)
            },
            Const = 0.19f
        },
        new SlideTable()
        {
            Name = "line5",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 4),
                BuildSlideArea(SensorArea.B1, 5, 7),
                BuildSlideArea(SensorArea.C, 10, 12),
                BuildSlideArea(SensorArea.B5, 13, 16),
                BuildSlideArea(SensorArea.A5, 17, 19, true, true)
            },
            Const = 0.152f
        },
        new SlideTable()
        {
            Name = "line6",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 4),
                BuildSlideArea(SensorArea.B8, 6, 9),
                BuildSlideArea(SensorArea.B7, 11, 14),
                BuildSlideArea(SensorArea.A6, 15, 18, true, true)
            },
            Const = 0.19f
        },
        new SlideTable()
        {
            Name = "line7",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 3),
                BuildSlideArea(new SensorArea[] { SensorArea.A8, SensorArea.B8 }, 6, 9, false),
                BuildSlideArea(SensorArea.A7, 10, 13, true, true)
            },
            Const = 0.182f
        },
        new SlideTable()
        {
            Name = "v1",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 3),
                BuildSlideArea(SensorArea.B1, 4, 7),
                BuildSlideArea(SensorArea.C, 8, 13),
                BuildSlideArea(SensorArea.B1, 14, 16),
                BuildSlideArea(SensorArea.A1, 17, 19, true, true)
            },
            Const = 0.185f
        },
        new SlideTable()
        {
            Name = "v2",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 3),
                BuildSlideArea(SensorArea.B1, 4, 7),
                BuildSlideArea(SensorArea.C, 8, 13),
                BuildSlideArea(SensorArea.B2, 14, 16),
                BuildSlideArea(SensorArea.A2, 17, 19, true, true)
            },
            Const = 0.15f
        },
        new SlideTable()
        {
            Name = "v3",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 3),
                BuildSlideArea(SensorArea.B1, 4, 7),
                BuildSlideArea(SensorArea.C, 8, 13),
                BuildSlideArea(SensorArea.B3, 14, 16),
                BuildSlideArea(SensorArea.A3, 17, 19, true, true)
            },
            Const = 0.158f
        },
        new SlideTable()
        {
            Name = "v4",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 3),
                BuildSlideArea(SensorArea.B1, 4, 7),
                BuildSlideArea(SensorArea.C, 8, 13),
                BuildSlideArea(SensorArea.B4, 14, 16),
                BuildSlideArea(SensorArea.A4, 17, 19, true, true)
            },
            Const = 0.158f
        },
        new SlideTable()
        {
            Name = "v6",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 3),
                BuildSlideArea(SensorArea.B1, 4, 7),
                BuildSlideArea(SensorArea.C, 8, 13),
                BuildSlideArea(SensorArea.B6, 14, 16),
                BuildSlideArea(SensorArea.A6, 17, 19, true, true)
            },
            Const = 0.158f
        },
        new SlideTable()
        {
            Name = "v7",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 3),
                BuildSlideArea(SensorArea.B1, 4, 7),
                BuildSlideArea(SensorArea.C, 8, 13),
                BuildSlideArea(SensorArea.B7, 14, 16),
                BuildSlideArea(SensorArea.A7, 17, 19, true, true)
            },
            Const = 0.158f
        },
        new SlideTable()
        {
            Name = "v8",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 3),
                BuildSlideArea(SensorArea.B1, 4, 7),
                BuildSlideArea(SensorArea.C, 8, 13),
                BuildSlideArea(SensorArea.B8, 14, 16),
                BuildSlideArea(SensorArea.A8, 17, 19, true, true)
            },
            Const = 0.154f
        },
        new SlideTable()
        {
            Name = "ppqq1",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 3),
                BuildSlideArea(SensorArea.B1, 5, 7),
                BuildSlideArea(SensorArea.C, 10, 13),
                BuildSlideArea(SensorArea.B4, 15, 17),
                BuildSlideArea(SensorArea.A3, 21, 26),
                BuildSlideArea(SensorArea.A2, 29, 32),
                BuildSlideArea(SensorArea.A1, 33, 35, true, true)
            },
            Const = 0.065f

        },
        new SlideTable()
        {
            Name = "ppqq2",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 3),
                BuildSlideArea(SensorArea.B1, 5, 7),
                BuildSlideArea(SensorArea.C, 9, 13),
                BuildSlideArea(SensorArea.B4, 14, 17),
                BuildSlideArea(SensorArea.A3, 20, 25),
                BuildSlideArea(SensorArea.A2, 26, 28, true, true),
            },
            Const = 0.086f
        },
        new SlideTable()
        {
            Name = "ppqq3",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 3),
                BuildSlideArea(SensorArea.B1, 4, 7),
                BuildSlideArea(SensorArea.C, 9, 13),
                BuildSlideArea(SensorArea.B4, 14, 17),
                BuildSlideArea(SensorArea.A3, 19, 22, true, true),
            },
            Const = 0.157f
        },
        new SlideTable()
        {
            Name = "ppqq4",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 3),
                BuildSlideArea(SensorArea.B1, 5, 7),
                BuildSlideArea(SensorArea.C, 9, 13),
                BuildSlideArea(SensorArea.B4, 14, 17),
                BuildSlideArea(SensorArea.A3, 20, 25),
                BuildSlideArea(SensorArea.A2, 28, 33),
                BuildSlideArea(SensorArea.B1, 34, 37),
                BuildSlideArea(SensorArea.C, 39, 43),
                BuildSlideArea(SensorArea.B4, 44, 46),
                BuildSlideArea(SensorArea.A4, 47, 49, true, true),
            },
            Const = 0.065f
        },
        new SlideTable()
        {
            Name = "ppqq5",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 3),
                BuildSlideArea(SensorArea.B1, 5, 7),
                BuildSlideArea(SensorArea.C, 9, 13),
                BuildSlideArea(SensorArea.B4, 14, 17),
                BuildSlideArea(SensorArea.A3, 20, 25),
                BuildSlideArea(SensorArea.A2, 28, 33),
                BuildSlideArea(SensorArea.B1, 34, 37),
                BuildSlideArea(SensorArea.C, 39, 43),
                BuildSlideArea(SensorArea.B5, 44, 46),
                BuildSlideArea(SensorArea.A5, 47, 49, true, true),
            },
            Const = 0.065f
        },
        new SlideTable()
        {
            Name = "ppqq6",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 3),
                BuildSlideArea(SensorArea.B1, 5, 7),
                BuildSlideArea(SensorArea.C, 9, 13),
                BuildSlideArea(SensorArea.B4, 14, 17),
                BuildSlideArea(SensorArea.A3, 20, 25),
                BuildSlideArea(SensorArea.A2, 28, 33),
                BuildSlideArea(SensorArea.B1, 34, 37),
                BuildSlideArea(new SensorArea[] { SensorArea.C, SensorArea.B8 }, 38, 40),
                BuildSlideArea(new SensorArea[] { SensorArea.B7, SensorArea.B6 }, 42, 44),
                BuildSlideArea(SensorArea.A6, 46, 48, true, true),
            },
            Const = 0.067f
        },
        new SlideTable()
        {
            Name = "ppqq7",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 3),
                BuildSlideArea(SensorArea.B1, 5, 7),
                BuildSlideArea(SensorArea.C, 9, 13),
                BuildSlideArea(SensorArea.B4, 14, 17),
                BuildSlideArea(SensorArea.A3, 20, 25),
                BuildSlideArea(SensorArea.A2, 28, 33),
                BuildSlideArea(SensorArea.B1, 34, 37),
                BuildSlideArea(SensorArea.B8, 38, 42),
                BuildSlideArea(SensorArea.A7, 43, 46, true, true),
            },
            Const = 0.079f
        },
        new SlideTable()
        {
            Name = "ppqq8",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 3),
                BuildSlideArea(SensorArea.B1, 5, 7),
                BuildSlideArea(SensorArea.C, 9, 13),
                BuildSlideArea(SensorArea.B4, 14, 17),
                BuildSlideArea(SensorArea.A3, 20, 25),
                BuildSlideArea(SensorArea.A2, 28, 33),
                BuildSlideArea(new SensorArea[] { SensorArea.B1, SensorArea.A1 }, 35, 37),
                BuildSlideArea(SensorArea.A8, 38, 41, true, true),
            },
            Const = 0.0626f
        },
        new SlideTable()
        {
            Name = "L2",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 3),
                BuildSlideArea(new SensorArea[] { SensorArea.B8, SensorArea.A8 }, 6, 10, false),
                BuildSlideArea(SensorArea.A7, 12, 19),
                BuildSlideArea(SensorArea.B8, 21, 24),
                BuildSlideArea(SensorArea.B1, 25, 28),
                BuildSlideArea(SensorArea.A2, 29, 32, true, true),
            },
            Const = 0.1f
        },
        new SlideTable()
        {
            Name = "L3",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 3),
                BuildSlideArea(new SensorArea[] { SensorArea.B8, SensorArea.A8 }, 6, 10, false),
                BuildSlideArea(SensorArea.A7, 12, 18),
                BuildSlideArea(SensorArea.B7, 20, 22),
                BuildSlideArea(SensorArea.C, 25, 27),
                BuildSlideArea(SensorArea.B3, 28, 31),
                BuildSlideArea(SensorArea.A3, 32, 34, true, true),
            },
            Const = 0.104f
        },
        new SlideTable()
        {
            Name = "L4",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 3),
                BuildSlideArea(new SensorArea[] { SensorArea.B8, SensorArea.A8 }, 6, 10, false),
                BuildSlideArea(SensorArea.A7, 12, 19),
                BuildSlideArea(SensorArea.B6, 21, 24),
                BuildSlideArea(SensorArea.B5, 25, 28),
                BuildSlideArea(SensorArea.A4, 29, 32, true, true),
            },
            Const = 0.098f
        },
        new SlideTable()
        {
            Name = "L5",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 3),
                BuildSlideArea(new SensorArea[] { SensorArea.B8, SensorArea.A8 }, 6, 10, false),
                BuildSlideArea(SensorArea.A7, 12, 18),
                BuildSlideArea(new SensorArea[] { SensorArea.B6, SensorArea.A6 }, 21, 24, false),
                BuildSlideArea(SensorArea.A5, 27, 28, true, true),
            },
            Const = 0.105f
        },
        new SlideTable()
        {
            Name = "s",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 4),
                BuildSlideArea(SensorArea.B8, 7, 9),
                BuildSlideArea(SensorArea.B7, 10, 12),
                BuildSlideArea(SensorArea.C, 14, 17),
                BuildSlideArea(SensorArea.B3, 19, 21),
                BuildSlideArea(SensorArea.B4, 22, 25),
                BuildSlideArea(SensorArea.A5, 27, 30, true, true),
            },
            Const = 0.13f
        },
        new SlideTable()
        {
            Name = "pq1",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 4),
                BuildSlideArea(SensorArea.B8, 5, 8),
                BuildSlideArea(SensorArea.B7, 9, 11),
                BuildSlideArea(SensorArea.B6, 12, 14),
                BuildSlideArea(SensorArea.B5, 15, 17),
                BuildSlideArea(SensorArea.B4, 19, 21),
                BuildSlideArea(SensorArea.B3, 22, 24),
                BuildSlideArea(SensorArea.B2, 25, 29),
                BuildSlideArea(SensorArea.A1, 30, 33, true, true),
            },
            Const = 0.095f
        },
        new SlideTable()
        {
            Name = "pq2",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 4),
                BuildSlideArea(SensorArea.B8, 5, 8),
                BuildSlideArea(SensorArea.B7, 9, 11),
                BuildSlideArea(SensorArea.B6, 12, 14),
                BuildSlideArea(SensorArea.B5, 16, 18),
                BuildSlideArea(SensorArea.B4, 19, 21),
                BuildSlideArea(SensorArea.B3, 22, 26),
                BuildSlideArea(SensorArea.A2, 27, 30, true, true),
            },
            Const = 0.112f
        },
        new SlideTable()
        {
            Name = "pq3",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 4),
                BuildSlideArea(SensorArea.B8, 5, 8),
                BuildSlideArea(SensorArea.B7, 9, 11),
                BuildSlideArea(SensorArea.B6, 12, 14),
                BuildSlideArea(SensorArea.B5, 16, 18),
                BuildSlideArea(SensorArea.B4, 20, 23),
                BuildSlideArea(SensorArea.A3, 25, 27, true, true),
            },
            Const = 0.125f
        },
        new SlideTable()
        {
            Name = "pq4",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 4),
                BuildSlideArea(SensorArea.B8, 5, 8),
                BuildSlideArea(SensorArea.B7, 9, 11),
                BuildSlideArea(SensorArea.B6, 12, 14),
                BuildSlideArea(SensorArea.B5, 16, 20),
                BuildSlideArea(SensorArea.A4, 22, 24, true, true),
            },
            Const = 0.139f
        },
        new SlideTable()
        {
            Name = "pq5",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 4),
                BuildSlideArea(SensorArea.B8, 5, 8),
                BuildSlideArea(SensorArea.B7, 9, 12),
                BuildSlideArea(SensorArea.B6, 14, 17),
                BuildSlideArea(SensorArea.A5, 19, 21, true, true),
            },
            Const = 0.160f
        },
        new SlideTable()
        {
            Name = "pq6",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 4),
                BuildSlideArea(SensorArea.B8, 5, 8),
                BuildSlideArea(SensorArea.B7, 9, 11),
                BuildSlideArea(SensorArea.B6, 13, 15),
                BuildSlideArea(SensorArea.B5, 16, 18),
                BuildSlideArea(SensorArea.B4, 19, 21),
                BuildSlideArea(SensorArea.B3, 22, 24),
                BuildSlideArea(SensorArea.B2, 25, 27),
                BuildSlideArea(SensorArea.B1, 28, 30),
                BuildSlideArea(SensorArea.B8, 31, 33),
                BuildSlideArea(SensorArea.B7, 35, 38),
                BuildSlideArea(SensorArea.A6, 40, 42, true, true),
            },
            Const = 0.080f
        },
        new SlideTable()
        {
            Name = "pq7",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 4),
                BuildSlideArea(SensorArea.B8, 7, 9),
                BuildSlideArea(SensorArea.B7, 10, 12),
                BuildSlideArea(SensorArea.B6, 13, 15),
                BuildSlideArea(SensorArea.B5, 16, 18),
                BuildSlideArea(SensorArea.B4, 20, 22),
                BuildSlideArea(SensorArea.B3, 23, 25),
                BuildSlideArea(SensorArea.B2, 26, 28),
                BuildSlideArea(SensorArea.B1, 30, 32),
                BuildSlideArea(SensorArea.B8, 33, 36),
                BuildSlideArea(SensorArea.A7, 37, 40, true, true),
            },
            Const = 0.084f
        },
        new SlideTable()
        {
            Name = "pq8",
            JudgeQueue = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0, 4),
                BuildSlideArea(SensorArea.B8, 5, 8),
                BuildSlideArea(SensorArea.B7, 9, 11),
                BuildSlideArea(SensorArea.B6, 12, 14),
                BuildSlideArea(SensorArea.B5, 15, 17),
                BuildSlideArea(SensorArea.B4, 19, 21),
                BuildSlideArea(SensorArea.B3, 22, 24),
                BuildSlideArea(SensorArea.B2, 25, 27),
                BuildSlideArea(SensorArea.B1, 28, 32),
                BuildSlideArea(SensorArea.A8, 33, 36, true, true),
            },
            Const = 0.0895f
        },
    };

    static readonly WifiTable[] WIFI_TABLES = new WifiTable[]
    {
        // Index 0 (Dictionary Key 1)
        new WifiTable
        {
            Name = "wifi",
            Left = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0),
                BuildSlideArea(SensorArea.B8, 2),
                BuildSlideArea(SensorArea.B7, 4),
                BuildSlideArea(new[] { SensorArea.A6, SensorArea.D6 }, 7, true, true)
            },
            Center = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0),
                BuildSlideArea(SensorArea.B1, 2),
                BuildSlideArea(SensorArea.C, 4),
                BuildSlideArea(new[] { SensorArea.A5, SensorArea.B5 }, 7, true, true)
            },
            Right = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A1, 0),
                BuildSlideArea(SensorArea.B2, 2),
                BuildSlideArea(SensorArea.B3, 4),
                BuildSlideArea(new[] { SensorArea.A4, SensorArea.D5 }, 7, true, true)
            },
            Const = 0.162870f
        },
        // Index 1 (Dictionary Key 2)
        new WifiTable
        {
            Name = "wifi",
            Left = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A2, 0),
                BuildSlideArea(SensorArea.B1, 2),
                BuildSlideArea(SensorArea.B8, 4),
                BuildSlideArea(new[] { SensorArea.A7, SensorArea.D7 }, 7, true, true)
            },
            Center = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A2, 0),
                BuildSlideArea(SensorArea.B2, 2),
                BuildSlideArea(SensorArea.C, 4),
                BuildSlideArea(new[] { SensorArea.A6, SensorArea.B6 }, 7, true, true)
            },
            Right = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A2, 0),
                BuildSlideArea(SensorArea.B3, 2),
                BuildSlideArea(SensorArea.B4, 4),
                BuildSlideArea(new[] { SensorArea.A5, SensorArea.D6 }, 7, true, true)
            },
            Const = 0.162870f
        },
        // Index 2 (Dictionary Key 3)
        new WifiTable
        {
            Name = "wifi",
            Left = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A3, 0),
                BuildSlideArea(SensorArea.B2, 2),
                BuildSlideArea(SensorArea.B1, 4),
                BuildSlideArea(new[] { SensorArea.A8, SensorArea.D8 }, 7, true, true)
            },
            Center = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A3, 0),
                BuildSlideArea(SensorArea.B3, 2),
                BuildSlideArea(SensorArea.C, 4),
                BuildSlideArea(new[] { SensorArea.A7, SensorArea.B7 }, 7, true, true)
            },
            Right = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A3, 0),
                BuildSlideArea(SensorArea.B4, 2),
                BuildSlideArea(SensorArea.B5, 4),
                BuildSlideArea(new[] { SensorArea.A6, SensorArea.D7 }, 7, true, true)
            },
            Const = 0.162870f
        },
        // Index 3 (Dictionary Key 4)
        new WifiTable
        {
            Name = "wifi",
            Left = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A4, 0),
                BuildSlideArea(SensorArea.B3, 2),
                BuildSlideArea(SensorArea.B2, 4),
                BuildSlideArea(new[] { SensorArea.A1, SensorArea.D1 }, 7, true, true)
            },
            Center = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A4, 0),
                BuildSlideArea(SensorArea.B4, 2),
                BuildSlideArea(SensorArea.C, 4),
                BuildSlideArea(new[] { SensorArea.A8, SensorArea.B8 }, 7, true, true)
            },
            Right = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A4, 0),
                BuildSlideArea(SensorArea.B5, 2),
                BuildSlideArea(SensorArea.B6, 4),
                BuildSlideArea(new[] { SensorArea.A7, SensorArea.D8 }, 7, true, true)
            },
            Const = 0.162870f
        },
        // Index 4 (Dictionary Key 5)
        new WifiTable
        {
            Name = "wifi",
            Left = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A5, 0),
                BuildSlideArea(SensorArea.B4, 2),
                BuildSlideArea(SensorArea.B3, 4),
                BuildSlideArea(new[] { SensorArea.A2, SensorArea.D2 }, 7, true, true)
            },
            Center = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A5, 0),
                BuildSlideArea(SensorArea.B5, 2),
                BuildSlideArea(SensorArea.C, 4),
                BuildSlideArea(new[] { SensorArea.A1, SensorArea.B1 }, 7, true, true)
            },
            Right = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A5, 0),
                BuildSlideArea(SensorArea.B6, 2),
                BuildSlideArea(SensorArea.B7, 4),
                BuildSlideArea(new[] { SensorArea.A8, SensorArea.D1 }, 7, true, true)
            },
            Const = 0.162870f
        },
        // Index 5 (Dictionary Key 6)
        new WifiTable
        {
            Name = "wifi",
            Left = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A6, 0),
                BuildSlideArea(SensorArea.B5, 2),
                BuildSlideArea(SensorArea.B4, 4),
                BuildSlideArea(new[] { SensorArea.A3, SensorArea.D3 }, 7, true, true)
            },
            Center = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A6, 0),
                BuildSlideArea(SensorArea.B6, 2),
                BuildSlideArea(SensorArea.C, 4),
                BuildSlideArea(new[] { SensorArea.A2, SensorArea.B2 }, 7, true, true)
            },
            Right = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A6, 0),
                BuildSlideArea(SensorArea.B7, 2),
                BuildSlideArea(SensorArea.B8, 4),
                BuildSlideArea(new[] { SensorArea.A1, SensorArea.D2 }, 7, true, true)
            },
            Const = 0.162870f
        },
        // Index 6 (Dictionary Key 7)
        new WifiTable
        {
            Name = "wifi",
            Left = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A7, 0),
                BuildSlideArea(SensorArea.B6, 2),
                BuildSlideArea(SensorArea.B5, 4),
                BuildSlideArea(new[] { SensorArea.A4, SensorArea.D4 }, 7, true, true)
            },
            Center = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A7, 0),
                BuildSlideArea(SensorArea.B7, 2),
                BuildSlideArea(SensorArea.C, 4),
                BuildSlideArea(new[] { SensorArea.A3, SensorArea.B3 }, 7, true, true)
            },
            Right = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A7, 0),
                BuildSlideArea(SensorArea.B8, 2),
                BuildSlideArea(SensorArea.B1, 4),
                BuildSlideArea(new[] { SensorArea.A2, SensorArea.D3 }, 7, true, true)
            },
            Const = 0.162870f
        },
        // Index 7 (Dictionary Key 8)
        new WifiTable
        {
            Name = "wifi",
            Left = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A8, 0),
                BuildSlideArea(SensorArea.B7, 2),
                BuildSlideArea(SensorArea.B6, 4),
                BuildSlideArea(new[] { SensorArea.A5, SensorArea.D5 }, 7, true, true)
            },
            Center = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A8, 0),
                BuildSlideArea(SensorArea.B8, 2),
                BuildSlideArea(SensorArea.C, 4),
                BuildSlideArea(new[] { SensorArea.A4, SensorArea.B4 }, 7, true, true)
            },
            Right = new SlideArea[]
            {
                BuildSlideArea(SensorArea.A8, 0),
                BuildSlideArea(SensorArea.B1, 2),
                BuildSlideArea(SensorArea.B2, 4),
                BuildSlideArea(new[] { SensorArea.A3, SensorArea.D4 }, 7, true, true)
            },
            Const = 0.162870f
        }
    };

    public static SlideTable? FindTableByName(string prefabName)
    {
        return Array.Find(SLIDE_TABLES, x => x.Name == prefabName);
    }

    public static WifiTable GetWifiTable(int startPos)
    {
        return WIFI_TABLES[startPos - 1];
    }

    static SlideArea BuildSlideArea(SensorArea area, int arrowProgress,
        bool isSkippable = true, bool isLast = false)
    {
        return new SlideArea()
        {
            Areas = new[] { area },
            ArrowProgressWhenOn = arrowProgress,
            ArrowProgressWhenFinished = arrowProgress,
            IsSkippable = isSkippable,
            IsLast = isLast
        };
    }

    static SlideArea BuildSlideArea(SensorArea area, int progressWhenOn, int progressWhenFinished,
        bool isSkippable = true, bool isLast = false)
    {
        return new SlideArea()
        {
            Areas = new[] { area },
            ArrowProgressWhenOn = progressWhenOn,
            ArrowProgressWhenFinished = progressWhenFinished,
            IsSkippable = isSkippable,
            IsLast = isLast
        };
    }

    static SlideArea BuildSlideArea(SensorArea[] type, int arrowProgress,
        bool isSkippable = true, bool isLast = false)
    {
        return new SlideArea()
        {
            Areas = type,
            ArrowProgressWhenOn = arrowProgress,
            ArrowProgressWhenFinished = arrowProgress,
            IsSkippable = isSkippable,
            IsLast = isLast
        };
    }

    static SlideArea BuildSlideArea(SensorArea[] type, int progressWhenOn,
        int progressWhenFinished, bool isSkippable = true, bool isLast = false)
    {
        return new SlideArea()
        {
            Areas = type,
            ArrowProgressWhenOn = progressWhenOn,
            ArrowProgressWhenFinished = progressWhenFinished,
            IsSkippable = isSkippable,
            IsLast = isLast
        };
    }
}

