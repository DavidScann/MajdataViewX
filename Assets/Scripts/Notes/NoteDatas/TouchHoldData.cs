using System.Threading;
using MajSimai;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

using static SkinManager;
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
    public bool usingSV;

    public bool isEnd;
    public float2 centerPos;

    public float fanAlpha;
    public float maskProgress;

    public NoteSp fanSprite;
    public NoteSp pointSprite;
    public NoteSp borderSprite;
    public NoteSp _borderOnSpriteCache;

    public bool isHeadJudged;
    public JudgeGrade judgeGrade;
    public float headDiff;
    public float playerIdleTime;
    public float holdPercent;
    public bool isHolding;
    public float releaseTimeSec;

    public int groupId;
    public int coverageId;

    public void Init()
    {
        groupId = -1;
        coverageId = -1;
        fanAlpha = 0;
        maskProgress = 0;

        centerPos = MajPos.GetAreaPos(sensor);

        fanSprite = NoteSp.TOUCH_HOLD_0;
        pointSprite = NoteSp.TOUCH_POINT;
        _borderOnSpriteCache = borderSprite = NoteSp.TOUCH_HOLD_BORDER;

        if (isBreak)
        {
            fanSprite = NoteSp.TOUCH_HOLD_BREAK_0;
            pointSprite = NoteSp.TOUCH_POINT_BREAK;
            _borderOnSpriteCache = borderSprite = NoteSp.TOUCH_HOLD_BORDER_BREAK;
        }
        if (isMine)
        {
            fanSprite = NoteSp.TOUCH_HOLD_MINE_0;
            pointSprite = NoteSp.TOUCH_POINT_MINE;
            if (isBreak)
                _borderOnSpriteCache = borderSprite = NoteSp.TOUCH_HOLD_BORDER_BREAK_MINE;
            else
                _borderOnSpriteCache = borderSprite = NoteSp.TOUCH_HOLD_BORDER_MINE;
        }
    }
}