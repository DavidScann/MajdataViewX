using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using static SkinManager;

[BurstCompile]
public struct EachLineData
{
    public float time;
    public int key;
    public int curvLength;
    public float speed;
    public bool usingSV;

    public bool isEnd;

    public NoteSp lineSprite;
    public float ang;
    public float scale;

    public void Init()
    {
        ang = -45f * key;
        lineSprite = NoteSp.EACH_LINE_0.Offset(curvLength - 1);
    }

    public readonly bool IsFoldable(EachLineData other) =>
        time == other.time &&
        key == other.key &&
        curvLength == other.curvLength &&
        speed == other.speed &&
        usingSV == other.usingSV;
}