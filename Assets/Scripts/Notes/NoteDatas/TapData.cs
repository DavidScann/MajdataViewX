#pragma warning disable CS8500
using Unity.Burst;
using Unity.Mathematics;

using static NoteSkinManager;

[BurstCompile]
public struct TapData
{
    // args TODO:REQUIRED
    public float Time { get; init; }
    public SensorType Key { get; init; }
    public float Speed { get; init; }
    public int ButtonOrderIndex { get; init; }
    public int SensorOrderIndex { get; init; }

    // attrs
    public bool IsStar { get; init; }
    public bool IsDouble { get; init; }
    public float RotateSpeed { get; init; }     // RotateSpeed = 1 时是每秒转 180 度

    public bool IsEach { get; init; }
    public bool IsEx { get; init; }
    public bool IsBreak { get; init; }
    public bool IsMine { get; init; }
    public bool UsingSV { get; init; }

    // outs
    public float2 Pos { get; set; }
    public float Scale { get; set; }
    public float AngleKey { get; set; }
    public float AngleRot { get; set; }
    public float Brightness { get; set; }

    //sprite
    public uint TapSprite { get; set; }
    public uint LineSprite { get; set; }
    public uint ExSprite { get; set; }
    public float4 ExColor { get; set; }

    // state
    public bool IsJudged { get; set; }
    public float Diff { get; set; }
    public JudgeGrade JudgeGrade { get; set; }

    public bool IsEnd { get; set; }

    public void Init()
    {
        Pos = float2.zero;
        Scale = 1f;
        AngleKey = -22.5f + -45f * (int)Key;
        AngleRot = AngleKey;
        Brightness = 1f;

        // Load Skin
        if (IsStar)
        {
            if (IsDouble)
            {
                TapSprite = STAR_DOUBLE;
                LineSprite = LINE_STAR;
                ExSprite = STAR_EX_DOUBLE;
                ExColor = Ex;
                if (IsEach)
                {
                    TapSprite = STAR_EACH_DOUBLE;
                    LineSprite = LINE_EACH;
                    ExColor = Ex_Each;
                }
                if (IsBreak)
                {
                    TapSprite = STAR_BREAK_DOUBLE;
                    LineSprite = LINE_BREAK;
                    ExColor = Ex_Break;
                }
                if (IsMine)
                {
                    if (IsBreak)
                        TapSprite = STAR_BREAK_DOUBLE_MINE;
                    else
                        TapSprite = STAR_MINE_DOUBLE;
                    LineSprite = LINE_MINE;
                }
            }
            else
            {
                TapSprite = STAR;
                LineSprite = LINE_STAR;
                ExSprite = STAR_EX;
                ExColor = Ex;
                if (IsEach)
                {
                    TapSprite = STAR_EACH;
                    LineSprite = LINE_EACH;
                    ExColor = Ex_Each;
                }
                if (IsBreak)
                {
                    TapSprite = STAR_BREAK;
                    LineSprite = LINE_BREAK;
                    ExColor = Ex_Break;
                }
                if (IsMine)
                {
                    if (IsBreak)
                        TapSprite = STAR_BREAK_MINE;
                    else
                        TapSprite = STAR_MINE;
                    LineSprite = LINE_MINE;
                }
            }
        }
        else
        {
            TapSprite = TAP;
            LineSprite = LINE;
            ExSprite = TAP_EX;
            ExColor = Ex;
            if (IsEach)
            {
                TapSprite = TAP_EACH;
                LineSprite = LINE_EACH;
                ExColor = Ex_Each;
            }
            if (IsBreak)
            {
                TapSprite = TAP_BREAK;
                LineSprite = LINE_BREAK;
                ExColor = Ex_Break;
            }
            if (IsMine)
            {
                if (IsBreak)
                    TapSprite = TAP_BREAK_MINE;
                else
                    TapSprite = TAP_MINE;
                LineSprite = LINE_MINE;
            }
        }
    }
}
