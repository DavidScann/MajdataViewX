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
public struct HoldData
{
    public float time;
    public SensorType key;
    public float speed;
    public float LastFor;
    public int sensorOrderIndex;

    public bool isEach;
    public bool isEx;
    public bool isBreak;
    public bool isMine;
    public bool usingSV;

    public bool isEnd;
    public float2 pos;
    public float ang;
    public float2 bodyScale;
    public float2 holdEndPos;
    public float holdEndScale;

    public uint bodySprite;
    public uint endSprite;
    public uint lineSprite;
    public uint exSprite;
    public float4 exColor;

    public float2 sliceBorder;
    public float brightness;
    public bool isJudged;
    public JudgeGrade judgeGrade;
    public float headDiff;
    public float playerIdleTime;
    public float holdPercent;
    public bool isHolding;

    public void Init()
    {
        pos = float2.zero;
        ang = -22.5f + -45f * (int)key;
        bodyScale = new float2(1, 1);
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

[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
public unsafe struct HoldUpdateJob : IJobParallelFor
{
    [NativeDisableUnsafePtrRestriction]
    public BurstTimeData* TimeDataPtr;
    public NativeArray<HoldData> holds;

    [NativeDisableParallelForRestriction]
    public NativeArray<LineRenderData> tapLinesRender;
    [NativeDisableParallelForRestriction]
    public NativeArray<NotesRenderData> notesRender;
    [NativeDisableParallelForRestriction]
    public NativeArray<SimpleRenderData> simpleRender;

    [NativeDisableUnsafePtrRestriction]
    public int* TapLinesWriteCountPtr;
    [NativeDisableUnsafePtrRestriction]
    public int* NotesWriteCountPtr;
    [NativeDisableUnsafePtrRestriction]
    public int* SimpleWriteCountPtr;

    public void Execute(int index)
    {
        var hold = holds[index];
        TransformUpdate(ref hold, index);
        AutoplayUpdate(ref hold);
        CheckUpdate(ref hold);
        holds[index] = hold;
    }

    private void TransformUpdate(ref HoldData hold, int index)
    {
        if (hold.isEnd) return;

        var noteTime = TimeDataPtr->NoteTime;

        // ---- body ----
        var headTiming = hold.usingSV
            ? TimeDataPtr->FakeNoteTime - TimeDataPtr->GetPositionAtTime(hold.time)
            : noteTime - hold.time;
        var headDistance = headTiming * hold.speed + 4.8f;

        // ---- Tail (hold end) ----
        var tailTiming = hold.usingSV
            ? TimeDataPtr->FakeNoteTime - TimeDataPtr->GetPositionAtTime(hold.time + hold.LastFor)
            : noteTime - (hold.time + hold.LastFor);
        var tailDistance = tailTiming * hold.speed + 4.8f;

        // ---- Invisible ----
        if (headDistance < 0f) return;

        // ---- Break shine ----
        if (hold.isBreak)
        {
            var extra = math.max(math.sin(TimeDataPtr->GetFrame() * 0.17f) * 0.5f, 0f);
            hold.brightness = 0.95f + extra;
        }

        // show line
        if (headDistance > 0.3f)
        {
            var lineScale = math.saturate(headDistance / 4.8f);
            var lineIdx = Interlocked.Increment(ref *TapLinesWriteCountPtr) - 1;
            tapLinesRender[lineIdx] = new LineRenderData
            {
                angRad = math.radians(hold.ang),
                scale = lineScale,
                spriteId = hold.lineSprite,
                sort = (uint)index,
            };
        }

        // ---- body ----
        var hasEnd = false;
        var sliceBorder = float2.zero;
        // 头在变大（Phase B — uniform scale, pinned at 1.225）
        if (headDistance < 1.225f)
        {
            var destScale = math.min(headDistance * 0.4f + 0.51f, 1f);
            NoteHelper.GetPosFromDistance(1.225f, hold.key, out var pos);
            hold.pos = pos;
            // desired world size = destScale * legacy sprite size (1.22 × 1.4)
            float desiredW = destScale * HoldBaseWidth;
            float desiredH = destScale * HoldCapAllowance;
            hold.bodyScale = new float2(desiredW / HoldNativeWidth, desiredH / HoldNativeHeight);
            hold.holdEndScale = 0f;
        }
        // 头到达，拉伸中（Phase C/D — stretched bar, X preserved at 1.22 world）
        else
        {
            var headClamped = math.min(headDistance, 4.8f);
            var tailClamped = math.clamp(tailDistance, 1.225f, 4.8f);
            var barLen = math.max(headClamped - tailClamped, 0f);
            var midDist = (headClamped + tailClamped) * 0.5f;

            NoteHelper.GetPosFromDistance(midDist, hold.key, out var pos);
            hold.pos = pos;
            float desiredW = HoldBaseWidth;                          // legacy fixed sprite width
            float desiredH = barLen + HoldCapAllowance;              // legacy bar world height
            hold.bodyScale = new float2(desiredW / HoldNativeWidth, desiredH / HoldNativeHeight);
            sliceBorder = hold.sliceBorder;

            if (tailDistance >= 1.225f)
            {
                // 头到达，尾出现
                NoteHelper.GetPosFromDistance(math.min(tailDistance, 4.8f), hold.key, out var endPos);
                hold.holdEndPos = endPos;
                hold.holdEndScale = 1f;
                hasEnd = true;
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
                scale = hold.bodyScale,
                spriteId = hold.bodySprite,
                color = new float4(1, 1, 1, 1),
                brightness = hold.brightness,
                exSprite = hold.isEx ? hold.exSprite : 0u,
                exColor = hold.exColor,
                sliceBorder = sliceBorder,
                sort = (uint)index,
            };
        }

        // ---- Write holdEnd
        if (hasEnd)
        {
            var endIdx = Interlocked.Increment(ref *SimpleWriteCountPtr) - 1;
            simpleRender[endIdx] = new SimpleRenderData
            {
                pos = hold.holdEndPos,
                angRad = math.radians(hold.ang),
                scale = new float2(1f, 1f),
                spriteId = hold.endSprite,
                color = new float4(1, 1, 1, 1),
                sort = (uint)index,
            };
        }
    }

    private void AutoplayUpdate(ref HoldData hold)
    {
        if (hold.isEnd) return;

        var timing = TimeDataPtr->NoteTime - hold.time;
        if (timing < -0.01f) return;

        switch (NoteHelper.AutoPlayMode)
        {
            case AutoPlayMode.Enable:
                if (!hold.isJudged)
                {
                    hold.judgeGrade = hold.isMine ? JudgeGrade.Miss : JudgeGrade.Perfect;
                    hold.isJudged = true;
                    hold.isHolding = true;
                    hold.headDiff = 0;
                }
                if (hold.isJudged && math.max(hold.LastFor - timing, 0) <= 0)
                {
                    hold.holdPercent = 1f;
                    EndNote(ref hold);
                }
                break;
            case AutoPlayMode.Random:
                if (!hold.isJudged)
                {
                    var gradeIndex = new Random(114514).NextInt(1, 14);
                    hold.judgeGrade = hold.isMine
                        ? (gradeIndex > 4 ? JudgeGrade.Miss : JudgeGrade.Perfect)
                        : (JudgeGrade)gradeIndex;
                    hold.isJudged = true;
                    hold.isHolding = true;
                    hold.headDiff = gradeIndex > 7 ? 11.4514f : -11.4514f;
                }
                if (hold.isJudged && math.max(hold.LastFor - timing, 0) <= 0)
                {
                    hold.holdPercent = 1f;
                    EndNote(ref hold);
                }
                break;
            case AutoPlayMode.DJAuto:
                if (hold.isMine) break;
                NoteHelper.SensorStates[(int)hold.key].Status = SensorStatus.On;
                break;
        }
    }

    private void CheckUpdate(ref HoldData hold)
    {
        if (NoteHelper.AutoPlayMode is AutoPlayMode.Enable or AutoPlayMode.Random) return;
        if (hold.isEnd) return;

        var noteTime = TimeDataPtr->NoteTime;
        var timing = noteTime - hold.time;
        var key = (int)hold.key;

        // ---- Head judgment ----
        if (!hold.isJudged)
        {
            if (hold.isMine && timing >= 0.016667f)
            {
                hold.judgeGrade = JudgeGrade.Perfect;
                hold.isJudged = true;
                hold.isHolding = true;
                return;
            }
            if (!hold.isMine && timing > 0.15f)
            {
                hold.judgeGrade = JudgeGrade.Miss;
                hold.isJudged = true;
                hold.headDiff = 0.15f;
                // NOTE: do NOT EndNote here. A missed head keeps tracking the body and
                // can still be recovered to LateGood by the release-percent mapping below.
                return;
            }

            var stateOn = NoteHelper.SensorStates[key].Status == SensorStatus.On;
            var stateBusy = NoteHelper.SensorStates[key].IsJudging;
            if (!stateOn || stateBusy) return;

            if (timing >= -NoteHelper.TAP_JUDGE_GOOD_AREA_MSEC / 1000f)
            {
                var orderIdx = NoteHelper.NextSensorIndex[key];
                if (orderIdx == hold.sensorOrderIndex)
                {
                    NoteHelper.SensorStates[key].IsJudging = true;
                    NoteHelper.NextSensorIndex[key] = orderIdx + 1;

                    hold.judgeGrade = NoteHelper.GetTapJudge(timing, hold.isEx);
                    hold.isJudged = true;
                    hold.isHolding = true;
                    hold.headDiff = timing;
                }
            }
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

        if (!TimeDataPtr->IsStart) return;

        var on = NoteHelper.SensorStates[key].Status == SensorStatus.On;
        if (timing > 0.25f && remainingTime > 0.2f && !on)
            hold.playerIdleTime += TimeDataPtr->deltaTime;
    }

    private void EndNote(ref HoldData hold)
    {
        NoteHelper.PlayHoldSound(new JudgeResult
        {
            Grade = hold.judgeGrade,
            IsBreak = hold.isBreak,
            IsEX = hold.isEx,
            IsMine = hold.isMine,
            Diff = hold.headDiff
        });
        NoteHelper.PlayJudgeEffect((int)hold.key, hold.judgeGrade, hold.isBreak);
        NoteHelper.PlayFastLateEffect((int)hold.key, hold.judgeGrade);
        NoteHelper.ReportResult(hold.judgeGrade, hold.isBreak, SimaiNoteType.Hold);
        hold.isEnd = true;
    }
}
