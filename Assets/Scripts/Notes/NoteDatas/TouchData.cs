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
    public bool usingSV;

    public bool isAppeared;
    public bool isEnd;
    public float2 centerPos;

    public float wholeDuration;
    public float moveDuration;
    public float displayDuration;

    public float fanAlpha;

    public uint fanSprite;
    public uint pointSprite;
    public uint justSprite;

    public bool isJudged;
    public JudgeGrade judgeGrade;
    public float diff;

    public int groupId;
    public int coverageId;

    public void Init()
    {
        groupId = -1;
        coverageId = -1;
        fanAlpha = 0;

        centerPos = MajPos.GetAreaPos(sensor);

        wholeDuration = 3.209385682f * math.pow(speed, -0.9549621752f);
        displayDuration = 0.2f * wholeDuration;
        moveDuration = 0.8f * wholeDuration;

        fanSprite = TOUCH;
        pointSprite = TOUCH_POINT;
        justSprite = TOUCH_JUST;

        if (isEach)
        {
            fanSprite = TOUCH_EACH;
            pointSprite = TOUCH_POINT_EACH;
        }
        if (isBreak)
        {
            fanSprite = TOUCH_BREAK;
            pointSprite = TOUCH_POINT_BREAK;
        }
        if (isMine)
        {
            if (isBreak)
            {
                fanSprite = TOUCH_BREAK_MINE;
                pointSprite = TOUCH_POINT_BREAK_MINE;
            }
            else
            {
                fanSprite = TOUCH_MINE;
                pointSprite = TOUCH_POINT_MINE;
            }
        }
    }
}
