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

    public bool isEnd;
    public float2 pos;
    public float ang;
    public float scale;
    public float stretchY;
    public float2 holdEndPos;
    public float holdEndScale;

    public uint bodySprite;
    public uint endSprite;
    public uint lineSprite;
    public uint exSprite;
    public float4 exColor;

    public float2 sliceBorder;
    public float brightness;

    public bool ButtonLastState { get; set; }
    public bool SensorLastState { get; set; }
    public bool isHeadJudged;
    public JudgeGrade judgeGrade;
    public float headDiff;
    public float playerIdleTime;
    public float holdPercent;
    public bool isHolding;

    public void Init()
    {
        pos = float2.zero;
        ang = -22.5f + -45f * (int)Key;
        scale = 1f;
        stretchY = 0f;
        holdEndPos = float2.zero;
        holdEndScale = 0f;
        brightness = 1f;

        bodySprite = HOLD;
        endSprite = HOLD_END;
        lineSprite = LINE;
        exSprite = HOLD_EX;
        exColor = Ex;
        sliceBorder = HoldSliceBorder;

        if (isEach)
        {
            bodySprite = HOLD_EACH;
            endSprite = HOLD_END_EACH;
            lineSprite = LINE_EACH;
            exColor = Ex_Each;
        }
        if (isBreak)
        {
            bodySprite = HOLD_BREAK;
            endSprite = HOLD_END_BREAK;
            lineSprite = LINE_BREAK;
            exColor = Ex_Break;
        }
        if (isMine)
        {
            bodySprite = (uint)(isBreak ? HOLD_BREAK_MINE : HOLD_MINE);
            lineSprite = LINE_MINE;
        }
    }
}