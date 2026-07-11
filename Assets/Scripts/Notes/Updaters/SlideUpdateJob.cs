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
            ? TimeData.FakeNoteTime - TimeData.GetPositionAtTime(slide.shootTime)
            : TimeData.NoteTime - slide.shootTime;
        slide.process = math.saturate(timing / math.max(slide.LastFor, 0.001f));

        if (tapTiming <= 0)
        {
            slide.slideAlpha = math.clamp((tapTiming - slide.fadeInStartTiming) / slide.fadeInDuration, 0f, 1f);
        }
        else
        {
            slide.slideAlpha = 1f;
        }

        if (slide.isBreak) // break shine
        {
            var extra = math.max(math.sin(TimeData.GetFrame() * 0.17f) * 0.5f, 0f);
            slide.brightness = 0.95f + extra;
        }

        if (timing <= 0)
        {
            slide.starAlpha = math.saturate(tapTiming / (slide.shootTime - slide.tapTime));
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

        var color = new float4(1, 1, 1, slide.slideAlpha);

        var sortTime = (uint)math.clamp(slide.tapTime * 100f, 0f, 0xFFFFF);
        var timePart = slide.legacySlideLayer ? (0xFFFFFu - sortTime) : sortTime;

        // 现在 wifi 也含路径起终点了
        // 第一个是路径起点，最后一个是路径终点，忽略不画，倒数第二个要看情况
        var startIdx = slide.eaten + 1;
        var endIdx = slide.noLastArrow? cnt - 2 : cnt - 1;
        var writeCount = math.max(0, endIdx - startIdx);

        if (writeCount <= 0) return;

        var idx = Interlocked.Add(ref *SlidesWriteCountPtr, writeCount) - writeCount;
        for (var i = startIdx; i < endIdx; i++)
        {
            var p = slide.slideArrows[i];

            slidesRender[idx + i - startIdx] = new SimpleRenderData
            {
                pos = new float2(p.X, p.Y),
                angRad = math.radians(p.RotZ),
                scale = new float2(1, 1),
                spriteId = slide.isWifi ? slide.slideSprite + (uint)i - 1 : slide.slideSprite,
                color = color,
                brightness = slide.brightness,
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
            var idxLast = slide.slideArrowsCount - 1; //这里借助路径起终点画star

            var distance = slide.process * slide.slideArrows[idxLast].L;
            while (slide.slideArrows[slide.processIdx].L < distance && slide.processIdx < idxLast) slide.processIdx++;
            // processIdx 初值是 1 所以一定不会下溢，然后循环条件保证了不会上溢
            var idx0 = slide.processIdx - 1;
            var idx1 = slide.processIdx;
            var p0 = slide.slideArrows[idx0];
            var p1 = slide.slideArrows[idx1];
            var t = math.unlerp(p0.L, p1.L, distance);

            var starPosX = math.lerp(p0.X, p1.X, t);
            var starPosY = math.lerp(p0.Y, p1.Y, t);
            var deltaRot = math.fmod(p1.RotZ - p0.RotZ + 540f, 360f) - 180f;
            var starRot = p0.RotZ + deltaRot * t;

            var nIdx = Interlocked.Increment(ref *NotesWriteCountPtr) - 1;
            slide.starPos = new float2(starPosX, starPosY);
            notesRender[nIdx] = new NotesRenderData
            {
                pos = slide.starPos,
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
            var starPos = stackalloc float2[3]; //C, L, R   //这里不借助slideArrows，提供不了另两条的信息
            slide.starPos = starPos[0] = slide.starPosConstC * slide.process + slide.starPosStart;
            slide.starPosL = starPos[1] = slide.starPosConstL * slide.process + slide.starPosStart;
            slide.starPosR = starPos[2] = slide.starPosConstR * slide.process + slide.starPosStart;
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
        if (NoteHelper.AutoPlayMode is AutoPlayMode.Disable) return;
        if (slide.isEnd || slide.isJudged) return;
        var timing = TimeData.NoteTime - slide.shootTime;
        if (timing < -0.01f) return;

        switch (NoteHelper.AutoPlayMode)
        {
            case AutoPlayMode.Enable:
                // TODO: 平滑slide动画 == False
                slide.eaten = math.max((int)(slide.process * slide.slideArrowsCount - 2), 0);
                if (!slide.isSoundPlayed)
                {
                    NoteHelper.PlaySlideSound(SfxRequests,
                        slide.isBreak
                    );
                    slide.isSoundPlayed = true;
                }

                if (slide.LastFor - timing <= 0)
                {
                    slide.judgeGrade = JudgeGrade.LateCritical;
                    slide.isJudged = true;
                    CompleteSlide(ref slide);
                }
                break;
            case AutoPlayMode.Random:
                // TODO: 平滑slide动画 == False
                slide.eaten = math.max((int)(slide.process * slide.slideArrowsCount - 2), 0);
                if (!slide.isSoundPlayed)
                {
                    NoteHelper.PlaySlideSound(SfxRequests,
                        slide.isBreak
                    );
                    slide.isSoundPlayed = true;
                }

                if (slide.LastFor - timing <= 0)
                {
                    var grade = (JudgeGrade)GlobalRandom.NextInt((int)JudgeGrade.FastGood, (int)JudgeGrade.Miss);
                    slide.judgeGrade = slide.isMine
                        ? (grade < JudgeGrade.FastPerfect3rd ? JudgeGrade.Miss : JudgeGrade.LateCritical)
                        : grade;
                    slide.isJudged = true;
                    CompleteSlide(ref slide);
                }
                break;
            case AutoPlayMode.DJAutoButton:
            case AutoPlayMode.DJAutoSensor:
                if (!slide.isWifi)
                {
                    InputData.HandleWorldPosition(slide.starPos);

                }
                else
                {
                    //划wifi时使用大手子
                    InputData.HandleWorldPosition((slide.starPosL + slide.starPos) / 2, MajCtx.DJAUTO_WIFI_RADIUS);
                    InputData.HandleWorldPosition((slide.starPosR + slide.starPos) / 2, MajCtx.DJAUTO_WIFI_RADIUS);
                }
                break;
        }
    }

    private void CheckUpdate(ref SlideData slide)
    {
        if (NoteHelper.AutoPlayMode is AutoPlayMode.Enable or AutoPlayMode.Random) return;
        if (slide.isEnd || slide.isJudged) return;

        var tapTiming = TimeData.NoteTime - slide.tapTime;
        if (tapTiming < -NoteHelper.SLIDE_CHECK_AHEAD_TIME_MSEC / 1000f) return; // 提前100ms接受判定

        var timing = TimeData.NoteTime - slide.shootTime;
        var remaining = slide.LastFor - timing;

        // var stayTime = slide.LastFor * slide.Const;
        // if (slide.usingSV)
        // {
        //     var endPos = TimeData.GetPositionAtTime(slide.shootTime + slide.LastFor);
        //     var judgePos = TimeData.GetPositionAtTime(slide.shootTime + slide.LastFor * (1f - slide.Const));
        //     stayTime = endPos - judgePos;
        // }
        // 星星 miss 的时间点在结束后 +150ms
        var forceJudge = timing - slide.LastFor - NoteHelper.SLIDE_FORCE_MISS / 1000f;

        bool timeout = slide.isMine ? (remaining <= -NoteHelper.MINE_END_SEC) : (forceJudge >= 0);

        if (timeout)
        {
            slide.judgeGrade = slide.isMine
                ? JudgeGrade.LateCritical
                : (GetRemainingAreaCount(slide) <= 1 ? JudgeGrade.LateGood : JudgeGrade.Miss);
            slide.isJudged = true;
            CompleteSlide(ref slide);
            return;
        }

        if (!slide.isWifi)
        {
            ProcessAreas(ref slide, slide.judgeQueue, slide.judgeQueueCount, ref slide.judgeCurrent, ref slide.currentOn);
        }
        else
        {
            ProcessAreas(ref slide, slide.judgeQueue, slide.judgeQueueCount, ref slide.judgeCurrent, ref slide.currentOn);
            ProcessAreas(ref slide, slide.judgeQueueL, slide.judgeQueueLCount, ref slide.judgeL_Current, ref slide.currentOnL);
            ProcessAreas(ref slide, slide.judgeQueueR, slide.judgeQueueRCount, ref slide.judgeR_Current, ref slide.currentOnR);
        }

        if (!slide.isWifi)
        {
            if (slide.judgeCurrent >= slide.judgeQueueCount)
            {
                slide.judgeGrade = CalcSlideJudgeGrade(ref slide);
                CompleteSlide(ref slide);
                return;
            }

            slide.eaten = (slide.currentOn >= SensorType.A1) ? slide.judgeQueue[slide.judgeCurrent].ArrowProgressPush
                : (slide.judgeCurrent > 0) ? slide.judgeQueue[slide.judgeCurrent - 1].ArrowProgressFinish
                : 0;
        }
        else
        {
            if (slide.judgeCurrent >= slide.judgeQueueCount &&
                slide.judgeL_Current >= slide.judgeQueueLCount &&
                slide.judgeR_Current >= slide.judgeQueueRCount)
            {
                slide.judgeGrade = CalcSlideJudgeGrade(ref slide);
                CompleteSlide(ref slide);
                return;
            }

            var eatenC = (slide.currentOn >= SensorType.A1) ? slide.judgeQueue[slide.judgeCurrent].ArrowProgressPush
                : (slide.judgeCurrent > 0) ? slide.judgeQueue[slide.judgeCurrent - 1].ArrowProgressFinish
                : 0;
            var eatenL = (slide.currentOnL >= SensorType.A1) ? slide.judgeQueueL[slide.judgeL_Current].ArrowProgressPush
                : (slide.judgeL_Current > 0) ? slide.judgeQueueL[slide.judgeL_Current - 1].ArrowProgressFinish
                : 0;
            var eatenR = (slide.currentOnR >= SensorType.A1) ? slide.judgeQueueR[slide.judgeR_Current].ArrowProgressPush
                : (slide.judgeR_Current > 0) ? slide.judgeQueueR[slide.judgeR_Current - 1].ArrowProgressFinish
                : 0;
            slide.eaten = math.min(eatenC, math.min(eatenL, eatenR));
        }
    }

    // 检查 area 队列，更新 sensor On/Off 状态并推进游标
    private void ProcessAreas(ref SlideData slide, SlideArea* queue, int queueCount, ref byte cur, ref SensorType currentOn)
    {
        if (cur >= queueCount) return;
        
        var changed = false;
        do
        {
            changed = false;
            
            var first = queue[cur];
            var hasSecond = cur + 1 < queueCount;
            
            // 先看当前第一个区
            if (currentOn <= SensorType.Invalid)  // 第一个区还没按
            {
                if (MajBurst.InputData.GetSensorState(first.SensorA).Status)
                {
                    currentOn = first.SensorA;
                    changed = true;
                    if (!hasSecond) cur++;  // 最后一个区不需要松手
                }
                else if (first.SensorB >= SensorType.A1 && MajBurst.InputData.GetSensorState(first.SensorB).Status)
                {
                    currentOn = first.SensorB;
                    changed = true;
                    if (!hasSecond) cur++;  // 最后一个区不需要松手
                }
            }
            else // 第一个区已经按下了
            {
                if (!MajBurst.InputData.GetSensorState(currentOn).Status)
                {
                    currentOn = SensorType.Invalid;
                    changed = true;
                    cur++;
                }
            }
            
            // 然后看当前第二个区，注意当第一个区已经按下时一定可以跳区
            var skippable = (cur != slide.unskippable1 && cur != slide.unskippable2 || currentOn >= SensorType.A1);
            if (!changed && hasSecond && skippable)
            {
                var second = queue[cur + 1];
                var isSecondLast = cur + 2 >= queueCount;
                if (MajBurst.InputData.GetSensorState(second.SensorA).Status)
                {
                    currentOn = second.SensorA;
                    changed = true;
                    cur++;
                    if (isSecondLast) cur++;  // 最后一个区不需要松手
                }
                else if (second.SensorB >= SensorType.A1 && MajBurst.InputData.GetSensorState(second.SensorB).Status)
                {
                    currentOn = second.SensorB;
                    changed = true;
                    cur++;
                    if (isSecondLast) cur++;  // 最后一个区不需要松手
                }
            }

            if (changed && !slide.isSoundPlayed)
            {
                NoteHelper.PlaySlideSound(SfxRequests,
                    slide.isBreak
                );
                slide.isSoundPlayed = true;
            }
        } while (changed && cur < queueCount);

        if (cur >= queueCount)
        {
            currentOn = SensorType.Invalid;
            cur = (byte)queueCount;
        }
    }

    private JudgeGrade CalcSlideJudgeGrade(ref SlideData slide)
    {
        if (slide.isMine)
        {
            return JudgeGrade.Miss;
        }

        var stayTime = slide.LastFor * slide.Const;
        var judgeTiming = slide.shootTime + slide.LastFor * (1f - slide.Const);

        if (slide.usingSV)
        {
            var endPos = TimeData.GetPositionAtTime(slide.shootTime + slide.LastFor);
            judgeTiming = TimeData.GetPositionAtTime(judgeTiming);
            stayTime = endPos - judgeTiming;
        }

        var triggerTime = slide.usingSV ? TimeData.FakeNoteTime : TimeData.NoteTime;

        const float totalInterval = 36f / 60; // 秒
        const float nPInterval = 14f / 60; // Perfect基础区间
        const float gr1Interval = 21f / 60;
        const float gr2Interval = 25f / 60;
        const float gr3Interval = 29f / 60;

        var extInterval = stayTime / 4f;           // Perfect额外区间
        var pInterval = math.min(nPInterval + extInterval, totalInterval);// Perfect总区间

        var diff = judgeTiming - triggerTime;
        var isFast = diff > 0;
        diff = math.abs(diff);

        if (diff <= pInterval)
            return isFast ? JudgeGrade.FastCritical : JudgeGrade.LateCritical;
        if (diff <= gr1Interval)
            return isFast ? JudgeGrade.FastGreat1st : JudgeGrade.LateGreat1st;
        if (diff <= gr2Interval)
            return isFast ? JudgeGrade.FastGreat2nd : JudgeGrade.LateGreat2nd;
        if (diff <= gr3Interval)
            return isFast ? JudgeGrade.FastGreat3rd : JudgeGrade.LateGreat3rd;
        return isFast ? JudgeGrade.FastGood : JudgeGrade.LateGood;
    }

    private void CompleteSlide(ref SlideData slide)
    {
        slide.judgeTime = TimeData.NoteTime;
        slide.isJudged = true;
        NoteHelper.PlaySlideEndSound(SfxRequests,
            slide.judgeGrade,
            slide.isMine,
            slide.isBreak
        );
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
            >= JudgeGrade.FastPerfect3rd and <= JudgeGrade.LatePerfect3rd => 0,
            JudgeGrade.FastGreat1st or JudgeGrade.FastGreat2nd or JudgeGrade.FastGreat3rd => 6,
            JudgeGrade.FastGood => 12,
            JudgeGrade.LateGreat1st or JudgeGrade.LateGreat2nd or JudgeGrade.LateGreat3rd => 18,
            JudgeGrade.LateGood => 24,
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
            brightness = slide.brightness,  // TODO: shine
            sort = 0u,
        };
    }

    private void EndNote(ref SlideData slide)
    {
        slide.isEnd = true;
    }
}
