#pragma warning disable CS8500
using MajSimai;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using static MajBurst;

[BurstCompile]
public unsafe struct TouchUpdateJob : IJobParallelFor
{
    public NativeArray<TouchData> touches;

    [NativeDisableParallelForRestriction]
    public NativeArray<SimpleRenderData> touchesRender;

    [NativeDisableUnsafePtrRestriction]
    public int* TouchesWriteCountPtr;

    [NativeDisableUnsafePtrRestriction]
    public bool* SfxRequests;
    [NativeDisableUnsafePtrRestriction]
    public EffectData* JudgeEffectRequests;
    public NativeList<ReportResultEntry>.ParallelWriter ReportResults;

    [ReadOnly] public NativeArray<int> touchGroupTotalCounts;
    [NativeDisableParallelForRestriction] public NativeArray<int> touchGroupJudgedCounts;
    [ReadOnly] public NativeArray<CoverResult> touchGroupCoverResults;

    public void Execute(int index)
    {
        ref var touch = ref touches.ElementRef(index);
        TransformUpdate(ref touch, index);
        AutoplayUpdate(ref touch);
        CheckUpdate(ref touch);
    }

    private void TransformUpdate(ref TouchData touch, int index)
    {
        if (touch.isEnd) return;

        var sortTime = (uint)math.clamp(touch.time * 100f, 0f, 0xFFFFF);

        var timing = touch.usingSV
            ? TimeData.FakeNoteTime - TimeData.GetPositionAtTime(touch.time)
            : TimeData.NoteTime - touch.time;
        var pow = -math.exp(8f * (timing * 0.43f / touch.moveDuration) - 0.85f) + 0.42f;
        var fanDist = math.clamp(pow, 0f, 0.4f);

        if (-timing > touch.wholeDuration)
        {
            return;
        }

        if (!touch.isAppeared)
        {
            touch.isAppeared = true;
            MajBurst.MultTouchHandler.RegisterActive(touch.sensor);
        }

        if (-timing < touch.wholeDuration && -timing >= touch.moveDuration)
        {
            touch.fanAlpha = 1 - math.saturate((-timing - touch.moveDuration) / touch.displayDuration);
            fanDist = 0.4f;
        }
        else if (-timing <= touch.moveDuration)
        {
            touch.fanAlpha = 1f;
        }

        var centerPos = touch.centerPos;

        var fanPositions = stackalloc float2[4]
        {
            centerPos + new float2(0.226f + fanDist, 0),
            centerPos + new float2(0, 0.226f + fanDist),
            centerPos + new float2(-(0.226f + fanDist), 0),
            centerPos + new float2(0, -(0.226f + fanDist)),
        };
        for (int i = 0; i < 4; i++)
        {
            var tIdx = Interlocked.Increment(ref *TouchesWriteCountPtr) - 1;
            touchesRender[tIdx] = new SimpleRenderData
            {
                pos = fanPositions[i],
                angRad = math.radians(90f * (i + 1)),
                scale = new float2(1, 1),
                spriteId = touch.fanSprite,
                color = new float4(1, 1, 1, touch.fanAlpha),
                brightness = 1f,
                sort = (sortTime << 4) | 0x3,
            };
        }

        var ptIdx = Interlocked.Increment(ref *TouchesWriteCountPtr) - 1;
        touchesRender[ptIdx] = new SimpleRenderData
        {
            pos = centerPos,
            angRad = 0,
            scale = new float2(1, 1),
            spriteId = touch.pointSprite,
            color = new float4(1, 1, 1, touch.fanAlpha),
            brightness = 1f,
            sort = (sortTime << 4) | 0x2,
        };

        if (timing > -0.02f)
        {
            var justIdx = Interlocked.Increment(ref *TouchesWriteCountPtr) - 1;
            touchesRender[justIdx] = new SimpleRenderData
            {
                pos = centerPos,
                angRad = 0,
                scale = new float2(1, 1),
                spriteId = touch.justSprite,
                color = new float4(1),
                brightness = 1f,
                sort = (sortTime << 4) | 0x1,
            };
        }

        if (-timing < touch.wholeDuration &&
            MajBurst.MultTouchHandler.CanShowBorder(touch.sensor, out var isThree, out var sprite))
        {
            if (!isThree)
            {
                var borderIdx = Interlocked.Increment(ref *TouchesWriteCountPtr) - 1;
                touchesRender[borderIdx] = new SimpleRenderData
                {
                    pos = centerPos,
                    angRad = 0,
                    scale = new float2(1, 1),
                    spriteId = (uint)sprite,
                    color = new float4(1, 1, 1, touch.fanAlpha),
                    brightness = 1f,
                    sort = sortTime << 4,
                };
            }
            else
            {
                var borderIdx = Interlocked.Increment(ref *TouchesWriteCountPtr) - 1;
                touchesRender[borderIdx] = new SimpleRenderData
                {
                    pos = centerPos,
                    angRad = 0,
                    scale = new float2(1, 1),
                    spriteId = (uint)sprite,
                    color = new float4(1, 1, 1, touch.fanAlpha),
                    brightness = 1f,
                    sort = sortTime << 4,
                };
            }
        }
    }

    private void AutoplayUpdate(ref TouchData touch)
    {
        if (touch.isEnd) return;
        if (NoteHelper.AutoPlayMode is AutoPlayMode.Disable) return;

        var timing = TimeData.NoteTime - touch.time;
        if (timing < -0.01f) return;

        switch (NoteHelper.AutoPlayMode)
        {
            case AutoPlayMode.Enable:
                touch.judgeGrade = touch.isMine ? JudgeGrade.Miss : JudgeGrade.LateCritical;
                touch.isJudged = true;
                touch.diff = 0;
                EndNote(ref touch);
                break;
            case AutoPlayMode.Random:
                var grade = (JudgeGrade)GlobalRandom.NextInt((int)JudgeGrade.FastGood, (int)JudgeGrade.Miss); ;
                touch.judgeGrade = touch.isMine
                    ? (grade < JudgeGrade.FastPerfect3rd ? JudgeGrade.Miss : JudgeGrade.LateCritical)
                    : grade;
                touch.isJudged = true;
                touch.diff = grade >= JudgeGrade.LateCritical ? 11.4514f : -11.4514f;
                EndNote(ref touch);
                break;
            case AutoPlayMode.DJAutoButton:
            case AutoPlayMode.DJAutoSensor:
                if (!touch.isJudged)
                {
                    InputData.DJAutoAddGroupCoverage(touchGroupCoverResults[touch.coverageId]);
                }
                break;
        }
    }

    private void CheckUpdate(ref TouchData touch)
    {
        if (touch.isJudged || touch.isEnd) return;
        if (!NoteHelper.IsSimulated) return;

        var noteTime = TimeData.NoteTime;
        var diffSec = noteTime - touch.time;

        if (touch.isMine)
        {
            var mineOn = MajBurst.InputData.GetSensorState(touch.sensor).Status;
            if (mineOn && diffSec >= -NoteHelper.TAP_JUDGE_GOOD_AREA_MSEC / 1000f)
            {
                touch.judgeGrade = JudgeGrade.Miss;
                touch.isJudged = true;
                touch.diff = diffSec;
                EndNote(ref touch);
                return;
            }
            if (diffSec >= NoteHelper.MINE_END_SEC)
            {
                touch.judgeGrade = JudgeGrade.LateCritical;
                touch.isJudged = true;
                EndNote(ref touch);
            }
            return;
        }

        if (diffSec > NoteHelper.TOUCH_JUDGE_GOOD_AREA_MSEC / 1000f)
        {
            touch.judgeGrade = JudgeGrade.Miss;
            touch.isJudged = true;
            EndNote(ref touch);
            return;
        }

        var stateOn = MajBurst.InputData.GetSensorState(touch.sensor).Status;

        if (touch.groupId != -1)
        {
            if (touchGroupJudgedCounts[touch.groupId] * 2 > touchGroupTotalCounts[touch.groupId])
            {
                stateOn = true;
            }
        }

        if (!stateOn) return;

        var diffMSec = diffSec * 1000;
        if (diffMSec < -NoteHelper.TOUCH_JUDGE_SEG_1ST_PERFECT_MSEC) return;
        if (!MajBurst.InputData.CanJudgeSensor(touch.sensor, touch.sensorOrderIndex)) return;

        touch.judgeGrade = NoteHelper.GetTouchJudge(diffSec);
        touch.isJudged = true;
        touch.diff = diffSec;
        EndNote(ref touch);
    }

    private void EndNote(ref TouchData touch)
    {
        if (touch.isBreak)
            NoteHelper.PlayTapSound(SfxRequests,
                touch.judgeGrade,
                true,
                touch.isEx,
                false,
                touch.diff
            );
        else
            NoteHelper.PlayTouchSound(SfxRequests,
                touch.judgeGrade,
                touch.isMine,
                touch.isHanabi
            );
        NoteHelper.PlayTouchEffect(JudgeEffectRequests,
            (int)touch.sensor + 8,
            touch.judgeGrade,
            touch.isBreak,
            touch.isHanabi
        );
        NoteHelper.ReportResult(ReportResults,
            touch.judgeGrade,
            touch.isBreak,
            SimaiNoteType.Touch
        );

        MajBurst.InputData.NextTouch(touch.sensor);
        MajBurst.MultTouchHandler.Unregister(touch.sensor);
        if (touch.isAppeared)
        {
            touch.isAppeared = false;
            MajBurst.MultTouchHandler.UnregisterActive(touch.sensor);
        }

        if (touch.groupId != -1 && touch.judgeGrade != JudgeGrade.Miss)
        {
            unsafe
            {
                var ptr = (int*)touchGroupJudgedCounts.GetUnsafePtr();
                Interlocked.Increment(ref ptr[touch.groupId]);
            }
        }

        touch.isEnd = true;
    }
}
