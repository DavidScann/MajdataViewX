using System.Threading;
using MajSimai;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using static NoteSkinManager;
using static MajBurst;
using Notes.SlideUtils;

[BurstCompile]
public unsafe struct SlideUpdateJob : IJobParallelFor
{
    public NativeArray<SlideData> slides;

    [NativeDisableParallelForRestriction]
    public NativeArray<SimpleRenderData> slidesRender;
    [NativeDisableParallelForRestriction]
    public NativeArray<NotesRenderData> notesRender;

    [NativeDisableUnsafePtrRestriction]
    public int* SlidesWriteCountPtr;
    [NativeDisableUnsafePtrRestriction]
    public int* NotesWriteCountPtr;

    [NativeDisableUnsafePtrRestriction]
    public bool* SfxRequests;
    public NativeList<ReportResultEntry>.ParallelWriter ReportResults;

    public const float SlideOKKeepDuration = 17 * MajCtx.FRAME_LENGTH_SEC;
    public const float SlideOKFadeOutDuration = 8 * MajCtx.FRAME_LENGTH_SEC;

    public void Execute(int index)
    {
        ref var slide = ref slides.ElementRef(index);
        TransformUpdate(ref slide, index);
        AutoplayUpdate(ref slide);
        CheckUpdate(ref slide);
    }

    private void TransformUpdate(ref SlideData slide, int index)
    {
        if (slide.isEnd) return;
        if (slide.isJudged)
        {
            RenderSlideOK(ref slide);
            return;
        }

        var tapTiming = slide.usingSV
            ? TimeData.FakeNoteTime - TimeData.GetPositionAtTime(slide.tapTime)
            : TimeData.NoteTime - slide.tapTime;
        if (tapTiming - slide.fadeInStartTiming < 0) return;
        var timing = slide.usingSV
            ? TimeData.FakeNoteTime - TimeData.GetPositionAtTime(slide.time)
            : TimeData.NoteTime - slide.time;
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

        RenderArrows(ref slide, index);
        RenderStar(ref slide, index);
    }

    private void RenderArrows(ref SlideData slide, int index)
    {
        var cnt = slide.slideArrowsCount;
        var eaten = slide.eaten;

        var color = new float4(1, 1, 1, slide.slideAlpha);

        var sortTime = (uint)math.clamp(slide.tapTime * 100f, 0f, 0xFFFFF);
        var timePart = slide.legacySlideLayer ? (0xFFFFFu - sortTime) : sortTime;

        var writeCount = cnt - eaten;
        var idx = Interlocked.Add(ref *SlidesWriteCountPtr, writeCount) - writeCount;
        for (var i = eaten; i < cnt; i++)
        {
            ref readonly var p = ref slide.slideArrows[i];

            slidesRender[idx + i - eaten] = new SimpleRenderData
            {
                pos = new float2(p.X, p.Y),
                angRad = math.radians(p.RotZ),
                scale = new float2(1, 1),
                spriteId = slide.isWifi ? slide.slideSprite + (uint)i : slide.slideSprite,
                color = color,
                sort = (timePart << 8) | (uint)i,
            };
        }
    }

    private void RenderStar(ref SlideData slide, int index)
    {
        if (slide.starAlpha <= 0) return;

        var sortTime = (uint)math.clamp(slide.tapTime * 100f, 0f, 0xFFFFF);
        var timePart = slide.legacySlideLayer ? (0xFFFFFu - sortTime) : sortTime;
        if (!slide.isWifi)
        {
            var cnt = slide.slideArrowsCount;

            var idxF = slide.process * (cnt - 1);
            var idx0 = (int)idxF;
            var idx1 = math.min(idx0 + 1, cnt - 1);
            var t = idxF - idx0;
            var p0 = slide.slideArrows[idx0];
            var p1 = slide.slideArrows[idx1];

            var starPosX = math.lerp(p0.X, p1.X, t);
            var starPosY = math.lerp(p0.Y, p1.Y, t);
            var starRot = math.lerp(p0.RotZ, p1.RotZ, t);

            var nIdx = Interlocked.Increment(ref *NotesWriteCountPtr) - 1;
            notesRender[nIdx] = new NotesRenderData
            {
                pos = new float2(starPosX, starPosY),
                angRad = math.radians(starRot + 90),
                scale = slide.starScale,
                stretchY = 0,
                spriteId = slide.starSprite,
                color = new float4(1, 1, 1, slide.starAlpha),
                brightness = 1f,
                exSprite = 0,
                exColor = float4.zero,
                sliceBorder = new float2(0, 0),
                sort = 0x100000u + timePart,
            };
        }
        else
        {
            var starPos = stackalloc float2[3]; // L, C, R
            starPos[0] = slide.starPosConstL * slide.process + slide.starPosStart;
            starPos[1] = slide.starPosConstC * slide.process + slide.starPosStart;
            starPos[2] = slide.starPosConstR * slide.process + slide.starPosStart;
            var nIdx = Interlocked.Add(ref *NotesWriteCountPtr, 3) - 3;
            for (var i = 0; i < 3; i++)
            {
                var rotZ = slide.slideArrows[0].RotZ - 22.5f * (i - 1);
                notesRender[nIdx + i] = new NotesRenderData
                {
                    pos = starPos[i],
                    angRad = math.radians(rotZ + 90),
                    scale = slide.starScale,
                    stretchY = 0,
                    spriteId = slide.starSprite,
                    color = new float4(1, 1, 1, slide.starAlpha),
                    brightness = 1f,
                    exSprite = 0,
                    exColor = float4.zero,
                    sliceBorder = new float2(0, 0),
                    sort = 0x100000u + timePart,
                };
            }
        }
    }

    private void AutoplayUpdate(ref SlideData slide)
    {
        if (NoteHelper.AutoPlayMode is AutoPlayMode.DJAuto or AutoPlayMode.Disable) return;
        if (slide.isEnd || slide.isJudged) return;
        var timing = TimeData.NoteTime - slide.time;
        if (timing < -0.01f) return;
        slide.eaten = math.max((int)(slide.process * slide.slideArrowsCount), 0);
        if (slide.LastFor - timing <= 0)
        {
            switch (NoteHelper.AutoPlayMode)
            {
                case AutoPlayMode.Enable:
                    slide.judgeGrade = JudgeGrade.Perfect;
                    slide.isJudged = true;
                    CompleteSlide(ref slide);
                    break;
                case AutoPlayMode.Random:
                    slide.judgeGrade = (JudgeGrade)new Random(114514).NextInt(1, 14);
                    slide.isJudged = true;
                    CompleteSlide(ref slide);
                    break;
            }
        }
    }

    private void CheckUpdate(ref SlideData slide)
    {
        if (NoteHelper.AutoPlayMode is AutoPlayMode.Enable or AutoPlayMode.Random) return;
        if (slide.isEnd || slide.isJudged) return;

        var timing = TimeData.NoteTime - slide.time;
        if (timing < -0.05f) return;

        var remaining = slide.LastFor - timing;

        // too late
        if (remaining <= 0)
        {
            slide.judgeGrade = GetRemainingAreaCount(slide) <= 1 ? JudgeGrade.LateGood : JudgeGrade.Miss;
            slide.isJudged = true;
            CompleteSlide(ref slide);
            return;
        }

        if (!slide.isWifi)
        {
            ProcessAreas(ref slide, slide.judgeQueue, slide.judgeQueueCount, ref slide.judgeCurrent);
        }
        else
        {
            ProcessAreas(ref slide, slide.judgeQueue, slide.judgeQueueCount, ref slide.judgeCurrent);
            ProcessAreas(ref slide, slide.judgeQueueL, slide.judgeQueueLCount, ref slide.judgeL_Current);
            ProcessAreas(ref slide, slide.judgeQueueR, slide.judgeQueueRCount, ref slide.judgeR_Current);
        }

        if (!slide.isWifi)
        {
            if (slide.judgeCurrent >= slide.judgeQueueCount)
            {
                CompleteSlide(ref slide);
                return;
            }

            ref readonly var curArea = ref slide.judgeQueue[slide.judgeCurrent];
            if (curArea.On && !curArea.Off)
                slide.eaten = curArea.ArrowProgressPush;
            else if (slide.judgeCurrent > 0)
                slide.eaten = slide.judgeQueue[slide.judgeCurrent - 1].ArrowProgressFinish;
        }
        else
        {
            if (slide.judgeCurrent >= slide.judgeQueueCount &&
                slide.judgeL_Current >= slide.judgeQueueLCount &&
                slide.judgeR_Current >= slide.judgeQueueRCount)
            {
                CompleteSlide(ref slide);
                return;
            }

            ref readonly var curArea = ref slide.judgeQueue[slide.judgeCurrent];
            ref readonly var curAreaL = ref slide.judgeQueue[slide.judgeL_Current];
            ref readonly var curAreaR = ref slide.judgeQueue[slide.judgeR_Current];
            var min = slide.judgeCurrent;
            if (slide.judgeL_Current < min) min = slide.judgeL_Current;
            if (slide.judgeR_Current < min) min = slide.judgeR_Current;

            if (curArea.On && !curArea.Off &&
                curAreaL.On && !curAreaL.Off &&
                curAreaR.On && !curAreaR.Off)
                slide.eaten = curArea.ArrowProgressPush;
            else if (min > 0)
                slide.eaten = slide.judgeQueue[min - 1].ArrowProgressFinish;
        }
    }

    // 检查 area 队列，更新 sensor On/Off 状态并推进游标
    private void ProcessAreas(ref SlideData slide, SlideArea* queue, int queueCount, ref byte cur)
    {
        if (cur >= queueCount) return;

        ref var first = ref queue[cur];
        var hasSecond = cur + 1 < queueCount;
        var isSecondLast = hasSecond && cur + 2 >= queueCount;

        CheckArea(ref first);
        if (first.On && !slide.isSoundPlayed)
        {
            NoteHelper.PlaySlideSound(SfxRequests, slide.isBreak);
            slide.isSoundPlayed = true;
        }

        if (hasSecond && (first.IsSkippable || first.On))
        {
            ref var second = ref queue[cur + 1];
            CheckArea(ref second);

            if (second.On)
            {
                if (isSecondLast)
                {
                    cur += 2;
                }
                else
                {
                    cur += 1;
                }
                return;
            }
        }

        if (first.On)
        {
            if (!hasSecond)
            {
                cur++;
            }
            else if (first.Off)
            {
                cur++;
            }
            return;
        }
    }

    private static void CheckArea(ref SlideArea area)
    {
        var status = MajBurst.InputData.GetSensorState(area.SensorA).Status;
        if (area.SensorB != SensorType.Invalid)
        {
            status |= MajBurst.InputData.GetSensorState(area.SensorB).Status;
        }
        area.Judge(status);
    }

    private void CompleteSlide(ref SlideData slide)
    {
        slide.judgeTime = TimeData.NoteTime;
        slide.isJudged = true;
        if (slide.isBreak) NoteHelper.PlayBreakSlideEndSound(SfxRequests);
        NoteHelper.ReportResult(ReportResults, slide.judgeGrade, slide.isBreak, SimaiNoteType.Slide);
    }

    private int GetRemainingAreaCount(SlideData slide)
    {
        if (!slide.isWifi)
        {
            return slide.judgeQueueCount - slide.judgeCurrent;
        }
        else
        {
            return slide.judgeQueueCount - slide.judgeCurrent +
                    slide.judgeQueueLCount - slide.judgeL_Current +
                    slide.judgeQueueRCount - slide.judgeR_Current;
        }
    }

    private void RenderSlideOK(ref SlideData slide)
    {
        ref readonly var ok = ref slide.okPose;

        var baseJ = slide.okType switch
        {
            SlideOkType.StraightL => JUST_STR_L,
            SlideOkType.StraightR => JUST_STR_R,
            SlideOkType.CircleL => JUST_CURV_L,
            SlideOkType.CircleR => JUST_CURV_R,
            SlideOkType.WifiU => JUST_WIFI_U,
            SlideOkType.WifiD => JUST_WIFI_D,
            _ => JUST_STR_L,
        };
        var off = slide.judgeGrade switch
        {
            JudgeGrade.Perfect or JudgeGrade.LatePerfect2nd or JudgeGrade.FastPerfect2nd or JudgeGrade.LatePerfect3rd or JudgeGrade.FastPerfect3rd => 0,
            JudgeGrade.FastGreat or JudgeGrade.FastGreat2nd or JudgeGrade.FastGreat3rd => 5,
            JudgeGrade.LateGreat or JudgeGrade.LateGreat2nd or JudgeGrade.LateGreat3rd => 11,
            JudgeGrade.FastGood => 17,
            JudgeGrade.LateGood => 23,
            _ => 30,
        };

        // SlideOK fade-out animation (Just_curv animator equivalent):
        // Record judge time on first render, then fade out from 1→0 over SlideOKFadeOutDuration
        var elapsedFromJudge = TimeData.NoteTime - slide.judgeTime;

        slide.slideOKAlpha = elapsedFromJudge switch
        {
            < 0 => 0f,
            < 2 * MajCtx.FRAME_LENGTH_SEC => math.saturate(elapsedFromJudge / (2 * MajCtx.FRAME_LENGTH_SEC)),
            < 17 * MajCtx.FRAME_LENGTH_SEC => 1f,
            < 25 * MajCtx.FRAME_LENGTH_SEC => math.saturate(1f - (elapsedFromJudge - 17 * MajCtx.FRAME_LENGTH_SEC) / (8 * MajCtx.FRAME_LENGTH_SEC)),
            _ => 0f,
        };

        if (elapsedFromJudge > 25 * MajCtx.FRAME_LENGTH_SEC)
            EndNote(ref slide);

        var idx = Interlocked.Increment(ref *SlidesWriteCountPtr) - 1;
        slidesRender[idx] = new SimpleRenderData
        {
            pos = new float2(ok.X, ok.Y),
            angRad = math.radians(ok.RotZ),
            scale = new float2(1, 1),
            spriteId = (uint)(baseJ + off),
            color = new float4(1, 1, 1, slide.slideOKAlpha),
            sort = 0u,
        };
    }

    private void EndNote(ref SlideData slide)
    {
        slide.isEnd = true;
    }
}
