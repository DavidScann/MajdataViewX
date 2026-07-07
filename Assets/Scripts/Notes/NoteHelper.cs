#pragma warning disable CS8500

using System.Runtime.CompilerServices;
using System.Threading;
using MajSimai;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using static MajCtx;

[BurstCompile]
public static unsafe class NoteHelper
{
    public static readonly SharedStatic<AutoPlayMode> AutoPlayModeSS =
        SharedStatic<AutoPlayMode>.GetOrCreate<InputManager>();
    public static AutoPlayMode AutoPlayMode =>
        AutoPlayModeSS.Data;

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

    // ============== Pure Math ==============

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [BurstCompile]
    public static void GetPosFromDistance(float distance, SensorType key, out float2 result)
    {
        int index = (int)key;
        if (index >= 0 && index < 8)
        {
            float2 dir = index switch
            {
                0 => new(0.38268343f, 0.92387953f),
                1 => new(0.92387953f, 0.38268343f),
                2 => new(0.92387953f, -0.38268343f),
                3 => new(0.38268343f, -0.92387953f),
                4 => new(-0.38268343f, -0.92387953f),
                5 => new(-0.92387953f, -0.38268343f),
                6 => new(-0.92387953f, 0.38268343f),
                7 => new(-0.38268343f, 0.92387953f),
                _ => new(0, 0)
            };
            result = dir * distance;
            return;
        }
        result = float2.zero;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [BurstCompile]
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
    [BurstCompile]
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


    // ============== SFX ==============
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [BurstCompile]
    public static void PlayTapSound(bool* SfxRequests,
        JudgeGrade grade, bool isBreak, bool isEx, bool isMine, float diff)
    {
        if (isMine &&
            grade is JudgeGrade.Miss or JudgeGrade.TooFast)
            return;

        if (grade is JudgeGrade.Miss or JudgeGrade.TooFast || isMine)
            return;

        if (isBreak)
        {
            switch (grade)
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

        switch (grade)
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
    public static void PlayHoldSound(bool* SfxRequests,
        JudgeGrade grade, bool isBreak, bool isEx, bool isMine, float diff)
    {
        PlayTapSound(SfxRequests, grade, isBreak, isEx, isMine, diff);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PlayTouchSound(bool* SfxRequests)
    {
        SfxRequests[AudioManager.TOUCH] = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PlayHanabiSound(bool* SfxRequests)
    {
        SfxRequests[AudioManager.FIREWORK] = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PlaySlideSound(bool* SfxRequests, bool isBreak)
    {
        if (isBreak)
        {
            SfxRequests[AudioManager.BREAK_SLIDE] = true;
        }
        // 官机上无论如何都会播放普通 slide 的启动音
        SfxRequests[AudioManager.SLIDE] = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PlayBreakSlideEndSound(bool* SfxRequests)
    {
        SfxRequests[AudioManager.BREAK_SLIDE_JUDGE] = true;
        // SfxRequests[AudioManager.BREAK_SFX] = true;  // blame @LeZi9916
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetTouchHoldSound(bool* SfxRequests, bool on)
    {
        SfxRequests[AudioManager.TOUCHHOLD] = on;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PlayAllPerfectSound(bool* SfxRequests)
    {
        SfxRequests[AudioManager.ALL_PERFECT] = true;
    }

    // ============== Effect ==============

    /// <summary>
    /// 播放tap类型打击特效
    /// </summary>
    /// <param name="key">对应的button/sensor位置，注意sensor需要+8，以区分键与A区</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PlayTapEffect(
        EffectData* JudgeEffectRequests,
        int key, JudgeGrade judge, bool isBreak)
    {
        JudgeEffectRequests[key].Effect = EffectType.Tap;
        JudgeEffectRequests[key].IsBreak = isBreak;
        JudgeEffectRequests[key].JudgeGrade = judge;
    }

    /// <summary>
    /// 播放touch类型打击特效
    /// </summary>
    /// <param name="key">对应的button/sensor位置，注意sensor需要+8，以区分键与A区</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PlayTouchEffect(
        EffectData* JudgeEffectRequests,
        int key, JudgeGrade judge, bool isBreak)
    {
        JudgeEffectRequests[key].Effect = EffectType.Touch;
        JudgeEffectRequests[key].IsBreak = isBreak;
        JudgeEffectRequests[key].JudgeGrade = judge;
    }

    /// <summary>
    /// 播放hold按住特效
    /// </summary>
    /// <param name="key">对应的button/sensor位置，注意sensor需要+8，以区分键与A区</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetHoldEffect(
        EffectData* JudgeEffectRequests,
        int key, JudgeGrade judge, bool hasHolding)
    {
        JudgeEffectRequests[key].HasHolding = hasHolding;
        if (!hasHolding) return;

        switch (judge)
        {
            case JudgeGrade.LateGood:
            case JudgeGrade.FastGood:
                JudgeEffectRequests[key].HoldingColor = new Color(0.56f, 1f, 0.59f); // Green
                break;
            case JudgeGrade.LateGreat:
            case JudgeGrade.LateGreat2nd:
            case JudgeGrade.LateGreat3rd:
            case JudgeGrade.FastGreat3rd:
            case JudgeGrade.FastGreat2nd:
            case JudgeGrade.FastGreat:
                JudgeEffectRequests[key].HoldingColor = new Color(1f, 0.70f, 0.94f); // Pink
                break;
            case JudgeGrade.LatePerfect3rd:
            case JudgeGrade.FastPerfect3rd:
            case JudgeGrade.LatePerfect2nd:
            case JudgeGrade.FastPerfect2nd:
            case JudgeGrade.Perfect:
                JudgeEffectRequests[key].HoldingColor = new Color(1f, 0.93f, 0.61f); // Green
                break;
            case JudgeGrade.Miss:
            case JudgeGrade.TooFast:
                JudgeEffectRequests[key].HoldingColor = new Color(1f, 1f, 1f); // White
                break;
        }
    }

    // ============== Report ==============

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReportResult(
        NativeList<ReportResultEntry>.ParallelWriter ReportResults,
        JudgeGrade grade, bool isBreak, SimaiNoteType noteType)
    {
        ReportResults.AddNoResize(new ReportResultEntry
        {
            Grade = grade,
            IsBreak = isBreak,
            NoteType = noteType,
        });
    }
}
