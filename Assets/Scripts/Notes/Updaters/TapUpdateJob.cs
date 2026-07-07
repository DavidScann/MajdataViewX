using System.Threading;
using MajSimai;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using static MajBurst;

[BurstCompile]
public unsafe struct TapUpdateJob : IJobParallelFor
{
    public NativeArray<TapData> taps;

    [NativeDisableParallelForRestriction]
    public NativeArray<LineRenderData> tapLinesRender;
    [NativeDisableParallelForRestriction]
    public NativeArray<NotesRenderData> notesRender;

    [NativeDisableUnsafePtrRestriction]
    public int* tapLinesWriteCountPtr;
    [NativeDisableUnsafePtrRestriction]
    public int* notesWriteCountPtr;

    [NativeDisableUnsafePtrRestriction]
    public bool* SfxRequests;
    [NativeDisableUnsafePtrRestriction]
    public EffectData* JudgeEffectRequests;
    public NativeList<ReportResultEntry>.ParallelWriter ReportResults;

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
        if (tap.IsEnd) return;

        var timing = tap.UsingSV
            ? TimeData.FakeNoteTime - TimeData.GetPositionAtTime(tap.Time)
            : TimeData.NoteTime - tap.Time;

        var rawDistance = timing * tap.Speed + 4.8f;
        var clampedDistance = math.max(rawDistance, 1.225f);

        var destScale = math.min(rawDistance * 0.4f + 0.51f, 1f);
        var lineScale = clampedDistance / 4.8f;

        if (destScale < 0f) return;

        var sortTime = (uint)math.clamp(tap.Time * 100f, 0f, 0xFFFFF);

        // show line
        if (destScale > 0.3f)
        {
            var lineIdx = Interlocked.Increment(ref *tapLinesWriteCountPtr) - 1;
            tapLinesRender[lineIdx] = new LineRenderData()
            {
                angRad = math.radians(tap.Ang),
                scale = lineScale,
                spriteId = tap.LineSprite,
                sort = sortTime,
            };
        }

        // show tap
        NoteHelper.GetPosFromDistance(clampedDistance, tap.Key, out var pos);
        tap.Pos = pos;
        tap.Scale = destScale;

        if (tap.IsBreak)
        {
            var extra = math.max(math.sin(TimeData.GetFrame() * 0.17f) * 0.5f, 0f);
            tap.Brightness = 0.95f + extra;
        }
        if (tap.IsStar && tap.RotateSpeed != 0)
        {
            var deltaRot = -180f * tap.RotateSpeed * TimeData.deltaTime;
            tap.Ang += deltaRot;
        }

        var tapIdx = Interlocked.Increment(ref *notesWriteCountPtr) - 1;
        notesRender[tapIdx] = new NotesRenderData()
        {
            pos = tap.Pos,
            angRad = math.radians(tap.Ang),
            scale = tap.Scale,
            stretchY = 0,
            spriteId = tap.TapSprite,
            color = new float4(1, 1, 1, 1),
            brightness = tap.Brightness,

            exSprite = tap.IsEx ? tap.ExSprite : 0,
            exColor = tap.ExColor,
            sliceBorder = float2.zero,

            sort = sortTime,
        };
    }

    private void AutoplayUpdate(ref TapData tap)
    {
        if (tap.IsEnd) return;

        var timing = TimeData.NoteTime - tap.Time;
        if (timing < -0.01f) return;

        switch (NoteHelper.AutoPlayMode)
        {
            case AutoPlayMode.Enable:
                tap.JudgeGrade = tap.IsMine ? JudgeGrade.Miss : JudgeGrade.Perfect;
                tap.IsJudged = true;
                tap.Diff = 0;
                EndNote(ref tap);
                break;
            case AutoPlayMode.Random:
                // TODO: use guid as seed
                var gradeIndex = new Random(114514).NextInt(1, 14);
                if (tap.IsMine)
                    tap.JudgeGrade = gradeIndex > 4 ? JudgeGrade.Miss : JudgeGrade.Perfect;
                else
                    tap.JudgeGrade = (JudgeGrade)gradeIndex;
                tap.IsJudged = true;
                tap.Diff = gradeIndex > 7 ? 11.4514f : -11.4514f;
                EndNote(ref tap);
                break;
        }
    }

    private void CheckUpdate(ref TapData tap)
    {
        if (NoteHelper.AutoPlayMode is AutoPlayMode.Enable or AutoPlayMode.Random) return;
        if (tap.IsEnd) return;

        var diffSec = TimeData.NoteTime - tap.Time;
        var stateOn = MajBurst.InputData.GetSensorState(tap.Key).Status;

        // ---- Mine: touched within window -> Miss, otherwise Perfect once survived.
        //      Resolved independently of the sensor-order gate so it never softlocks. ----
        if (tap.IsMine)
        {
            if (stateOn && diffSec >= -NoteHelper.TAP_JUDGE_GOOD_AREA_MSEC / 1000f)
            {
                tap.JudgeGrade = JudgeGrade.Miss;
                tap.IsJudged = true;
                tap.Diff = diffSec;
                EndNote(ref tap);
                return;
            }
            if (diffSec >= 0.016667f)
            {
                tap.JudgeGrade = JudgeGrade.Perfect;
                tap.IsJudged = true;
                EndNote(ref tap);
            }
            return;
        }

        // ---- Late timeout (independent of sensor, so an untouched tap still misses) ----
        if (diffSec > 0.15f)
        {
            tap.JudgeGrade = JudgeGrade.Miss;
            tap.IsJudged = true;
            EndNote(ref tap);
            return;
        }

        // ---- Sensor input judgment ----
        if (!stateOn) return;
        if (diffSec < -NoteHelper.TAP_JUDGE_GOOD_AREA_MSEC / 1000f) return;
        if (!MajBurst.InputData.CanJudgeSensor(tap.Key, tap.SensorOrderIndex)) return;

        tap.JudgeGrade = NoteHelper.GetTapJudge(diffSec, tap.IsEx);
        tap.IsJudged = true;
        tap.Diff = diffSec;
        EndNote(ref tap);
    }

    private void EndNote(ref TapData tap)
    {
        NoteHelper.PlayTapSound(SfxRequests,
            tap.JudgeGrade,
            tap.IsBreak,
            tap.IsEx,
            tap.IsMine,
            tap.Diff
        );
        NoteHelper.PlayTapEffect(JudgeEffectRequests,
            (int)tap.Key,
            tap.JudgeGrade,
            tap.IsBreak
        );
        NoteHelper.ReportResult(ReportResults,
            tap.JudgeGrade,
            tap.IsBreak,
            SimaiNoteType.Tap
        );
        MajBurst.InputData.NextTapHold(
            tap.Key
        );
        tap.IsEnd = true;
    }
}
