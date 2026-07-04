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
public struct TouchHoldData
{
    public float time;
    public SensorType sensor;
    public float speed;
    public int sensorOrderIndex;
    public float LastFor;

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
    public float maskProgress;

    public uint fanSprite;
    public uint pointSprite;
    public uint borderSprite;

    public bool isJudged;
    public JudgeGrade judgeGrade;
    public float headDiff;
    public float playerIdleTime;
    public float holdPercent;
    public bool isHolding;

    public void Init()
    {
        show = false;
        fanProgress = 0;
        fanAlpha = 0;
        maskProgress = 0;

        centerPos = TouchData.GetAreaPos(sensor);

        fanSprite = TOUCH_HOLD_0;
        pointSprite = TOUCH_POINT;
        borderSprite = TOUCH_HOLD_BORDER;

        if (isBreak)
        {
            fanSprite = TOUCH_HOLD_BREAK_0;
            pointSprite = TOUCH_POINT_BREAK;
            borderSprite = TOUCH_HOLD_BORDER_BREAK;
        }
        if (isMine)
        {
            fanSprite = TOUCH_HOLD_MINE_0;
            pointSprite = TOUCH_POINT_MINE;
            if (isBreak)
                borderSprite = TOUCH_HOLD_BORDER_BREAK_MINE;
            else
                borderSprite = TOUCH_HOLD_BORDER_MINE;
        }
    }
}