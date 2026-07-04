#pragma warning disable CS8500
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

using static NoteSkinManager;

[BurstCompile]
public struct EachLineData
{
    public float time;
    public int key;
    public int curvLength;
    public float speed;
    public bool usingSV;
    public bool isEnd;

    public uint lineSprite;
    public float ang;
    public float scale;

    public void Init()
    {
        ang = -45f * key;
        lineSprite = (uint)(EACH_LINE_0 + curvLength - 1);
    }
}