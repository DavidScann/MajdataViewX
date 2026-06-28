#pragma warning disable CS8500
using System.Threading;
using MajSimai;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

using static NoteSkinManager;

[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
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
    public bool isSpecialFlip;
    public bool smoothSlideAnime;
    public bool legacySlideLayer;

    public byte shapeIndex;

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

    public float slideOKFadeOutProgress; // 1→0, fade-out alpha for slideOK (Just_curv animator)
    public float judgeTime;          // Time when slide was judged (for fade-out calculation)

    // Fade animation constants
    public const float SlideOKFadeOutDuration = 0.5f; // Duration of Just_curv fade-out

    public void Init()
    {
        starAlpha = 0;
        starScale = 0;
        process = 0;
        slideAlpha = 0;
        slideOKFadeOutProgress = 1f;
        judgeTime = float.MinValue;

        ang = isMirror ? -45f * startPosition : -45f * (startPosition - 1);

        // 计算Slide淡入时机
        // 在8.0速时应当提前300ms显示Slide
        fadeInStartTiming = -3.926913f / speed;
        // Slide完全淡入时机
        // 正常情况下应为负值；速度过高将忽略淡入
        var fadeInFinishTiming = math.min(fadeInStartTiming + 0.2f, 0);
        //淡入时机与正解帧间隔小于200ms时，加快淡入动画的播放速度，这个过程现在是自然的
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

[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
public unsafe struct SlideUpdateJob : IJobParallelFor
{
    [NativeDisableUnsafePtrRestriction]
    public BurstTimeData* TimeDataPtr;
    public NativeArray<SlideData> slides;

    [NativeDisableParallelForRestriction]
    public NativeArray<SimpleRenderData> slidesRender;
    [NativeDisableParallelForRestriction]
    public NativeArray<NotesRenderData> notesRender;

    [NativeDisableUnsafePtrRestriction]
    public int* SlidesWriteCountPtr;
    [NativeDisableUnsafePtrRestriction]
    public int* NotesWriteCountPtr;

    [NativeDisableParallelForRestriction]
    public SlideTableStore SlideTable;

    public void Execute(int index)
    {
        var slide = slides[index];
        TransformUpdate(ref slide, index);
        AutoplayUpdate(ref slide);
        CheckUpdate(ref slide, index);
        slides[index] = slide;
    }

    private void TransformUpdate(ref SlideData slide, int index)
    {
        if (slide.isEnd) return;

        var tapTiming = slide.usingSV
            ? TimeDataPtr->FakeNoteTime - TimeDataPtr->GetPositionAtTime(slide.tapTime)
            : TimeDataPtr->NoteTime - slide.tapTime;
        if (tapTiming - slide.fadeInStartTiming < 0) return;
        var timing = slide.usingSV
            ? TimeDataPtr->FakeNoteTime - TimeDataPtr->GetPositionAtTime(slide.time)
            : TimeDataPtr->NoteTime - slide.time;
        slide.process = math.saturate(timing / math.max(slide.LastFor, 0.001f));

        if (tapTiming <= 0)
        {
            slide.slideAlpha = math.clamp((tapTiming - slide.fadeInStartTiming) / slide.fadeInDuration, 0f, 0.55f);
        }
        else
        {
            slide.slideAlpha = 1f;
        }

        if (timing <= 0)
        {
            slide.starAlpha = math.saturate(tapTiming / (slide.time - slide.tapTime));
            slide.starScale = slide.starAlpha + 0.5f;
        }
        else
        {
            slide.starAlpha = 1f;
            slide.starScale = 1.5f;
        }
        UpdateStarPosition(ref slide);

        RenderArrows(ref slide, index);
        RenderStar(ref slide, index);
        if (slide.isJudged) RenderSlideOK(ref slide);
    }

    private void UpdateStarPosition(ref SlideData slide)
    {
        var cnt = SlideTable.Shapes[slide.shapeIndex].ArrowCount;
        var off = SlideTable.Shapes[slide.shapeIndex].ArrowOffset;
        var idxF = slide.process * (cnt - 1);
        var idx0 = (int)idxF;
        var idx1 = math.min(idx0 + 1, cnt - 1);
        var t = idxF - idx0;

        var p0 = SlideTable.ArrowPoses[off + idx0];
        var p1 = SlideTable.ArrowPoses[off + idx1];

        var ca = math.cos(math.radians(slide.ang));
        var sa = math.sin(math.radians(slide.ang));
        var lx = math.lerp(p0.X, p1.X, t) * (slide.isMirror ? -1f : 1f);
        var ly = math.lerp(p0.Y, p1.Y, t);
        slide.starPosX = lx * ca - ly * sa;
        slide.starPosY = lx * sa + ly * ca;
        slide.starRot = slide.isMirror
            ? -math.lerp(p0.RotZ, p1.RotZ, t) + 180f
            : math.lerp(p0.RotZ, p1.RotZ, t);
    }

    private void RenderArrows(ref SlideData slide, int index)
    {
        var cnt = SlideTable.Shapes[slide.shapeIndex].ArrowCount;
        var off = SlideTable.Shapes[slide.shapeIndex].ArrowOffset;
        //TODO
        var eaten = math.max((int)(slide.process * cnt) - 1, 0);

        var color = new float4(1, 1, 1, slide.slideAlpha);

        var ca = math.cos(math.radians(slide.ang));
        var sa = math.sin(math.radians(slide.ang));

        for (int i = eaten; i < cnt; i++)
        {
            var p = SlideTable.ArrowPoses[off + i];
            var lx = p.X * (slide.isMirror ? -1f : 1f);
            var ly = p.Y;
            var wx = lx * ca - ly * sa;
            var wy = lx * sa + ly * ca;

            var idx = Interlocked.Increment(ref *SlidesWriteCountPtr) - 1;
            slidesRender[idx] = new SimpleRenderData
            {
                pos = new float2(wx, wy),
                angRad = math.radians(slide.isMirror ? -p.RotZ + slide.ang + 180f : p.RotZ + slide.ang),
                scale = new float2(1, 1),
                spriteId = slide.slideSprite,
                color = color,
                sort = (uint)(index + i * 0x100u),
            };
        }
    }

    private void RenderStar(ref SlideData slide, int index)
    {
        if (slide.starAlpha <= 0) return;
        var nIdx = Interlocked.Increment(ref *NotesWriteCountPtr) - 1;
        notesRender[nIdx] = new NotesRenderData
        {
            pos = new float2(slide.starPosX, slide.starPosY),
            angRad = math.radians(slide.starRot + 90),
            scale = new float2(slide.starScale, slide.starScale),
            spriteId = slide.starSprite,
            color = new float4(1, 1, 1, slide.starAlpha),
            brightness = 1f,
            exSprite = 0,
            exColor = float4.zero,
            sliceBorder = new float2(0, 0),
            sort = (uint)index,
        };
    }

    private void AutoplayUpdate(ref SlideData slide)
    {
        if (slide.isEnd) return;
        var timing = TimeDataPtr->NoteTime - slide.time;
        if (timing < -0.01f) return;
        if (math.max(slide.LastFor - timing, 0) <= 0)
        {
            switch (NoteHelper.AutoPlayMode)
            {
                case AutoPlayMode.Enable:
                    slide.judgeGrade = JudgeGrade.Perfect;
                    slide.isJudged = true;
                    EndNote(ref slide);
                    break;
                case AutoPlayMode.Random:
                    slide.judgeGrade = (JudgeGrade)new Random(114514).NextInt(1, 14);
                    slide.isJudged = true;
                    EndNote(ref slide);
                    break;
            }
        }
    }

    private void CheckUpdate(ref SlideData slide, int index)
    {
        if (NoteHelper.AutoPlayMode is AutoPlayMode.Enable or AutoPlayMode.Random) return;
        if (slide.isEnd) return;

        var timing = TimeDataPtr->NoteTime - slide.time;
        var remaining = math.max(slide.LastFor - timing, 0);

        // too late
        if (remaining <= 0)
        {
            slide.judgeGrade = GetRemainingAreaCount(slide) <= 1 ? JudgeGrade.LateGood : JudgeGrade.Miss;
            slide.isJudged = true;
            EndNote(ref slide);
            return;
        }

        if (!slide.isWifi) CheckSingleQueue(ref slide);
        else CheckWifiQueues(ref slide);

        // Check if all areas are finished → judge
        if (slide.isWifi)
        {
            ref readonly var w = ref SlideTable.Wifi;
            if (slide.judgeCurrent >= w.LeftCount &&
            slide.judgeC_Current >= w.CenterCount &&
            slide.judgeR_Current >= w.RightCount)
                CompleteSlide(ref slide);
        }
        else
        {
            if (slide.judgeCurrent >= SlideTable.Shapes[slide.shapeIndex].AreaCount)
                CompleteSlide(ref slide);
        }
    }

    private void CheckSingleQueue(ref SlideData slide)
    {
        ref readonly var td = ref SlideTable.Shapes[slide.shapeIndex];
        ProcessAreas(ref slide, td.AreaOffset, td.AreaCount, ref slide.judgeCurrent);
    }

    private void CheckWifiQueues(ref SlideData slide)
    {
        ref var w = ref SlideTable.Wifi;
        ProcessAreas(ref slide, w.LeftOffset, w.LeftCount, ref slide.judgeCurrent);
        ProcessAreas(ref slide, w.CenterOffset, w.CenterCount, ref slide.judgeC_Current);
        ProcessAreas(ref slide, w.RightOffset, w.RightCount, ref slide.judgeR_Current);
    }

    // 检查 area 队列，更新 sensor On/Off 状态并推进游标
    private void ProcessAreas(ref SlideData slide, int offset, byte count, ref byte cur)
    {
        if (cur >= count) return;

        ref var first = ref SlideTable.Areas[offset + cur];
        var hasSecond = cur + 1 < count;

        CheckArea(ref first);
        if (first.On && !slide.isSoundPlayed)
        {
            NoteHelper.PlaySlideSound(slide.isBreak);
            slide.isSoundPlayed = true;
        }

        if (hasSecond && (first.IsSkippable || first.On))
        {
            ref var second = ref SlideTable.Areas[offset + cur + 1];
            CheckArea(ref second);

            if (second.IsFinished) { cur += 2; return; }
            if (second.On) { cur += 1; return; }
        }

        if (first.IsFinished) cur++;
    }

    // 检查 sensor，更新 On/Off
    private static void CheckArea(ref SlideAreaData area)
    {
        for (int i = 0; i < area.AreaCount; i++)
        {
            var sensor = i == 0 ? area.Area0 : area.Area1;
            area.Judge(NoteHelper.SensorStates[(int)sensor].Status == SensorStatus.On);
        }
    }

    private void CompleteSlide(ref SlideData slide)
    {
        slide.judgeGrade = JudgeGrade.Perfect;
        slide.isJudged = true;
        if (slide.isBreak) NoteHelper.PlayBreakSlideEndSound();
        EndNote(ref slide);
    }

    private int GetRemainingAreaCount(SlideData slide)
    {
        if (!slide.isWifi)
        {
            return SlideTable.Shapes[slide.shapeIndex].AreaCount - slide.judgeCurrent;
        }
        else
        {
            ref readonly var w = ref SlideTable.Wifi;
            return w.LeftCount - slide.judgeCurrent +
            w.CenterCount - slide.judgeC_Current +
            w.RightCount - slide.judgeR_Current;
        }
    }

    private void EndNote(ref SlideData slide)
    {
        NoteHelper.ReportResult(slide.judgeGrade, slide.isBreak, SimaiNoteType.Slide);
        slide.isEnd = true;
    }

    private void RenderSlideOK(ref SlideData slide)
    {
        var ok = SlideTable.Shapes[slide.shapeIndex].OK;

        var ca = math.cos(math.radians(slide.ang));
        var sa = math.sin(math.radians(slide.ang));
        var lx = ok.X * (slide.isMirror ? -1f : 1f);
        var ly = ok.Y;
        var wx = lx * ca - ly * sa;
        var wy = lx * sa + ly * ca;

        var okRotZ = ok.RotZ;
        if (slide.isMirror) okRotZ += 180f;

        // isJustR flip: for non-wifi curv slides, the sprite may need 180° flip + shift
        if (!slide.isWifi)
        {
            bool needsFlip;
            if (slide.isJustR) needsFlip = slide.isMirror;
            else needsFlip = !slide.isMirror;
            if (needsFlip)
            {
                okRotZ += 180f;
                var flipRad = math.radians(okRotZ);
                wx += math.sin(flipRad) * 0.27f;
                wy += math.cos(flipRad) * -0.27f;
            }
        }

        var baseJ = slide.isWifi ? JUST_2 : (slide.isJustR ? JUST_0 : JUST_3);
        var off = slide.judgeGrade switch
        {
            JudgeGrade.Perfect or JudgeGrade.LatePerfect2nd or JudgeGrade.FastPerfect2nd or JudgeGrade.LatePerfect3rd or JudgeGrade.FastPerfect3rd => 0,
            JudgeGrade.LateGreat or JudgeGrade.FastGreat or JudgeGrade.LateGreat2nd or JudgeGrade.FastGreat2nd or JudgeGrade.LateGreat3rd or JudgeGrade.FastGreat3rd => 5,
            JudgeGrade.LateGood or JudgeGrade.FastGood => 11,
            _ => 23,
        };

        // SlideOK fade-out animation (Just_curv animator equivalent):
        // Record judge time on first render, then fade out from 1→0 over SlideOKFadeOutDuration
        if (slide.judgeTime < 0f) slide.judgeTime = TimeDataPtr->NoteTime;
        var elapsedFromJudge = TimeDataPtr->NoteTime - slide.judgeTime;
        slide.slideOKFadeOutProgress = math.saturate(1f - elapsedFromJudge / SlideData.SlideOKFadeOutDuration);

        var idx = Interlocked.Increment(ref *SlidesWriteCountPtr) - 1;
        slidesRender[idx] = new SimpleRenderData
        {
            pos = new float2(wx, wy),
            angRad = math.radians(okRotZ),
            scale = new float2(1, 1),
            spriteId = (uint)(baseJ + off),
            color = new float4(1, 1, 1, slide.slideOKFadeOutProgress),
            sort = 0xFFFFFF00u,
        };
    }

}
