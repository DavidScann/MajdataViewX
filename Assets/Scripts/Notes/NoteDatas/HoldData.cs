using System;
using System.ComponentModel;
using Unity.Burst;
using Unity.Mathematics;

using static SkinManager;

[BurstCompile]
public struct HoldData
{
    public float time;
    public SensorType Key;
    public float speed;
    public float LastFor;
    public int ButtonOrderIndex;
    public int SensorOrderIndex;

    public bool isEach;
    public bool isEx;
    public bool isBreak;
    public bool isMine;
    public bool usingSV;

    public bool isFolded;

    public bool isEnd;
    public float2 pos;
    public float ang;
    public float scale;
    public float stretchY;
    public float2 holdEndPos;
    public float holdEndScale;

    public NoteSp bodySprite;
    public NoteSp endSprite;
    public NoteSp lineSprite;
    public NoteSp exSprite;
    public float4 exColor;

    public float2 sliceBorder;
    public float brightness;

    public bool isHeadJudged;
    public float headDiff;
    public JudgeGrade judgeGrade;
    public bool isHolding;
    public float holdPercent;
    public float playerIdleTimeSec;
    public float lastReleaseTimeSec;

    public void Init()
    {
        pos = float2.zero;
        ang = -22.5f + -45f * (int)Key;
        scale = 1f;
        stretchY = 0f;
        holdEndPos = float2.zero;
        holdEndScale = 0f;
        brightness = 1f;

        bodySprite = NoteSp.HOLD;
        endSprite = NoteSp.HOLD_END;
        lineSprite = NoteSp.LINE;
        exSprite = NoteSp.HOLD_EX;
        exColor = Ex;
        sliceBorder = HoldSliceBorder;

        if (isEach)
        {
            bodySprite = NoteSp.HOLD_EACH;
            endSprite = NoteSp.HOLD_END_EACH;
            lineSprite = NoteSp.LINE_EACH;
            exColor = Ex_Each;
        }
        if (isBreak)
        {
            bodySprite = NoteSp.HOLD_BREAK;
            endSprite = NoteSp.HOLD_END_BREAK;
            lineSprite = NoteSp.LINE_BREAK;
            exColor = Ex_Break;
        }
        if (isMine)
        {
            bodySprite = isBreak ? NoteSp.HOLD_BREAK_MINE : NoteSp.HOLD_MINE;
            endSprite = NoteSp.HOLD_END_MINE;
            lineSprite = NoteSp.LINE_MINE;
            exColor = Ex_Mine;
        }

        // Each Tap Line优先级高于break低于mine
        if (isEach && !isMine)
            lineSprite = NoteSp.LINE_EACH;

        // 一开始放开时间无穷大，按下后才能重置为0，避免一开始就小于release忽略时间
        lastReleaseTimeSec = float.PositiveInfinity;
    }

    public readonly bool IsFoldable(HoldData other) =>
        time == other.time &&
        Key == other.Key &&
        speed == other.speed &&
        LastFor == other.LastFor &&

        isEach == other.isEach &&
        isEx == other.isEx &&
        isBreak == other.isBreak &&
        isMine == other.isMine &&
        usingSV == other.usingSV;
}