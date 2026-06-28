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
public struct TapData
{
    // args
    public float time;
    public SensorType key;
    public float speed;
    public int sensorOrderIndex;

    // attrs
    public bool isStar;
    public bool isDouble;
    public float rotateSpeed;

    public bool isEach;
    public bool isEx;
    public bool isBreak;
    public bool isMine;
    public bool usingSV;

    // outs
    public float2 pos;
    public float scale;
    public float ang;
    public float brightness;

    //sprite
    public uint tapSprite;
    public uint lineSprite;
    public uint exSprite;
    public float4 exColor;

    // state
    public bool isJudged;
    public float diff;
    public JudgeGrade judgeGrade;

    public bool isEnd;

    public void Init()
    {
        pos = float2.zero;
        scale = 1f;
        ang = -22.5f + -45f * (int)key;
        brightness = 1f;

        // Load Skin
        if (isStar)
        {
            if (isDouble)
            {
                tapSprite = STAR_DOUBLE;
                lineSprite = LINE_STAR;
                exSprite = STAR_EX_DOUBLE;
                exColor = Ex;
                if (isEach)
                {
                    tapSprite = STAR_EACH_DOUBLE;
                    lineSprite = LINE_EACH;
                    exColor = Ex_Each;
                }
                if (isBreak)
                {
                    tapSprite = STAR_BREAK_DOUBLE;
                    lineSprite = LINE_BREAK;
                    exColor = Ex_Break;
                }
                if (isMine)
                {
                    if (isBreak)
                        tapSprite = STAR_BREAK_DOUBLE_MINE;
                    else
                        tapSprite = STAR_MINE_DOUBLE;
                    lineSprite = LINE_MINE;
                }
            }
            else
            {
                tapSprite = STAR;
                lineSprite = LINE_STAR;
                exSprite = STAR_EX;
                exColor = Ex;
                if (isEach)
                {
                    tapSprite = STAR_EACH;
                    lineSprite = LINE_EACH;
                    exColor = Ex_Each;
                }
                if (isBreak)
                {
                    tapSprite = STAR_BREAK;
                    lineSprite = LINE_BREAK;
                    exColor = Ex_Break;
                }
                if (isMine)
                {
                    if (isBreak)
                        tapSprite = STAR_BREAK_MINE;
                    else
                        tapSprite = STAR_MINE;
                    lineSprite = LINE_MINE;
                }
            }
        }
        else
        {
            tapSprite = TAP;
            lineSprite = LINE;
            exSprite = TAP_EX;
            exColor = Ex;
            if (isEach)
            {
                tapSprite = TAP_EACH;
                lineSprite = LINE_EACH;
                exColor = Ex_Each;
            }
            if (isBreak)
            {
                tapSprite = TAP_BREAK;
                lineSprite = LINE_BREAK;
                exColor = Ex_Break;
            }
            if (isMine)
            {
                if (isBreak)
                    tapSprite = TAP_BREAK_MINE;
                else
                    tapSprite = TAP_MINE;
                lineSprite = LINE_MINE;
            }
        }
    }
}


[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
public unsafe struct TapUpdateJob : IJobParallelFor
{
    [NativeDisableUnsafePtrRestriction]
    public BurstTimeData* TimeDataPtr;
    public NativeArray<TapData> taps;

    [NativeDisableParallelForRestriction]
    public NativeArray<LineRenderData> tapLinesRender;
    [NativeDisableParallelForRestriction]
    public NativeArray<NotesRenderData> notesRender;

    [NativeDisableUnsafePtrRestriction]
    public int* TapLinesWriteCountPtr;
    [NativeDisableUnsafePtrRestriction]
    public int* NotesWriteCountPtr;

    public void Execute(int index)
    {
        var tap = taps[index];
        TransformUpdate(ref tap, index);
        AutoplayUpdate(ref tap);
        CheckUpdate(ref tap);
        taps[index] = tap;
    }

    private void TransformUpdate(ref TapData tap, int index)
    {
        if (tap.isEnd) return;

        var timing = tap.usingSV
            ? TimeDataPtr->FakeNoteTime - TimeDataPtr->GetPositionAtTime(tap.time)
            : TimeDataPtr->NoteTime - tap.time;

        var rawDistance = timing * tap.speed + 4.8f;
        var clampedDistance = math.max(rawDistance, 1.225f);

        var destScale = math.min(rawDistance * 0.4f + 0.51f, 1f);
        var lineScale = clampedDistance / 4.8f;

        if (destScale < 0f) return;

        // show line
        if (destScale > 0.3f)
        {
            var lineIdx = Interlocked.Increment(ref *TapLinesWriteCountPtr) - 1;
            tapLinesRender[lineIdx] = new LineRenderData()
            {
                angRad = math.radians(tap.ang),
                scale = lineScale,
                spriteId = tap.lineSprite,
                sort = (uint)index,
            };
        }

        // show tap
        NoteHelper.GetPosFromDistance(clampedDistance, tap.key, out var pos);
        tap.pos = pos;
        tap.scale = destScale;

        if (tap.isBreak)
        {
            var extra = math.max(math.sin(TimeDataPtr->GetFrame() * 0.17f) * 0.5f, 0f);
            tap.brightness = 0.95f + extra;
        }
        if (tap.isStar && tap.rotateSpeed != 0)
        {
            var deltaRot = -180f * tap.rotateSpeed * TimeDataPtr->deltaTime;
            tap.ang += deltaRot;
        }

        var tapIdx = Interlocked.Increment(ref *NotesWriteCountPtr) - 1;
        notesRender[tapIdx] = new NotesRenderData()
        {
            pos = tap.pos,
            angRad = math.radians(tap.ang),
            scale = new float2(tap.scale, tap.scale),
            spriteId = tap.tapSprite,
            color = new float4(1, 1, 1, 1),
            brightness = tap.brightness,

            exSprite = tap.isEx ? tap.exSprite : 0,
            exColor = tap.exColor,
            sliceBorder = float2.zero,

            sort = (uint)index,
        };
    }

    private void AutoplayUpdate(ref TapData tap)
    {
        if (tap.isEnd) return;

        var timing = TimeDataPtr->NoteTime - tap.time;
        if (timing < -0.01f) return;

        switch (NoteHelper.AutoPlayMode)
        {
            case AutoPlayMode.Enable:
                tap.judgeGrade = tap.isMine ? JudgeGrade.Miss : JudgeGrade.Perfect;
                tap.isJudged = true;
                tap.diff = 0;
                EndNote(ref tap);
                break;
            case AutoPlayMode.Random:
                // TODO: use guid as seed
                var gradeIndex = new Random(114514).NextInt(1, 14);
                if (tap.isMine)
                    tap.judgeGrade = gradeIndex > 4 ? JudgeGrade.Miss : JudgeGrade.Perfect;
                else
                    tap.judgeGrade = (JudgeGrade)gradeIndex;
                tap.isJudged = true;
                tap.diff = gradeIndex > 7 ? 11.4514f : -11.4514f;
                EndNote(ref tap);
                break;
        }
    }

    private void CheckUpdate(ref TapData tap)
    {
        if (NoteHelper.AutoPlayMode is AutoPlayMode.Enable or AutoPlayMode.Random) return;
        if (tap.isEnd) return;

        var diffSec = TimeDataPtr->NoteTime - tap.time;
        var key = (int)tap.key;

        var stateOn = NoteHelper.SensorStates[key].Status == SensorStatus.On;
        var stateBusy = NoteHelper.SensorStates[key].IsJudging;

        // ---- Mine: touched within window -> Miss, otherwise Perfect once survived.
        //      Resolved independently of the sensor-order gate so it never softlocks. ----
        if (tap.isMine)
        {
            if (stateOn && !stateBusy && diffSec >= -NoteHelper.TAP_JUDGE_GOOD_AREA_MSEC / 1000f)
            {
                tap.judgeGrade = JudgeGrade.Miss;
                tap.isJudged = true;
                tap.diff = diffSec;
                EndNote(ref tap);
                return;
            }
            if (diffSec >= 0.016667f)
            {
                tap.judgeGrade = JudgeGrade.Perfect;
                tap.isJudged = true;
                EndNote(ref tap);
            }
            return;
        }

        // ---- Late timeout (independent of sensor, so an untouched tap still misses) ----
        if (diffSec > 0.15f)
        {
            tap.judgeGrade = JudgeGrade.Miss;
            tap.isJudged = true;
            EndNote(ref tap);
            return;
        }

        // ---- Sensor input judgment ----
        if (!stateOn || stateBusy) return;

        if (diffSec >= -NoteHelper.TAP_JUDGE_GOOD_AREA_MSEC / 1000f)
        {
            var orderIdx = NoteHelper.NextSensorIndex[key];
            if (orderIdx == tap.sensorOrderIndex)
            {
                NoteHelper.SensorStates[key].IsJudging = true;
                NoteHelper.NextSensorIndex[key] = orderIdx + 1;

                tap.judgeGrade = NoteHelper.GetTapJudge(diffSec, tap.isEx);
                tap.isJudged = true;
                tap.diff = diffSec;
                EndNote(ref tap);
            }
        }
    }

    private void EndNote(ref TapData tap)
    {
        NoteHelper.PlayTapSound(new JudgeResult
        {
            Grade = tap.judgeGrade,
            IsBreak = tap.isBreak,
            IsEX = tap.isEx,
            IsMine = tap.isMine,
            Diff = tap.diff
        });
        NoteHelper.PlayJudgeEffect((int)tap.key, tap.judgeGrade, tap.isBreak);
        NoteHelper.PlayFastLateEffect((int)tap.key, tap.judgeGrade);
        NoteHelper.ReportResult(tap.judgeGrade, tap.isBreak, SimaiNoteType.Tap);
        tap.isEnd = true;
    }
}
