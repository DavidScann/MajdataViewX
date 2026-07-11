using MajSimai;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.UIElements;
using static MajBurst;
using static NoteSkinManager;

[BurstCompile]
public unsafe struct HoldUpdateJob : IJobParallelFor
{
    public NativeArray<HoldData> holds;

    [NativeDisableParallelForRestriction]
    public NativeArray<LineRenderData> tapLinesRender;
    [NativeDisableParallelForRestriction]
    public NativeArray<NotesRenderData> notesRender;

    [NativeDisableUnsafePtrRestriction]
    public int* TapLinesWriteCountPtr;
    [NativeDisableUnsafePtrRestriction]
    public int* NotesWriteCountPtr;

    [NativeDisableUnsafePtrRestriction]
    public bool* SfxRequests;
    [NativeDisableUnsafePtrRestriction]
    public EffectData* JudgeEffectRequests;
    public NativeList<ReportResultEntry>.ParallelWriter ReportResults;

    public void Execute(int index)
    {
        ref var hold = ref holds.ElementRef(index);
        TransformUpdate(ref hold, index);
        AutoplayUpdate(ref hold);
        CheckUpdate(ref hold);
    }

    private void TransformUpdate(ref HoldData hold, int index)
    {
        if (hold.isEnd) return;

        var noteTime = TimeData.NoteTime;

        // ---- body ----
        var headTiming = hold.usingSV
            ? TimeData.FakeNoteTime - TimeData.GetPositionAtTime(hold.time)
            : noteTime - hold.time;
        var headDistance = headTiming * hold.speed + 4.8f;
        var clampedDistance = math.max(headDistance, 1.225f);

        var destScale = math.min(headDistance * 0.4f + 0.51f, 1f);
        var lineScale = math.min(clampedDistance / 4.8f, 1f);

        // ---- Tail (hold end) ----
        var tailTiming = hold.usingSV
            ? TimeData.FakeNoteTime - TimeData.GetPositionAtTime(hold.time + hold.LastFor)
            : noteTime - (hold.time + hold.LastFor);
        var tailDistance = tailTiming * hold.speed + 4.8f;

        // ---- Invisible ----
        if (destScale < 0f) return;

        var sortTime = (uint)math.clamp(hold.time * 100f, 0f, 0xFFFFF);

        // ---- shine ----
        if (hold.isBreak)           // break shine
        {
            var extra = math.max(math.sin(TimeData.GetFrame() * 0.17f) * 0.5f, 0f);
            hold.brightness = 0.95f + extra;
        }
        else if (hold.isHolding)    // on shine
        {
            var frame = TimeData.GetFrame() % 16;
            var extra = (1f - math.abs(frame - 8f) / 8f) * 0.5f; //0->0.5->0
            hold.brightness = 1f + extra;
        }
        else
        {
            hold.brightness = 1f;
        }

        // ---- hold effect ----
        NoteHelper.SetHoldEffect(JudgeEffectRequests,
            (int)hold.Key,
            hold.judgeGrade,
            hold.isHolding
        );

        // ---- hold on/off skin ----
        if (hold.LastFor > 0.3f && // 忽略短hold
            headTiming >= 0.1f &&  // 忽略头6帧
            !hold.isMine)          // 忽略mine
        {
            if (hold.isHolding)
            {
                if (hold.isBreak)
                {
                    hold.bodySprite = HOLD_BREAK_ON;
                }
                else if (hold.isEach)
                {
                    hold.bodySprite = HOLD_EACH_ON;
                }
                else if (hold.isMine)
                {
                    if (hold.isBreak)
                        hold.bodySprite = HOLD_BREAK_MINE_ON;
                    else
                        hold.bodySprite = HOLD_MINE_ON;
                }
                else
                {
                    hold.bodySprite = HOLD_ON;
                }
            }
            else
            {
                hold.bodySprite = HOLD_OFF;
            }
        }

        // show line
        if (destScale > 0.3f)
        {
            var lineIdx = Interlocked.Increment(ref *TapLinesWriteCountPtr) - 1;
            tapLinesRender[lineIdx] = new LineRenderData
            {
                angRad = math.radians(hold.ang),
                scale = lineScale,
                spriteId = hold.lineSprite,
                sort = sortTime,
            };
        }

        // ---- body ----
        if (headDistance < 1.225f)
        {
            NoteHelper.GetPosFromDistance(1.225f, hold.Key, out var pos);
            hold.pos = pos;
            hold.scale = destScale;
            hold.stretchY = -0.58f; //原图带有一定高度
            hold.holdEndScale = 0f;
        }
        else
        {
            var headClamped = math.min(headDistance, 4.8f);
            var tailClamped = math.clamp(tailDistance, 1.225f, 4.8f);
            var barLen = math.max(headClamped - tailClamped, 0f);
            var midDist = (headClamped + tailClamped) * 0.5f;

            NoteHelper.GetPosFromDistance(midDist, hold.Key, out var pos);
            hold.pos = pos;
            hold.scale = 1;
            hold.stretchY = barLen - 0.58f;

            if (tailDistance >= 1.225f)
            {
                NoteHelper.GetPosFromDistance(math.min(tailDistance, 4.8f), hold.Key, out var endPos);
                hold.holdEndPos = endPos;
                hold.holdEndScale = 1f;
            }
            else
            {
                hold.holdEndScale = 0f;
            }
        }

        // ---- Write body ----
        {
            var noteIdx = Interlocked.Increment(ref *NotesWriteCountPtr) - 1;
            notesRender[noteIdx] = new NotesRenderData
            {
                pos = hold.pos,
                angRad = math.radians(hold.ang),
                scale = hold.scale,
                stretchY = hold.stretchY,
                spriteId = hold.bodySprite,
                color = new float4(1, 1, 1, 1),
                brightness = hold.brightness,
                exSprite = hold.isEx ? hold.exSprite : 0u,
                exColor = hold.exColor,
                sliceBorder = hold.sliceBorder,
                sort = sortTime,
            };
        }

        // ---- Write holdEnd
        if (hold.holdEndScale > 0f)
        {
            var endIdx = Interlocked.Increment(ref *NotesWriteCountPtr) - 1;
            notesRender[endIdx] = new NotesRenderData
            {
                pos = hold.holdEndPos,
                angRad = math.radians(hold.ang),
                scale = 1f,
                stretchY = 0,
                spriteId = hold.endSprite,
                color = new float4(1, 1, 1, 1),
                brightness = 1f,
                exSprite = 0,
                exColor = float4.zero,
                sliceBorder = float2.zero,
                sort = sortTime,
            };
        }
    }

    private void AutoplayUpdate(ref HoldData hold)
    {
        if (hold.isEnd) return;
        if (NoteHelper.AutoPlayMode is AutoPlayMode.Disable) return;

        var timing = TimeData.NoteTime - hold.time;
        if (timing < -0.01f) return;

        switch (NoteHelper.AutoPlayMode)
        {
            case AutoPlayMode.Enable:
                if (!hold.isHeadJudged)
                {
                    hold.judgeGrade = hold.isMine ? JudgeGrade.Miss : JudgeGrade.LateCritical;
                    hold.isHeadJudged = true;
                    hold.isHolding = true;
                    hold.headDiff = 0;
                    NoteHelper.PlayHoldSound(SfxRequests,
                        hold.judgeGrade,
                        hold.isBreak,
                        hold.isEx,
                        hold.isMine,
                        hold.headDiff
                    );
                }
                if (hold.isHeadJudged && math.max(hold.LastFor - timing, 0) <= 0)
                {
                    hold.holdPercent = 1f;
                    EndNote(ref hold);
                }
                break;
            case AutoPlayMode.Random:
                if (!hold.isHeadJudged)
                {
                    // TODO:这谁写的random？
                    var grade = (JudgeGrade)(new Random(114514).NextInt((int)JudgeGrade.FastGood, (int)JudgeGrade.Miss));
                    hold.judgeGrade = hold.isMine
                        ? (grade < JudgeGrade.FastPerfect3rd ? JudgeGrade.Miss : JudgeGrade.LateCritical)
                        : grade;
                    hold.isHeadJudged = true;
                    hold.isHolding = true;
                    hold.headDiff = grade >= JudgeGrade.LateCritical ? 11.4514f : -11.4514f;
                    NoteHelper.PlayHoldSound(SfxRequests,
                        hold.judgeGrade,
                        hold.isBreak,
                        hold.isEx,
                        hold.isMine,
                        hold.headDiff
                    );
                }
                if (hold.isHeadJudged && hold.LastFor - timing <= 0)
                {
                    hold.holdPercent = 1f;
                    EndNote(ref hold);
                }
                break;
            case AutoPlayMode.DJAutoButton:
                if (!hold.isHeadJudged || math.max(hold.LastFor - timing, 0) > 0)
                {
                    InputData.DJAutoSetButtonOn(hold.Key);
                }
                break;
            case AutoPlayMode.DJAutoSensor:
                if (!hold.isHeadJudged || math.max(hold.LastFor - timing, 0) > 0)
                {
                    InputData.DJAutoSetSensorOn(hold.Key);
                }
                break;
        }
    }

    private void CheckUpdate(ref HoldData hold)
    {
        if (hold.isEnd) return;
        if (!NoteHelper.IsSimulated) return;

        var noteTime = TimeData.NoteTime;
        var timing = noteTime - hold.time;

        // ---- Head judgment ----
        if (!hold.isHeadJudged)
        {
            if (hold.isMine && timing >= 0.016667f)
            {
                hold.judgeGrade = JudgeGrade.LateCritical;
                hold.isHeadJudged = true;
                return;
            }
            if (!hold.isMine && timing > 0.15f)
            {
                hold.judgeGrade = JudgeGrade.Miss;
                hold.isHeadJudged = true;
                hold.headDiff = 0.15f;
                // NOTE: DO NOT EndNote here. A missed head keeps tracking the body and
                // can still be recovered to LateGood by the release-percent mapping below.
                return;
            }

            var buttonOn = InputData.GetButtonState(hold.Key).Status;
            var sensorOn = InputData.GetSensorState(hold.Key).Status;
            var buttonClicked = buttonOn && !hold.ButtonLastState;
            var sensorClicked = sensorOn && !hold.SensorLastState;
            hold.ButtonLastState = buttonOn;
            hold.SensorLastState = sensorOn;

            var clicked = sensorClicked || buttonClicked;

            if (!clicked) return;
            if (timing < -NoteHelper.TAP_JUDGE_GOOD_AREA_MSEC / 1000f) return;
            var canJudge =
                (buttonClicked && InputData.CanJudgeButton(hold.Key, hold.ButtonOrderIndex)) ||
                (sensorClicked && InputData.CanJudgeSensor(hold.Key, hold.SensorOrderIndex));
            if (!canJudge) return;

            hold.judgeGrade = NoteHelper.GetTapJudge(timing, hold.isEx);
            hold.isHeadJudged = true;
            hold.headDiff = timing;
            NoteHelper.PlayHoldSound(SfxRequests,
                hold.judgeGrade,
                hold.isBreak,
                hold.isEx,
                hold.isMine,
                hold.headDiff
            );
            return;
        }

        // ---- Hold tracking ----
        var remainingTime = math.max(hold.LastFor - timing, 0);
        if (remainingTime <= 0)
        {
            var realityHT = hold.LastFor - 0.3f - math.max(hold.headDiff, 0f);
            var pct = math.clamp((realityHT - hold.playerIdleTime) / math.max(realityHT, 0.001f), 0f, 1f);
            hold.holdPercent = pct;
            if (!hold.isMine)
                hold.judgeGrade = NoteHelper.GetHoldFinalGrade(hold.judgeGrade, pct, realityHT);
            EndNote(ref hold);
            return;
        }

        if (!TimeData.IsStart) return;

        var on = InputData.GetSensorState(hold.Key).Status || InputData.GetButtonState(hold.Key).Status;
        hold.isHolding = on;
        if (timing > 0.25f && remainingTime > 0.2f && !on)
            hold.playerIdleTime += TimeData.deltaTime;
    }

    private void EndNote(ref HoldData hold)
    {
        NoteHelper.PlayTapSound(SfxRequests,
            hold.judgeGrade,
            false,
            false,
            false,
            0
        );
        NoteHelper.PlayTapEffect(JudgeEffectRequests,
            (int)hold.Key,
            hold.judgeGrade,
            hold.isBreak
        );
        NoteHelper.ReportResult(ReportResults,
            hold.judgeGrade,
            hold.isBreak,
            SimaiNoteType.Hold
        );
        MajBurst.InputData.NextTapHold(
            hold.Key
        );
        hold.isEnd = true;
    }
}