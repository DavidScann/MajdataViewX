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
public struct TouchData
{
    public float time;
    public SensorType sensor;
    public float speed;
    public int sensorOrderIndex;

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

    public uint fanSprite;
    public uint pointSprite;
    public uint borderSprite0;
    public uint borderSprite1;
    public uint justSprite;

    public bool isJudged;
    public JudgeGrade judgeGrade;
    public float diff;

    public void Init()
    {
        show = false;
        fanProgress = 0;
        fanAlpha = 0;

        centerPos = GetAreaPos(sensor);

        fanSprite = TOUCH;
        pointSprite = TOUCH_POINT;
        borderSprite0 = TOUCH_BORDER_0;
        borderSprite1 = TOUCH_BORDER_1;
        justSprite = TOUCH_JUST;

        if (isEach)
        {
            fanSprite = TOUCH_EACH;
            pointSprite = TOUCH_POINT_EACH;
            borderSprite0 = TOUCH_BORDER_EACH_0;
            borderSprite1 = TOUCH_BORDER_EACH_1;
        }
        if (isBreak)
        {
            fanSprite = TOUCH_BREAK;
            pointSprite = TOUCH_POINT_BREAK;
            borderSprite0 = TOUCH_BORDER_BREAK_0;
            borderSprite1 = TOUCH_BORDER_BREAK_1;
        }
        if (isMine)
        {
            if (isBreak)
            {
                fanSprite = TOUCH_BREAK_MINE;
                pointSprite = TOUCH_POINT_BREAK_MINE;
                borderSprite0 = TOUCH_BORDER_BREAK_MINE_0;
                borderSprite1 = TOUCH_BORDER_BREAK_MINE_1;
            }
            else
            {
                fanSprite = TOUCH_MINE;
                pointSprite = TOUCH_POINT_MINE;
                borderSprite0 = TOUCH_BORDER_MINE_0;
                borderSprite1 = TOUCH_BORDER_MINE_1;
            }
        }
    }

    public static float2 GetAreaPos(SensorType sensor)
    {
        int i = (int)sensor;
        if (i >= 0 && i <= 7)
            return RingPos(4.1f, i + 1, false);
        if (i >= 8 && i <= 15)
            return RingPos(2.3f, i - 7, false);
        if (i == 16)
            return float2.zero;
        if (i >= 17 && i <= 24)
            return RingPos(4.1f, i - 16, true);
        if (i >= 25 && i <= 32)
            return RingPos(3.0f, i - 24, true);
        return float2.zero;
    }

    private static float2 RingPos(float radius, int index1, bool altAngle)
    {
        var a = altAngle
            ? (index1 * -2f + 6.4f) * 0.125f * math.PI
            : (index1 * -2f + 5f) * 0.125f * math.PI;
        return new float2(radius * math.cos(a), radius * math.sin(a));
    }
}

[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
public unsafe struct TouchUpdateJob : IJobParallelFor
{
    [NativeDisableUnsafePtrRestriction]
    public BurstTimeData* TimeDataPtr;
    public NativeArray<TouchData> touches;

    [NativeDisableParallelForRestriction]
    public NativeArray<SimpleRenderData> touchesRender;

    [NativeDisableUnsafePtrRestriction]
    public int* TouchesWriteCountPtr;

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

        var timing = TimeDataPtr->NoteTime - touch.time;
        var fakeTiming = TimeDataPtr->FakeNoteTime - TimeDataPtr->GetPositionAtTime(touch.time);

        var wholeDuration = 3.209385682f * math.pow(touch.speed, -0.9549621752f);
        var moveDuration = 0.8f * wholeDuration;
        var displayDuration = 0.2f * wholeDuration;

        var pow = -math.exp(8f * (fakeTiming * 0.43f / moveDuration) - 0.85f) + 0.42f;
        var fanDist = math.clamp(pow, 0f, 0.4f);
        touch.fanProgress = fanDist;

        if (-fakeTiming <= wholeDuration && -fakeTiming > moveDuration)
        {
            var fadeT = (-fakeTiming - moveDuration) / displayDuration;
            touch.fanAlpha = math.saturate(fadeT);
            touch.show = true;
        }
        else if (-fakeTiming < moveDuration)
        {
            touch.fanAlpha = 1f;
            touch.show = true;
        }

        if (!touch.show) return;

        var centerPos = touch.centerPos;
        var ptIdx = Interlocked.Increment(ref *TouchesWriteCountPtr) - 1;
        touchesRender[ptIdx] = new SimpleRenderData
        {
            pos = centerPos,
            angRad = 0,
            scale = new float2(1, 1),
            spriteId = touch.pointSprite,
            color = new float4(touch.fanAlpha, touch.fanAlpha, touch.fanAlpha, 1),
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
            var tIdx = Interlocked.Increment(ref *TouchesWriteCountPtr) - 1;
            touchesRender[tIdx] = new SimpleRenderData
            {
                pos = fanPositions[i],
                angRad = 0,
                scale = new float2(1, 1),
                spriteId = touch.fanSprite,
                color = new float4(touch.fanAlpha, touch.fanAlpha, touch.fanAlpha, 1),
                sort = (uint)index + (uint)i * 0x10000u,
            };
        }

        var borderIdx = Interlocked.Increment(ref *TouchesWriteCountPtr) - 1;
        touchesRender[borderIdx] = new SimpleRenderData
        {
            pos = centerPos,
            angRad = 0,
            scale = new float2(1, 1),
            spriteId = touch.borderSprite0,
            color = new float4(touch.fanAlpha, touch.fanAlpha, touch.fanAlpha, 1),
            sort = (uint)index + 8u * 0x10000u,
        };
        var borderIdx2 = Interlocked.Increment(ref *TouchesWriteCountPtr) - 1;
        touchesRender[borderIdx2] = new SimpleRenderData
        {
            pos = centerPos,
            angRad = 0,
            scale = new float2(1, 1),
            spriteId = touch.borderSprite1,
            color = new float4(touch.fanAlpha, touch.fanAlpha, touch.fanAlpha, 1),
            sort = (uint)index + 9u * 0x10000u,
        };
    }

    private void AutoplayUpdate(ref TouchData touch)
    {
        if (touch.isEnd) return;

        var timing = TimeDataPtr->NoteTime - touch.time;
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
            case AutoPlayMode.DJAuto:
                if (!touch.isJudged && !touch.isMine)
                {
                    NoteHelper.SensorStates[(int)touch.sensor].Status = SensorStatus.On;
                }
                break;
        }
    }

    private void CheckUpdate(ref TouchData touch)
    {
        if (NoteHelper.AutoPlayMode is AutoPlayMode.Enable or AutoPlayMode.Random) return;
        if (touch.isJudged || touch.isEnd) return;

        var noteTime = TimeDataPtr->NoteTime;
        var diffSec = noteTime - touch.time;
        var key = (int)touch.sensor;

        if (touch.isMine)
        {
            var mineOn = NoteHelper.SensorStates[key].Status == SensorStatus.On;
            var mineBusy = NoteHelper.SensorStates[key].IsJudging;
            if (mineOn && !mineBusy && diffSec >= -0.15f)
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

        var stateOn = NoteHelper.SensorStates[key].Status == SensorStatus.On;
        var stateBusy = NoteHelper.SensorStates[key].IsJudging;
        if (!stateOn || stateBusy) return;

        var diffMSec = math.abs(diffSec * 1000);
        if (diffMSec > 150f && diffSec < 0) return;

        var orderIdx = NoteHelper.NextSensorIndex[key];
        if (orderIdx == touch.sensorOrderIndex)
        {
            NoteHelper.SensorStates[key].IsJudging = true;
            NoteHelper.NextSensorIndex[key] = orderIdx + 1;

            touch.judgeGrade = diffMSec <= 150 ? JudgeGrade.Perfect
                : diffMSec <= 200 ? JudgeGrade.LatePerfect2nd
                : diffMSec <= 250 ? JudgeGrade.LateGreat
                : JudgeGrade.LateGood;
            touch.isJudged = true;
            touch.diff = diffSec;
            EndNote(ref touch);
        }
    }

    private void EndNote(ref TouchData touch)
    {
        if (touch.isBreak)
            NoteHelper.PlayTapSound(new JudgeResult
            {
                Grade = touch.judgeGrade,
                IsBreak = true,
                IsEX = touch.isEx,
                IsMine = false,
                Diff = touch.diff
            });
        else if (touch.isHanabi)
            NoteHelper.PlayHanabiSound();
        else
            NoteHelper.PlayTouchSound();

        NoteHelper.PlayJudgeEffect((int)touch.sensor, touch.judgeGrade, touch.isBreak);
        NoteHelper.PlayFastLateEffect((int)touch.sensor, touch.judgeGrade);
        NoteHelper.ReportResult(touch.judgeGrade, touch.isBreak, SimaiNoteType.Touch);
        touch.show = false;
        touch.isEnd = true;
    }
}
