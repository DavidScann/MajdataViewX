#pragma warning disable CS8500

using System.Runtime.CompilerServices;
using System.Threading;
using MajSimai;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

using static MajCtx;

[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
public static unsafe class NoteHelper
{
    // ---- Static pointers (set once by managers) ----
    [NativeDisableUnsafePtrRestriction]
    public static bool* SfxRequests;
    [NativeDisableUnsafePtrRestriction]
    public static JudgeEffectData* JudgeEffectRequests;
    [NativeDisableUnsafePtrRestriction]
    public static FastLateData* FastLateRequests;
    [NativeDisableUnsafePtrRestriction]
    public static ReportResultEntry* ReportResults;
    [NativeDisableUnsafePtrRestriction]
    public static int* ReportCount;


    [NativeDisableUnsafePtrRestriction]
    public static SensorState* SensorStates;
    [NativeDisableUnsafePtrRestriction]
    public static int* NextSensorIndex;

    public static AutoPlayMode AutoPlayMode;

    // ---- Judgment constants ----
    public const float TAP_JUDGE_SEG_1ST_PERFECT_MSEC = 1 * FRAME_LENGTH_MSEC;
    public const float TAP_JUDGE_SEG_2ND_PERFECT_MSEC = 2 * FRAME_LENGTH_MSEC;
    public const float TAP_JUDGE_SEG_3RD_PERFECT_MSEC = 3 * FRAME_LENGTH_MSEC;
    public const float TAP_JUDGE_SEG_1ST_GREAT_MSEC = 4 * FRAME_LENGTH_MSEC;
    public const float TAP_JUDGE_SEG_2ND_GREAT_MSEC = 5 * FRAME_LENGTH_MSEC;
    public const float TAP_JUDGE_SEG_3RD_GREAT_MSEC = 6 * FRAME_LENGTH_MSEC;
    public const float TAP_JUDGE_GOOD_AREA_MSEC = 9 * FRAME_LENGTH_MSEC;

    public const float TOUCH_JUDGE_SEG_1ST_PERFECT_MSEC = 9 * FRAME_LENGTH_MSEC;
    public const float TOUCH_JUDGE_SEG_2ND_PERFECT_MSEC = 10.5f * FRAME_LENGTH_MSEC;
    public const float TOUCH_JUDGE_SEG_3RD_PERFECT_MSEC = 12 * FRAME_LENGTH_MSEC;
    public const float TOUCH_JUDGE_SEG_1ST_GREAT_MSEC = 13 * FRAME_LENGTH_MSEC;
    public const float TOUCH_JUDGE_SEG_2ND_GREAT_MSEC = 14 * FRAME_LENGTH_MSEC;
    public const float TOUCH_JUDGE_SEG_3RD_GREAT_MSEC = 15 * FRAME_LENGTH_MSEC;
    public const float TOUCH_JUDGE_GOOD_AREA_MSEC = 18 * FRAME_LENGTH_MSEC;

    public const float TOUCH_DISPLAY_OFFSET_SEC = 0 * FRAME_LENGTH_SEC;
    public const float TOUCH_HOLD_DISPLAY_OFFSET_SEC = 0 * FRAME_LENGTH_SEC;

    public const float HOLD_HEAD_IGNORE_LENGTH_SEC = 6 * FRAME_LENGTH_SEC;
    public const float HOLD_TAIL_IGNORE_LENGTH_SEC = 12 * FRAME_LENGTH_SEC;
    public const float TOUCH_HOLD_HEAD_IGNORE_LENGTH_SEC = 15 * FRAME_LENGTH_SEC;
    public const float TOUCH_HOLD_TAIL_IGNORE_LENGTH_SEC = 12 * FRAME_LENGTH_SEC;
    public const float DELUXE_HOLD_RELEASE_IGNORE_TIME_SEC = 2 * FRAME_LENGTH_SEC;

    private static readonly float2[] _directions = new float2[]
    {
        new(0.38268343f, 0.92387953f),
        new(0.92387953f, 0.38268343f),
        new(0.92387953f, -0.38268343f),
        new(0.38268343f, -0.92387953f),
        new(-0.38268343f, -0.92387953f),
        new(-0.92387953f, -0.38268343f),
        new(-0.92387953f, 0.38268343f),
        new(-0.38268343f, 0.92387953f),
    };

    // ============== Pure Math ==============

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
    public static void GetPosFromDistance(float distance, SensorType key, out float2 result)
    {
        int index = (int)key;
        if (index >= 0 && index < 8)
        {
            result = _directions[index] * distance;
            return;
        }
        result = float2.zero;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
    public static JudgeGrade GetTapJudge(float diffSec, bool isEx)
    {
        var isFast = diffSec < 0;
        var diffMSec = math.abs(diffSec * 1000);
        var result = diffMSec switch
        {
            <= TAP_JUDGE_SEG_1ST_PERFECT_MSEC => JudgeGrade.Perfect,
            <= TAP_JUDGE_SEG_2ND_PERFECT_MSEC => isFast ? JudgeGrade.FastPerfect2nd : JudgeGrade.LatePerfect2nd,
            <= TAP_JUDGE_SEG_3RD_PERFECT_MSEC => isFast ? JudgeGrade.FastPerfect3rd : JudgeGrade.LatePerfect3rd,
            <= TAP_JUDGE_SEG_1ST_GREAT_MSEC => isFast ? JudgeGrade.FastGreat : JudgeGrade.LateGreat,
            <= TAP_JUDGE_SEG_2ND_GREAT_MSEC => isFast ? JudgeGrade.FastGreat2nd : JudgeGrade.LateGreat2nd,
            <= TAP_JUDGE_SEG_3RD_GREAT_MSEC => isFast ? JudgeGrade.FastGreat3rd : JudgeGrade.LateGreat3rd,
            _ => isFast ? JudgeGrade.FastGood : JudgeGrade.LateGood
        };

        if (isEx) result = JudgeGrade.Perfect;
        return result;
    }

    /// <summary>
    /// Adjusts a hold/touch-hold head judgement by how long the body was actually held.
    /// Port of the legacy HoldDrop/TouchHoldDrop OnDestroy percent->grade mapping.
    /// <para><paramref name="percent"/> is the held ratio in [0,1]; <paramref name="realityHT"/>
    /// is the effective hold length (already excluding head/tail ignore windows).</para>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
    public static JudgeGrade GetHoldFinalGrade(JudgeGrade head, float percent, float realityHT)
    {
        if (realityHT <= 0f) return head;

        var j = (int)head;
        var isLate = j < 7; // Miss/Late side
        var good = isLate ? JudgeGrade.LateGood : JudgeGrade.FastGood;
        var great = isLate ? JudgeGrade.LateGreat : JudgeGrade.FastGreat;
        var perfect2 = isLate ? JudgeGrade.LatePerfect2nd : JudgeGrade.FastPerfect2nd;

        if (percent >= 1f)
        {
            if (head == JudgeGrade.Miss) return JudgeGrade.LateGood;
            if (math.abs(j - 7) == 6) return great; // head was a Good -> upgrade to Great
            return head;
        }
        if (percent >= 0.67f)
        {
            if (head == JudgeGrade.Miss) return JudgeGrade.LateGood;
            if (math.abs(j - 7) == 6) return great;
            if (head == JudgeGrade.Perfect) return perfect2;
            return head;
        }
        if (percent >= 0.33f)
        {
            if (math.abs(j - 7) >= 6) return good; // Miss or Good -> Good
            return great;
        }
        if (percent >= 0.05f) return good;
        if (head == JudgeGrade.Miss) return JudgeGrade.Miss;
        return good;
    }


    // ============== SFX (write to AudioManager's NativeArray) ==============

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PlayTapSound(in JudgeResult judgeResult)
    {
        if (judgeResult.IsMine && judgeResult.IsMissOrTooFast)
            return;

        if (judgeResult.IsMissOrTooFast || judgeResult.IsMine)
            return;

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
                    SfxRequests[AudioManager.BREAK_JUDGE] = true;
                    break;
                case JudgeGrade.Perfect:
                    SfxRequests[AudioManager.BREAK_JUDGE] = true;
                    SfxRequests[AudioManager.BREAK_SFX] = true;
                    break;
            }
            return;
        }

        if (isEx)
        {
            SfxRequests[AudioManager.TAP_EX] = true;
            return;
        }

        switch (judgeResult.Grade)
        {
            case JudgeGrade.LateGood:
            case JudgeGrade.FastGood:
                SfxRequests[AudioManager.TAP_GOOD] = true;
                break;
            case JudgeGrade.LateGreat:
            case JudgeGrade.LateGreat2nd:
            case JudgeGrade.LateGreat3rd:
            case JudgeGrade.FastGreat3rd:
            case JudgeGrade.FastGreat2nd:
            case JudgeGrade.FastGreat:
                SfxRequests[AudioManager.TAP_GREAT] = true;
                break;
            case JudgeGrade.LatePerfect3rd:
            case JudgeGrade.FastPerfect3rd:
            case JudgeGrade.LatePerfect2nd:
            case JudgeGrade.FastPerfect2nd:
            case JudgeGrade.Perfect:
                SfxRequests[AudioManager.TAP_PERFECT] = true;
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PlayHoldSound(in JudgeResult judgeResult)
    {
        PlayTapSound(judgeResult);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PlayTouchSound()
    {
        SfxRequests[AudioManager.TOUCH] = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PlayHanabiSound()
    {
        SfxRequests[AudioManager.FIREWORK] = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PlaySlideSound(bool isBreak)
    {
        if (isBreak)
            SfxRequests[AudioManager.BREAK_SLIDE] = true;
        else
            SfxRequests[AudioManager.SLIDE] = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PlayBreakSlideEndSound()
    {
        SfxRequests[AudioManager.BREAK_SLIDE_JUDGE] = true;
        SfxRequests[AudioManager.BREAK_SFX] = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetTouchHoldSound(bool on)
    {
        SfxRequests[AudioManager.TOUCHHOLD] = on;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PlayAllPerfectSound()
    {
        SfxRequests[AudioManager.ALL_PERFECT] = true;
    }

    // ============== Effect (write to EffectManager's NativeArray) ==============

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PlayJudgeEffect(int key, JudgeGrade judge, bool isBreak)
    {
        JudgeEffectRequests[key].HasEffect = true;
        JudgeEffectRequests[key].IsBreak = isBreak;
        JudgeEffectRequests[key].JudgeGrade = judge;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PlayFastLateEffect(int key, JudgeGrade judge)
    {
        FastLateRequests[key].HasEffect = true;
        FastLateRequests[key].JudgeGrade = judge;
    }

    // ============== Report (write to ObjectCounter's NativeArray) ==============

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReportResult(JudgeGrade grade, bool isBreak, SimaiNoteType noteType)
    {
        var idx = Interlocked.Increment(ref *ReportCount) - 1;
        ReportResults[idx] = new ReportResultEntry
        {
            Grade = grade,
            IsBreak = isBreak,
            NoteType = noteType,
        };
    }
}
