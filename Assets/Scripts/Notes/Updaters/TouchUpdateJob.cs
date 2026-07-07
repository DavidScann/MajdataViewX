#pragma warning disable CS8500
using MajSimai;
using System.ComponentModel;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using static MajBurst;
using static NoteSkinManager;

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

    public void Execute(int index)
    {
        var touch = touches[index];
        TransformUpdate(ref touch, index);
        AutoplayUpdate(ref touch);
        CheckUpdate(ref touch);
        touches[index] = touch;
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
        else if (-timing < touch.wholeDuration && -timing >= touch.moveDuration)
        {
            var fadeT = (timing - -touch.moveDuration) / touch.displayDuration;
            touch.fanAlpha = math.saturate(fadeT);
            pow = -math.exp(-0.85f) + 0.42f;
            fanDist = math.clamp(pow, 0f, 0.4f);
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
                    sort = sortTime << 4,
                };
            }
        }
    }

    private void AutoplayUpdate(ref TouchData touch)
    {
        if (touch.isEnd) return;

        var timing = TimeData.NoteTime - touch.time;
        if (timing < -0.01f) return;

        switch (NoteHelper.AutoPlayMode)
        {
            case AutoPlayMode.Enable:
                touch.judgeGrade = touch.isMine ? JudgeGrade.Miss : JudgeGrade.Perfect;
                touch.isJudged = true;
                touch.diff = 0;
                EndNote(ref touch);
                break;
            case AutoPlayMode.Random:
                var gradeIndex = new Random(114514).NextInt(1, 14);
                touch.judgeGrade = touch.isMine
                    ? (gradeIndex > 4 ? JudgeGrade.Miss : JudgeGrade.Perfect)
                    : (JudgeGrade)gradeIndex;
                touch.isJudged = true;
                touch.diff = gradeIndex > 7 ? 11.4514f : -11.4514f;
                EndNote(ref touch);
                break;
        }
    }

    private void CheckUpdate(ref TouchData touch)
    {
        if (NoteHelper.AutoPlayMode is AutoPlayMode.Enable or AutoPlayMode.Random) return;
        if (touch.isJudged || touch.isEnd) return;

        var noteTime = TimeData.NoteTime;
        var diffSec = noteTime - touch.time;

        if (touch.isMine)
        {
            var mineOn = MajBurst.InputData.GetSensorState(touch.sensor).Status;
            if (mineOn && diffSec >= -0.15f)
            {
                touch.judgeGrade = JudgeGrade.Miss;
                touch.isJudged = true;
                touch.diff = diffSec;
                EndNote(ref touch);
                return;
            }
            if (diffSec >= 0.016667f)
            {
                touch.judgeGrade = JudgeGrade.Perfect;
                touch.isJudged = true;
                EndNote(ref touch);
            }
            return;
        }

        if (diffSec > 0.316667f)
        {
            touch.judgeGrade = JudgeGrade.Miss;
            touch.isJudged = true;
            EndNote(ref touch);
            return;
        }

        var stateOn = MajBurst.InputData.GetSensorState(touch.sensor).Status;
        if (!stateOn) return;

        var diffMSec = math.abs(diffSec * 1000);
        if (diffMSec > 150f && diffSec < 0) return;
        if (!MajBurst.InputData.CanJudgeSensor(touch.sensor, touch.sensorOrderIndex)) return;

        touch.judgeGrade = diffMSec <= 150 ? JudgeGrade.Perfect
            : diffMSec <= 200 ? JudgeGrade.LatePerfect2nd
            : diffMSec <= 250 ? JudgeGrade.LateGreat
            : JudgeGrade.LateGood;
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
        else if (touch.isHanabi)
            NoteHelper.PlayHanabiSound(SfxRequests);
        else
            NoteHelper.PlayTouchSound(SfxRequests);

        NoteHelper.PlayTouchEffect(JudgeEffectRequests, (int)touch.sensor + 8, touch.judgeGrade, touch.isBreak);
        NoteHelper.ReportResult(ReportResults, touch.judgeGrade, touch.isBreak, SimaiNoteType.Touch);

        MajBurst.InputData.NextTouch(touch.sensor);
        MajBurst.MultTouchHandler.Unregister(touch.sensor);
        touch.isEnd = true;
    }
}
