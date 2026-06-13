#pragma warning disable CS8500 // 这会获取托管类型的地址、获取其大小或声明指向它的指针
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
public struct TapData
{
    public int ViewIndex { get; set; }

    // args
    public float time;
    public SensorType key;
    public float speed;

    public int sort;

    // children(through the master's updater to 
    // get ViewIndex, and 'show' controls by master,
    // when the master not show, they will be reset too)
    public TapLineData tapLine;
    public TapExData tapEx;

    // attrs
    public bool isStar;
    public float rotateSpeed;

    public bool isEach;
    public bool isEx;
    public bool isBreak;
    public bool isMine;
    public bool usingSV;

    //---------in args ---------

    //---------out args---------

    // outs
    public bool show;
    public float3 pos;
    public float3 scale;
    public quaternion ang;
    public float brightness;

    // state
    public bool isJudged;
    public float diff;
    public JudgeGrade judgeGrade;

    public bool isEnd;

    public void Init()
    {
        ViewIndex = -1;

        show = false;
        pos = float3.zero;
        scale = float3.zero;
        ang = quaternion.RotateZ(math.radians(-22.5f + -45f * (int)key));

        //tapLine
        tapLine.ViewIndex = -1;

        tapLine.show = false;
        tapLine.pos = float3.zero;
        tapLine.scale = float3.zero;
        tapLine.ang = quaternion.RotateZ(math.radians(-22.5f + -45f * (int)key));
        tapLine.sort = sort;

        //tapEx
        tapEx.ViewIndex = -1;

        tapEx.show = isEx;
        tapEx.pos = float3.zero;
        tapEx.scale = new float3(1, 1, 1);
        tapEx.ang = quaternion.RotateZ(math.radians(-22.5f + -45f * (int)key));
        tapEx.sort = sort;
    }
}


[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
public struct TapUpdateJob : IJobParallelFor
{
    // as _inputManager
    public AutoPlayMode AutoPlayMode;

    // as _timeProvider
    [NativeDisableUnsafePtrRestriction]
    public unsafe BurstTimeData* TimeDataPtr;

    // as _audioManager
    [NativeDisableUnsafePtrRestriction]
    public unsafe bool* SfxRequestsPtr;

    // as _effectManager
    [NativeDisableUnsafePtrRestriction]
    public unsafe JudgeEffectData* JudgeEffectRequestsPtr;
    [NativeDisableUnsafePtrRestriction]
    public unsafe FastLateData* FastLateRequestsPtr;

    // as _objectCounter
    [NativeDisableUnsafePtrRestriction]
    public unsafe ReportResultEntry* ReportRequestsPtr;
    [NativeDisableUnsafePtrRestriction]
    public unsafe int* ReportCountPtr;


    public NativeArray<TapData> taps;

    public void Execute(int index)
    {
        var tap = taps[index];
        TransformUpdate(ref tap);
        AutoplayUpdate(ref tap);
        //CheckUpdate(ref tap);
        taps[index] = tap;
    }

    public unsafe void TransformUpdate(ref TapData tap)
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
        tap.show = true;

        NoteHelper.GetPosFromDistance(clampedDistance, tap.key, out var pos);
        tap.pos = pos;
        tap.scale = new float3(destScale);
        if (rawDistance >= 1.225f)
        {
            tap.tapLine.show = true;
            tap.tapLine.scale = new float3(lineScale);

            if (tap.isBreak)
            {
                var extra = math.max(math.sin(TimeDataPtr->GetFrame() * 0.17f) * 0.5f, 0f);
                tap.brightness = 0.95f + extra;
            }
        }

        if (tap.isStar && tap.rotateSpeed != 0) // star rotate
        {
            var deltaRot = quaternion.RotateZ(math.radians(-180f * tap.rotateSpeed * TimeDataPtr->deltaTime));
            tap.ang = math.mul(tap.ang, deltaRot);
        }

        if (tap.isEx) //sync ex border
        {
            tap.tapEx.pos = tap.pos;
            // tap.tapEx.ang = tap.ang; // 0区别
            tap.tapEx.scale = tap.scale;
        }
    }

    public unsafe void AutoplayUpdate(ref TapData tap)
    {
        if (tap.isEnd) return;

        var timing = TimeDataPtr->NoteTime - tap.time;
        if (timing < 0.01f) return;
        switch (AutoPlayMode)
        {
            case AutoPlayMode.Enable:
                tap.judgeGrade = JudgeGrade.Perfect;
                tap.isJudged = true;
                tap.diff = 0;
                End(ref tap);
                break;
            case AutoPlayMode.Random:
                //TODO: use GUID or something as seed
                var gradeIndex = new Random(114514).NextInt(1, 14);
                if (tap.isMine)
                {
                    tap.judgeGrade = gradeIndex > 4
                        ? JudgeGrade.Miss
                        : JudgeGrade.Perfect;
                }
                else
                {
                    tap.judgeGrade = (JudgeGrade)gradeIndex;
                }
                tap.isJudged = true;
                tap.diff = gradeIndex > 7 ? 11.4514f : -11.4514f;
                End(ref tap);
                break;
            case AutoPlayMode.DJAuto:
                if (tap.isJudged)
                {
                    break;
                }
                if (tap.isMine)
                {
                    break;
                }
                // TODO
                //_inputManager.ClickArea(key);
                break;
        }
    }

    public unsafe void CheckUpdate(ref TapData tap)
    {
        var diffSec = TimeDataPtr->NoteTime - tap.time;

        if (AutoPlayMode is AutoPlayMode.Enable or AutoPlayMode.Random) return;
        if (tap.isJudged) return;

        if (tap.isMine)
        {
            if (diffSec * 1000 <= -NoteHelper.TOUCH_JUDGE_GOOD_AREA_MSEC) //Too Fast
            {
                return;
            }
            if (false) //TODO
            {
                tap.judgeGrade = NoteHelper.GetJudgeTap(diffSec, tap.isEx);
                tap.isJudged = true;
                End(ref tap);
                return;
            }
            if (diffSec >= 0.016667f) //Too Late
            {
                tap.judgeGrade = JudgeGrade.Perfect;
                tap.isJudged = true;
                End(ref tap);
                return;
            }
        }
        else
        {
            if (diffSec > 0.15f)
            {
                tap.judgeGrade = JudgeGrade.Miss;
                tap.isJudged = true;
                End(ref tap);
                return;
            }
        }
    }

    public unsafe void PlaySfx(in JudgeResult judgeResult)
    {
        if (judgeResult.IsMine && judgeResult.IsMissOrTooFast)
        {
            //SfxRequests[AudioManager.MISS] = true;
            return;
        }

        if (judgeResult.IsMissOrTooFast || judgeResult.IsMine)
        {
            return;
        }

        var isBreak = judgeResult.IsBreak;
        var isEx = judgeResult.IsEX;


        if (isBreak)
        {
            switch (judgeResult.Grade)
            {
                case JudgeGrade.LateGood:
                case JudgeGrade.FastGood:
                case JudgeGrade.LateGreat:
                case JudgeGrade.LateGreat2nd:
                case JudgeGrade.LateGreat3rd:
                case JudgeGrade.FastGreat3rd:
                case JudgeGrade.FastGreat2nd:
                case JudgeGrade.FastGreat:
                case JudgeGrade.LatePerfect3rd:
                case JudgeGrade.FastPerfect3rd:
                case JudgeGrade.LatePerfect2nd:
                case JudgeGrade.FastPerfect2nd:
                    SfxRequestsPtr[AudioManager.BREAK_JUDGE] = true;
                    break;
                case JudgeGrade.Perfect:
                    SfxRequestsPtr[AudioManager.BREAK_JUDGE] = true;
                    SfxRequestsPtr[AudioManager.BREAK_SFX] = true;
                    break;
            }
            return;
        }
        else if (isEx)
        {
            SfxRequestsPtr[AudioManager.TAP_EX] = true;
            return;
        }

        switch (judgeResult.Grade)
        {
            case JudgeGrade.LateGood:
            case JudgeGrade.FastGood:
                SfxRequestsPtr[AudioManager.TAP_GOOD] = true;
                //_audioManager.PlaySFX("tap_good.wav");
                break;
            case JudgeGrade.LateGreat:
            case JudgeGrade.LateGreat2nd:
            case JudgeGrade.LateGreat3rd:
            case JudgeGrade.FastGreat3rd:
            case JudgeGrade.FastGreat2nd:
            case JudgeGrade.FastGreat:
                SfxRequestsPtr[AudioManager.TAP_GREAT] = true;
                break;
            case JudgeGrade.LatePerfect3rd:
            case JudgeGrade.FastPerfect3rd:
            case JudgeGrade.LatePerfect2nd:
            case JudgeGrade.FastPerfect2nd:
            case JudgeGrade.Perfect:
                SfxRequestsPtr[AudioManager.TAP_PERFECT] = true;
                break;
        }
    }

    public unsafe void PlayEffect(TapData tap)
    {
        var key = (int)tap.key;
        JudgeEffectRequestsPtr[key].HasEffect = true;
        JudgeEffectRequestsPtr[key].IsBreak = tap.isBreak;
        JudgeEffectRequestsPtr[key].JudgeGrade = tap.judgeGrade;
        FastLateRequestsPtr[key].HasEffect = true;
        FastLateRequestsPtr[key].JudgeGrade = tap.judgeGrade;
    }

    public unsafe void ReportJudge(TapData tap)
    {
        var idx = Interlocked.Increment(ref *ReportCountPtr) - 1;
        ReportRequestsPtr[idx] = new ReportResultEntry
        {
            Grade = tap.judgeGrade,
            IsBreak = tap.isBreak,
        };
    }

    private void End(ref TapData tap)
    {
        PlaySfx(new JudgeResult
        {
            Grade = tap.judgeGrade,
            IsBreak = tap.isBreak,
            IsEX = tap.isEx,
            IsMine = tap.isMine,
            Diff = tap.diff
        });
        PlayEffect(tap);
        ReportJudge(tap);
        tap.show = false;
        tap.isEnd = true;
    }
}