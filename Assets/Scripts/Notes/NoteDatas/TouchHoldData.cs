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

    public bool show;
    public bool isEnd;
    public float2 centerPos;

    public float fanProgress;
    public float fanAlpha;
    public float maskProgress;

    public uint fanSprite;
    public uint pointSprite;
    public uint borderSprite;

    public bool isJudged;
    public JudgeGrade judgeGrade;
    public float headDiff;
    public float playerIdleTime;
    public float holdPercent;
    public bool isHolding;

    public void Init()
    {
        show = false;
        fanProgress = 0;
        fanAlpha = 0;
        maskProgress = 0;

        centerPos = TouchData.GetAreaPos(sensor);

        fanSprite = TOUCH_HOLD_0;
        pointSprite = TOUCH_POINT;
        borderSprite = TOUCH_HOLD_BORDER;

        if (isBreak)
        {
            fanSprite = TOUCH_HOLD_BREAK_0;
            pointSprite = TOUCH_POINT_BREAK;
            borderSprite = TOUCH_HOLD_BORDER_BREAK;
        }
        if (isMine)
        {
            fanSprite = TOUCH_HOLD_MINE_0;
            pointSprite = TOUCH_POINT_MINE;
            if (isBreak)
                borderSprite = TOUCH_HOLD_BORDER_BREAK_MINE;
            else
                borderSprite = TOUCH_HOLD_BORDER_MINE;
        }
    }
}

[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
public unsafe struct TouchHoldUpdateJob : IJobParallelFor
{
    [NativeDisableUnsafePtrRestriction]
    public BurstTimeData* TimeDataPtr;
    public NativeArray<TouchHoldData> touchHolds;

    [NativeDisableParallelForRestriction]
    public NativeArray<SimpleRenderData> simpleRender;

    [NativeDisableUnsafePtrRestriction]
    public int* SimpleWriteCountPtr;

    [NativeDisableParallelForRestriction]
    public NativeArray<MaskRenderData> maskRender;

    [NativeDisableUnsafePtrRestriction]
    public int* MaskWriteCountPtr;

    public void Execute(int index)
    {
        var th = touchHolds[index];
        TransformUpdate(ref th, index);
        AutoplayUpdate(ref th);
        CheckUpdate(ref th);
        touchHolds[index] = th;
    }

    private void TransformUpdate(ref TouchHoldData th, int index)
    {
        if (th.isEnd) return;

        var timing = TimeDataPtr->NoteTime - th.time;
        var fakeTiming = TimeDataPtr->FakeNoteTime - TimeDataPtr->GetPositionAtTime(th.time);

        var wholeDuration = 3.209385682f * math.pow(th.speed, -0.9549621752f);
        var moveDuration = 0.8f * wholeDuration;
        var displayDuration = 0.2f * wholeDuration;

        var pow = -math.exp(8f * (fakeTiming * 0.43f / moveDuration) - 0.85f) + 0.42f;
        var fanDist = math.clamp(pow, 0f, 0.4f);
        th.fanProgress = fanDist;

        if (-fakeTiming <= wholeDuration && -fakeTiming > moveDuration)
        {
            var fadeT = (-fakeTiming - moveDuration) / displayDuration;
            th.fanAlpha = math.saturate(fadeT);
            th.show = true;
        }
        else if (-fakeTiming < moveDuration)
        {
            th.fanAlpha = 1f;
            th.show = true;
        }

        var fakeLastFor = TimeDataPtr->FakeNoteTime - TimeDataPtr->GetPositionAtTime(th.time + th.LastFor);
        if (th.isJudged && fakeTiming >= 0)
        {
            th.maskProgress = math.clamp((fakeLastFor - fakeTiming) / math.max(fakeLastFor, 0.001f), 0f, 1f);
        }

        if (!th.show) return;

        var centerPos = th.centerPos;
        var color = new float4(th.fanAlpha, th.fanAlpha, th.fanAlpha, 1);

        var ptIdx = Interlocked.Increment(ref *SimpleWriteCountPtr) - 1;
        simpleRender[ptIdx] = new SimpleRenderData
        {
            pos = centerPos,
            angRad = 0,
            scale = new float2(1, 1),
            spriteId = th.pointSprite,
            color = color,
            sort = (uint)index,
        };

        var fanPositions = stackalloc float2[4]
        {
            centerPos + new float2(0.226f + fanDist, 0),
            centerPos + new float2(0, 0.226f + fanDist),
            centerPos + new float2(-(0.226f + fanDist), 0),
            centerPos + new float2(0, -(0.226f + fanDist)),
        };

        for (int i = 0; i < 4; i++)
        {
            var tIdx = Interlocked.Increment(ref *SimpleWriteCountPtr) - 1;
            simpleRender[tIdx] = new SimpleRenderData
            {
                pos = fanPositions[i],
                angRad = 0,
                scale = new float2(1, 1),
                spriteId = th.fanSprite + (uint)math.min(i, 3),
                color = color,
                sort = (uint)index + (uint)i * 0x10000u,
            };
        }

        float maskCutoff = 0;
        if (th.isJudged && fakeTiming >= 0)
        {
            var progress = (fakeLastFor - fakeTiming) / math.max(fakeLastFor, 0.001f);
            maskCutoff = math.clamp(0.91f * (1f - progress), 0f, 1f);
        }

        var borderIdx = Interlocked.Increment(ref *MaskWriteCountPtr) - 1;
        maskRender[borderIdx] = new MaskRenderData
        {
            pos = centerPos,
            angRad = 0,
            scale = new float2(1, 1),
            spriteId = th.borderSprite,
            color = color,
            maskCutoff = maskCutoff,
            sort = (uint)index + 8u * 0x10000u,
        };
    }

    private void AutoplayUpdate(ref TouchHoldData th)
    {
        if (th.isEnd) return;

        var timing = TimeDataPtr->NoteTime - th.time;
        if (timing < -0.01f) return;

        switch (NoteHelper.AutoPlayMode)
        {
            case AutoPlayMode.Enable:
                if (!th.isJudged)
                {
                    th.judgeGrade = th.isMine ? JudgeGrade.Miss : JudgeGrade.Perfect;
                    th.isJudged = true;
                    th.isHolding = true;
                    th.headDiff = 0;
                }
                if (th.isJudged)
                {
                    var remaining = math.max(th.LastFor - timing, 0);
                    if (remaining <= 0)
                    {
                        th.holdPercent = 1f;
                        EndNote(ref th);
                    }
                }
                break;
            case AutoPlayMode.Random:
                if (!th.isJudged)
                {
                    var gradeIndex = new Random(114514).NextInt(1, 14);
                    th.judgeGrade = th.isMine
                        ? (gradeIndex > 4 ? JudgeGrade.Miss : JudgeGrade.Perfect)
                        : (JudgeGrade)gradeIndex;
                    th.isJudged = true;
                    th.isHolding = true;
                    th.headDiff = gradeIndex > 7 ? 11.4514f : -11.4514f;
                }
                if (th.isJudged)
                {
                    var remaining = math.max(th.LastFor - timing, 0);
                    if (remaining <= 0)
                    {
                        th.holdPercent = 1f;
                        EndNote(ref th);
                    }
                }
                break;
            case AutoPlayMode.DJAuto:
                if (th.isMine) break;
                NoteHelper.SensorStates[(int)th.sensor].Status = SensorStatus.On;
                break;
        }
    }

    private void CheckUpdate(ref TouchHoldData th)
    {
        if (NoteHelper.AutoPlayMode is AutoPlayMode.Enable or AutoPlayMode.Random) return;
        if (th.isEnd) return;

        var noteTime = TimeDataPtr->NoteTime;
        var timing = noteTime - th.time;
        var key = (int)th.sensor;

        if (!th.isJudged)
        {
            if (th.isMine && timing >= 0.016667f)
            {
                th.judgeGrade = JudgeGrade.Perfect;
                th.isJudged = true;
                th.isHolding = true;
                return;
            }
            if (!th.isMine && timing > 0.316667f)
            {
                th.judgeGrade = JudgeGrade.Miss;
                th.isJudged = true;
                th.headDiff = 0.316667f;
                // NOTE: do NOT EndNote here. A missed head keeps tracking the body and
                // can still be recovered to LateGood by the release-percent mapping below.
                return;
            }

            var stateOn = NoteHelper.SensorStates[key].Status == SensorStatus.On;
            var stateBusy = NoteHelper.SensorStates[key].IsJudging;
            if (!stateOn || stateBusy) return;

            var diffMSec = math.abs(timing * 1000);
            if (diffMSec > 150f && timing < 0) return;

            var orderIdx = NoteHelper.NextSensorIndex[key];
            if (orderIdx == th.sensorOrderIndex)
            {
                NoteHelper.SensorStates[key].IsJudging = true;
                NoteHelper.NextSensorIndex[key] = orderIdx + 1;

                th.judgeGrade = diffMSec <= 150 ? JudgeGrade.Perfect
                    : diffMSec <= 200 ? JudgeGrade.LatePerfect2nd
                    : diffMSec <= 250 ? JudgeGrade.LateGreat
                    : JudgeGrade.LateGood;
                th.isJudged = true;
                th.isHolding = true;
                th.headDiff = timing;
            }
            return;
        }

        var remainingTime = math.max(th.LastFor - timing, 0);
        if (remainingTime <= 0)
        {
            var realityHT = th.LastFor - 0.45f - math.max(th.headDiff, 0f);
            var pct = math.clamp((realityHT - th.playerIdleTime) / math.max(realityHT, 0.001f), 0f, 1f);
            th.holdPercent = pct;
            if (!th.isMine)
                th.judgeGrade = NoteHelper.GetHoldFinalGrade(th.judgeGrade, pct, realityHT);
            EndNote(ref th);
            return;
        }

        if (!TimeDataPtr->IsStart) return;

        var on = NoteHelper.SensorStates[key].Status == SensorStatus.On;

        if (timing > 0.25f && remainingTime > 0.2f && !on)
            th.playerIdleTime += TimeDataPtr->deltaTime;
    }

    private void EndNote(ref TouchHoldData th)
    {
        NoteHelper.SetTouchHoldSound(false);

        if (th.isBreak)
            NoteHelper.PlayTapSound(new JudgeResult
            {
                Grade = th.judgeGrade,
                IsBreak = true,
                IsEX = th.isEx,
                IsMine = false,
                Diff = th.headDiff
            });
        else if (th.isHanabi)
            NoteHelper.PlayHanabiSound();
        else
            NoteHelper.PlayTouchSound();

        NoteHelper.PlayJudgeEffect((int)th.sensor, th.judgeGrade, th.isBreak);
        NoteHelper.PlayFastLateEffect((int)th.sensor, th.judgeGrade);
        NoteHelper.ReportResult(th.judgeGrade, th.isBreak, SimaiNoteType.TouchHold);
        th.show = false;
        th.isEnd = true;
    }
}
