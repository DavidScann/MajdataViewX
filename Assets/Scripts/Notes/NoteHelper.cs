using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

using static MajCtx;

[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
public static class NoteHelper
{
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


    private static readonly float3[] _directions = new float3[]
    {
        new(0.38268343f, 0.92387953f, 0),  // A1
        new(0.92387953f, 0.38268343f, 0),  // A2
        new(0.92387953f, -0.38268343f, 0), // A3
        new(0.38268343f, -0.92387953f, 0), // A4
        new(-0.38268343f, -0.92387953f, 0),// A5
        new(-0.92387953f, -0.38268343f, 0),// A6
        new(-0.92387953f, 0.38268343f, 0), // A7
        new(-0.38268343f, 0.92387953f, 0), // A8
    };
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
    public static void GetPosFromDistance(float distance, SensorType key, out float3 result)
    {
        int index = (int)key;
        if (index >= 0 && index < 8)
        {
            result = _directions[index] * distance;
            return;
        }
        result = float3.zero;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
    public static JudgeGrade GetJudgeTap(float diffSec, bool isEx)
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

        if (isEx)
        {
            result = JudgeGrade.Perfect;
        }

        return result;
    }
}