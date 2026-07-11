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
public unsafe struct TouchHoldUpdateJob : IJobParallelFor
{
    public NativeArray<TouchHoldData> touchHolds;

    [NativeDisableParallelForRestriction]
    public NativeArray<SimpleRenderData> simpleRender;

    [NativeDisableUnsafePtrRestriction]
    public int* SimpleWriteCountPtr;

    [NativeDisableParallelForRestriction]
    public NativeArray<MaskRenderData> maskRender;

    [NativeDisableUnsafePtrRestriction]
    public int* MaskWriteCountPtr;

    [NativeDisableUnsafePtrRestriction]
    public bool* SfxRequests;
    [NativeDisableUnsafePtrRestriction]
    public EffectData* JudgeEffectRequests;
    public NativeList<ReportResultEntry>.ParallelWriter ReportResults;

    [ReadOnly] public NativeArray<int> touchHoldGroupTotalCounts;
    [ReadOnly] public NativeArray<int> touchHoldGroupPressedCounts;
    [ReadOnly] public NativeArray<CoverResult> touchHoldGroupCoverResults;

    public void Execute(int index)
    {
        ref var th = ref touchHolds.ElementRef(index);
        TransformUpdate(ref th, index);
        AutoplayUpdate(ref th);
        CheckUpdate(ref th);
    }

    private void TransformUpdate(ref TouchHoldData th, int index)
    {
        if (th.isEnd) return;

        var sortTime = (uint)math.clamp(th.time * 100f, 0f, 0xFFFFF);

        var timing = th.usingSV
            ? TimeData.FakeNoteTime - TimeData.GetPositionAtTime(th.time)
            : TimeData.NoteTime - th.time;
        var lastFor = th.usingSV
            ? TimeData.GetPositionAtTime(th.time + th.LastFor) - TimeData.GetPositionAtTime(th.time)
            : th.LastFor;

        var wholeDuration = 3.209385682f * math.pow(th.speed, -0.9549621752f);
        var moveDuration = 0.8f * wholeDuration;
        var displayDuration = 0.2f * wholeDuration;

        var pow = -math.exp(8f * (timing * 0.43f / moveDuration) - 0.85f) + 0.42f;
        var fanDist = math.clamp(pow, 0f, 0.4f);

        if (-timing > wholeDuration)
        {
            return;
        }
        else if (-timing <= wholeDuration && -timing > moveDuration)
        {
            var fadeT = (-timing - moveDuration) / displayDuration;
            th.fanAlpha = math.saturate(1f - fadeT);
        }
        else if (-timing <= moveDuration)
        {
            th.fanAlpha = 1f;
        }

        if (timing >= 0)
        {
            th.maskProgress = math.clamp(timing / lastFor, 0f, 1f);
        }

        // ---- hold effect ----
        NoteHelper.SetHoldEffect(JudgeEffectRequests,
            (int)th.sensor + 8,
            th.judgeGrade,
            th.isHolding
        );
        NoteHelper.SetTouchHoldSound(SfxRequests, th.isHolding);

        // ---- hold on/off skin ----
        if (th.LastFor > 0.3f && // 忽略短hold
            timing >= 0.1f &&    // 忽略头6帧
            !th.isMine)          // 忽略mine
        {
            if (th.isHolding)
            {
                th.borderSprite = th._borderOnSpriteCache;
            }
            else
            {
                th.borderSprite = TOUCH_HOLD_BORDER_MISS;
            }
        }

        var centerPos = th.centerPos;
        var color = new float4(1, 1, 1, th.fanAlpha);

        var radius = 0.226f + fanDist;
        var c = math.SQRT2 / 2f;
        var fanPositions = stackalloc float2[4]
        {
            centerPos + new float2(radius * c, radius * c),
            centerPos + new float2(radius * c, -radius * c),
            centerPos + new float2(-radius * c, -radius * c),
            centerPos + new float2(-radius * c, radius * c),
        };

        for (int i = 0; i < 4; i++)
        {
            var tIdx = Interlocked.Increment(ref *SimpleWriteCountPtr) - 1;
            simpleRender[tIdx] = new SimpleRenderData
            {
                pos = fanPositions[i],
                angRad = math.radians(135f - 90f * i),
                scale = new float2(1, 1),
                spriteId = th.fanSprite + (uint)i,
                color = color,
                brightness = 1f,
                sort = (sortTime << 4) | 0x3,
            };
        }

        var ptIdx = Interlocked.Increment(ref *SimpleWriteCountPtr) - 1;
        simpleRender[ptIdx] = new SimpleRenderData
        {
            pos = centerPos,
            angRad = 0,
            scale = new float2(1, 1),
            spriteId = th.pointSprite,
            color = color,
            brightness = 1f,
            sort = (sortTime << 4) | 0x2,
        };

        var borderIdx = Interlocked.Increment(ref *MaskWriteCountPtr) - 1;
        maskRender[borderIdx] = new MaskRenderData
        {
            pos = centerPos,
            angRad = 0,
            scale = new float2(1, 1),
            spriteId = th.borderSprite,
            color = color,
            maskCutoff = th.maskProgress,
            sort = sortTime,
        };
    }

    private void AutoplayUpdate(ref TouchHoldData th)
    {
        if (th.isEnd) return;
        if (NoteHelper.AutoPlayMode is AutoPlayMode.Disable) return;

        var timing = TimeData.NoteTime - th.time;
        if (timing < -0.01f) return;

        switch (NoteHelper.AutoPlayMode)
        {
            case AutoPlayMode.Enable:
                if (!th.isHeadJudged)
                {
                    th.judgeGrade = th.isMine ? JudgeGrade.Miss : JudgeGrade.LateCritical;
                    th.isHeadJudged = true;
                    th.isHolding = true;
                    th.headDiff = 0;
                }
                if (th.isHeadJudged)
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
                if (!th.isHeadJudged)
                {
                    // TODO:这谁写的random？
                    var grade = (JudgeGrade)(new Random(114514).NextInt((int)JudgeGrade.FastGood, (int)JudgeGrade.Miss));
                    th.judgeGrade = th.isMine
                        ? (grade < JudgeGrade.FastPerfect3rd ? JudgeGrade.Miss : JudgeGrade.LateCritical)
                        : grade;
                    th.isHeadJudged = true;
                    th.isHolding = true;
                    th.headDiff = grade >= JudgeGrade.LateCritical ? 11.4514f : -11.4514f;
                }
                if (th.isHeadJudged)
                {
                    var remaining = math.max(th.LastFor - timing, 0);
                    if (remaining <= 0)
                    {
                        th.holdPercent = 1f;
                        EndNote(ref th);
                    }
                }
                break;
            case AutoPlayMode.DJAutoButton:
            case AutoPlayMode.DJAutoSensor:
                if (!th.isHeadJudged || math.max(th.LastFor - timing, 0) > 0)
                {
                    InputData.DJAutoAddGroupCoverage(touchHoldGroupCoverResults[th.coverageId]);
                }
                break;
        }
    }

    private void CheckUpdate(ref TouchHoldData th)
    {
        if (th.isEnd) return;
        if (!NoteHelper.IsSimulated) return;

        var noteTime = TimeData.NoteTime;
        var timing = noteTime - th.time;

        if (!th.isHeadJudged)
        {
            if (th.isMine && timing >= 0.016667f)
            {
                th.judgeGrade = JudgeGrade.LateCritical;
                th.isHeadJudged = true;
                return;
            }
            if (!th.isMine && timing > 0.316667f)
            {
                th.judgeGrade = JudgeGrade.Miss;
                th.isHeadJudged = true;
                th.headDiff = 0.316667f;
                return;
            }

            var _on = InputData.GetSensorState(th.sensor).Status;
            if (th.groupId != -1)
            {
                if (touchHoldGroupPressedCounts[th.groupId] * 2 > touchHoldGroupTotalCounts[th.groupId])
                {
                    _on = true;
                }
            }

            var clicked = _on && !th.SensorLastState;
            th.SensorLastState = _on;

            if (!clicked) return;
            var diffMSec = timing * 1000;
            if (diffMSec < -150) return;
            if (!InputData.CanJudgeSensor(th.sensor, th.sensorOrderIndex)) return;

            th.judgeGrade = diffMSec < 0 ? JudgeGrade.FastCritical
                : diffMSec <= 150 ? JudgeGrade.LateCritical
                : diffMSec <= 175 ? JudgeGrade.LatePerfect2nd
                : diffMSec <= 200 ? JudgeGrade.LatePerfect3rd
                : diffMSec <= 216.6667f ? JudgeGrade.LateGreat1st
                : diffMSec <= 233.3333f ? JudgeGrade.LateGreat2nd
                : diffMSec <= 250 ? JudgeGrade.LateGreat3rd
                : JudgeGrade.LateGood;
            th.isHeadJudged = true;
            th.headDiff = timing;
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

        if (!TimeData.IsStart) return;

        var on = InputData.GetSensorState(th.sensor).Status;
        if (th.groupId != -1)
        {
            if (touchHoldGroupPressedCounts[th.groupId] * 2 > touchHoldGroupTotalCounts[th.groupId])
            {
                on = true;
            }
        }

        th.isHolding = on;
        if (timing > 0.25f && remainingTime > 0.2f && !on)
            th.playerIdleTime += TimeData.deltaTime;
    }

    private void EndNote(ref TouchHoldData th)
    {
        NoteHelper.SetTouchHoldSound(SfxRequests, false);

        if (th.isBreak)
            NoteHelper.PlayTapSound(SfxRequests,
                th.judgeGrade,
                true,
                th.isEx,
                false,
                th.headDiff
            );
        else
            NoteHelper.PlayTouchSound(SfxRequests,
                th.judgeGrade,
                th.isMine,
                th.isHanabi
            );
        NoteHelper.PlayTouchEffect(JudgeEffectRequests,
            (int)th.sensor + 8,
            th.judgeGrade,
            th.isBreak,
            th.isHanabi
        );
        NoteHelper.ReportResult(ReportResults,
            th.judgeGrade,
            th.isBreak,
            SimaiNoteType.TouchHold
        );

        MajBurst.InputData.NextTouch(th.sensor);
        th.isEnd = true;
    }
}