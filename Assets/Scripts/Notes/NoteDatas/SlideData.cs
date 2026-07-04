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
public struct SlideData
{
    public float tapTime;
    public float time;
    public float LastFor;
    public int startPosition;
    public int endPosition;
    public float speed;
    public int sensorOrderIndex;

    public bool isEach;
    public bool isEx;
    public bool isBreak;
    public bool isMine;
    public bool usingSV;

    public bool isWifi;
    public bool isMirror;
    public bool isJustR;
    public bool smoothSlideAnime;
    public bool legacySlideLayer;

    public SlideTableMetadata metadata;
    public unsafe SlideArea* judgeQueue;
    public int judgeQueueCount;
    public unsafe SlideArea* judgeQueueC;
    public int judgeQueueCCount;
    public unsafe SlideArea* judgeQueueR;
    public int judgeQueueRCount;
    public unsafe SlidePose* slideArrows;
    public int slideArrowsCount;
    public SlidePose okPose;

    public bool isEnd;
    public bool isSoundPlayed;

    //slide
    public float2 pos;
    public float ang;
    public uint slideSprite;
    //star
    public float process;
    public float starPosX;
    public float starPosY;
    public float starRot;
    public float starAlpha;
    public float starScale;
    public uint starSprite;

    public bool isJudged;
    public JudgeGrade judgeGrade;

    public byte judgeCurrent; //SLide / Wifi Left
    public byte judgeC_Current; //Wifi Center
    public byte judgeR_Current; //Wifi Right

    // Animation state
    public float fadeInStartTiming;
    public float fadeInDuration;
    public float slideAlpha; // 0->1

    public float judgeTime; //被判定时
    public float slideOKAlpha; // 1->0
    public const float SlideOKFadeOutDuration = 0.5f;

    public unsafe void Init()
    {
        starAlpha = 0;
        starScale = 0;
        process = 0;
        slideAlpha = 0;
        slideOKAlpha = 1f;
        judgeTime = float.MinValue;


        ang = isMirror ? -45f * startPosition : -45f * (startPosition - 1);

        var diff = math.abs(1 - startPosition);
        if (!isWifi)
        {
            for (var i = 0; i < judgeQueueCount; i++)
            {
                if (isMirror)
                {
                    judgeQueue[i].Mirror(SensorType.A1);
                }
                if (diff != 0)
                {
                    judgeQueue[i].Diff(diff);
                }
            }
        }
        else
        {
            if (diff != 0)
            {
                for (var i = 0; i < judgeQueueCount; i++)
                    judgeQueue[i].Diff(diff);
                for (var i = 0; i < judgeQueueCCount; i++)
                    judgeQueueC[i].Diff(diff);
                for (var i = 0; i < judgeQueueRCount; i++)
                    judgeQueueR[i].Diff(diff);
            }
        }

        if (isMirror)
        {
            for (var i = 0; i < slideArrowsCount; i++)
            {
                slideArrows[i].X *= -1;
                slideArrows[i].RotZ = -slideArrows[i].RotZ + 180f;
            }
        }


        // 计算Slide淡入时机
        // 8.0速时应当提前300ms显示Slide
        fadeInStartTiming = -3.926913f / speed;
        // Slide完全淡入时机
        // 正常情况下应为负值；速度过高将忽略淡入      
        var fadeInFinishTiming = math.min(fadeInStartTiming + 0.2f, 0);
        //淡入时机与正解帧间隔尝于200ms时，加快淡入动画的播放速度，这个过程现在是自然实现的        
        fadeInDuration = fadeInFinishTiming - fadeInStartTiming;

        // Load Skin
        slideSprite = SLIDE;
        starSprite = STAR;
        if (isEach)
        {
            slideSprite = SLIDE_EACH;
            starSprite = STAR_EACH;
        }
        if (isBreak)
        {
            slideSprite = SLIDE_BREAK;
            starSprite = STAR_BREAK;
        }
        if (isMine)
        {
            if (isBreak)
            {
                slideSprite = SLIDE_BREAK_MINE;
                starSprite = STAR_BREAK_MINE;
            }
            else
            {
                slideSprite = SLIDE_MINE;
                starSprite = STAR_MINE;
            }
        }
    }
}
