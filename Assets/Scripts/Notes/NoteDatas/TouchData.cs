#pragma warning disable CS8500
using System.Threading;
using MajSimai;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

using static NoteSkinManager;
using static MajBurst;

[BurstCompile]
public struct TouchData
{
    public float time;
    public SensorType sensor;
    public float speed;
    public int sensorOrderIndex;

    public bool isHanabi;
    public bool isEach;
    public bool isEx;
    public bool isBreak;
    public bool isMine;

    public bool show;
    public bool isEnd;
    public float2 centerPos;

    public float fanProgress;
    public float fanAlpha;

    public uint fanSprite;
    public uint pointSprite;
    public uint borderSprite0;
    public uint borderSprite1;
    public uint justSprite;

    public bool isJudged;
    public JudgeGrade judgeGrade;
    public float diff;

    public void Init()
    {
        show = false;
        fanProgress = 0;
        fanAlpha = 0;

        centerPos = GetAreaPos(sensor);

        fanSprite = TOUCH;
        pointSprite = TOUCH_POINT;
        borderSprite0 = TOUCH_BORDER_0;
        borderSprite1 = TOUCH_BORDER_1;
        justSprite = TOUCH_JUST;

        if (isEach)
        {
            fanSprite = TOUCH_EACH;
            pointSprite = TOUCH_POINT_EACH;
            borderSprite0 = TOUCH_BORDER_EACH_0;
            borderSprite1 = TOUCH_BORDER_EACH_1;
        }
        if (isBreak)
        {
            fanSprite = TOUCH_BREAK;
            pointSprite = TOUCH_POINT_BREAK;
            borderSprite0 = TOUCH_BORDER_BREAK_0;
            borderSprite1 = TOUCH_BORDER_BREAK_1;
        }
        if (isMine)
        {
            if (isBreak)
            {
                fanSprite = TOUCH_BREAK_MINE;
                pointSprite = TOUCH_POINT_BREAK_MINE;
                borderSprite0 = TOUCH_BORDER_BREAK_MINE_0;
                borderSprite1 = TOUCH_BORDER_BREAK_MINE_1;
            }
            else
            {
                fanSprite = TOUCH_MINE;
                pointSprite = TOUCH_POINT_MINE;
                borderSprite0 = TOUCH_BORDER_MINE_0;
                borderSprite1 = TOUCH_BORDER_MINE_1;
            }
        }
    }

    public static float2 GetAreaPos(SensorType sensor)
    {
        int i = (int)sensor;
        if (i >= 0 && i <= 7)
            return RingPos(4.1f, i + 1, false);
        if (i >= 8 && i <= 15)
            return RingPos(2.3f, i - 7, false);
        if (i == 16)
            return float2.zero;
        if (i >= 17 && i <= 24)
            return RingPos(4.1f, i - 16, true);
        if (i >= 25 && i <= 32)
            return RingPos(3.0f, i - 24, true);
        return float2.zero;
    }

    private static float2 RingPos(float radius, int index1, bool altAngle)
    {
        var a = altAngle
            ? (index1 * -2f + 6.4f) * 0.125f * math.PI
            : (index1 * -2f + 5f) * 0.125f * math.PI;
        return new float2(radius * math.cos(a), radius * math.sin(a));
    }}
