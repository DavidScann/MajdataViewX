#pragma warning disable CS8500
using Unity.Burst;
using Unity.Mathematics;
using static NoteSkinManager;
using Notes.SlideUtils;

[BurstCompile]
public struct SlideData
{
    public float tapTime;
    public float shootTime;

    // FOR WIFI STARS CALCULATE ONLY
    public int startPos;
    public int endPos;

    public float LastFor;
    public float speed;
    public int sensorOrderIndex;

    public bool isEach;
    public bool isEx;
    public bool isBreak;
    public bool isMine;
    public bool usingSV;

    public bool isWifi;
    public bool smoothSlideAnime;
    public bool legacySlideLayer;

    public int judgeQueueOffset;
    public unsafe SlideArea* judgeQueue;
    public int judgeQueueCount;
    public int judgeQueueLOffset;
    public unsafe SlideArea* judgeQueueL;
    public int judgeQueueLCount;
    public int judgeQueueROffset;
    public unsafe SlideArea* judgeQueueR;
    public int judgeQueueRCount;
    public float Const;
    public int slideArrowsOffset;

    public int unskippable1;
    public int unskippable2;
    public SensorType currentOn;    // Invalid就是没按，否则就是正在按的区
    public SensorType currentOnL;
    public SensorType currentOnR;

    public unsafe SlidePose* slideArrows;
    // 注意第一个是起点最后一个是终点不需要画箭头
    public int slideArrowsCount;
    public bool noLastArrow;
    public SlideOkType okType;
    public SlidePose okPose;

    public bool isEnd;
    public bool isSoundPlayed;

    //slide
    public uint slideSprite;
    public int eaten;
    //star
    public float process;
    public int processIdx; // 标记现在引导星星走到哪儿了（引导星星之后的第一个箭头idx）
    public float starAlpha;
    public float starScale;
    public uint starSprite;
    //FOR WIFI
    public float2 starPosStart;
    public float2 starPosConstC; //for wifi only
    public float2 starPosConstL;
    public float2 starPosConstR;

    public float2 starPos;
    public float2 starPosL;
    public float2 starPosR;

    public bool isJudged;
    public JudgeGrade judgeGrade;

    public int judgeCurrent; //SLide / Wifi Center
    public int judgeL_Current; //Wifi Left
    public int judgeR_Current; //Wifi Right

    // Animation state
    public float fadeInStartTiming;
    public float fadeInDuration;
    public float slideAlpha; // 0->1

    public float judgeTime; //被判定时
    public float slideOKAlpha; // 1->0
    public float brightness;

    public void Init()
    {
        starAlpha = 0;
        starScale = 0;
        process = 0;
        processIdx = 1;
        slideAlpha = 0;
        slideOKAlpha = 1f;
        brightness = 1f;
        judgeTime = float.MinValue;
        currentOn = SensorType.Invalid;
        currentOnL = SensorType.Invalid;
        currentOnR = SensorType.Invalid;


        // 计算Slide淡入时机
        // 8.0速时应当提前300ms显示Slide
        fadeInStartTiming = -3.926913f / speed;
        // Slide完全淡入时机
        // 正常情况下应为负值；速度过高将忽略淡入      
        var fadeInFinishTiming = math.min(fadeInStartTiming + 0.2f, 0);
        //淡入时机与正解帧间隔小于200ms时，加快淡入动画的播放速度，这个过程现在是自然实现的        
        fadeInDuration = fadeInFinishTiming - fadeInStartTiming;

        // Calc Skippable (V slides calc is in SlideTableNeo)
        // in NoteManager->Loader

        // Load Skin
        if (!isWifi)
        {
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
        else
        {
            slideSprite = WIFI_0;
            starSprite = STAR;
            if (isEach)
            {
                slideSprite = WIFI_EACH_0;
                starSprite = STAR_EACH;
            }
            if (isBreak)
            {
                slideSprite = WIFI_BREAK_0;
                starSprite = STAR_BREAK;
            }
            if (isMine)
            {
                if (isBreak)
                {
                    slideSprite = WIFI_BREAK_MINE_0;
                    starSprite = STAR_BREAK_MINE;
                }
                else
                {
                    slideSprite = WIFI_MINE_0;
                    starSprite = STAR_MINE;
                }
            }

            //FOR WIFI STARS
            var endPosC = endPos;
            var endPosL = endPosC + 1 > 8 ? 1 : endPosC + 1;
            var endPosR = endPosC - 1 < 1 ? 8 : endPosC - 1;

            starPosStart = MajPos.GetBtnPos(startPos - 1);
            starPosConstC = MajPos.GetBtnPos(endPosC - 1) - starPosStart;
            starPosConstL = MajPos.GetBtnPos(endPosL - 1) - starPosStart;
            starPosConstR = MajPos.GetBtnPos(endPosR - 1) - starPosStart;
        }
    }
}
