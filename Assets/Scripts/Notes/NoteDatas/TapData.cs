using MajdataViewX.Types.Input;
using MajdataViewX.Types.Notes;
using Unity.Burst;
using Unity.Mathematics;
using static MajdataViewX.Managers.SkinManager;

namespace MajdataViewX.Notes.NoteDatas
{
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
        public bool IsDouble { get; set; }
        public float RotateSpeed { get; set; }     // RotateSpeed = 1 时是每秒转 180 度

        public bool IsEach { get; init; }
        public bool IsEx { get; init; }
        public bool IsBreak { get; init; }
        public bool IsMine { get; init; }
        public bool UsingSV { get; init; }

        public bool IsFolded { get; set; }
        public bool IsSlideGuide { get; set; }

        // outs
        public float2 Pos { get; set; }
        public float Scale { get; set; }
        public float AngleKey { get; set; }
        public float AngleRot { get; set; }
        public float Brightness { get; set; }

        //sprite
        public NoteSp TapSprite { get; set; }
        public NoteSp LineSprite { get; set; }
        public NoteSp ExSprite { get; set; }
        public float4 ExColor { get; set; }

        // state
        public bool IsJudged { get; set; }
        public float Diff { get; set; }
        public JudgeGrade JudgeGrade { get; set; }

        // DJAuto simulation records the exact tick time the press was issued;
        // the render jobs judge with this instead of the render-frame time.
        public bool DjAutoPressed { get; set; }
        public float DjAutoPressTime { get; set; }

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
                    TapSprite = NoteSp.STAR_DOUBLE;
                    LineSprite = NoteSp.LINE_STAR;
                    ExSprite = NoteSp.STAR_EX_DOUBLE;
                    ExColor = Ex_Star;
                    if (IsEach)
                    {
                        TapSprite = NoteSp.STAR_EACH_DOUBLE;
                        LineSprite = NoteSp.LINE_EACH;
                        ExColor = Ex_Each;
                    }
                    if (IsBreak)
                    {
                        TapSprite = NoteSp.STAR_BREAK_DOUBLE;
                        LineSprite = NoteSp.LINE_BREAK;
                        ExColor = Ex_Break;
                    }
                    if (IsMine)
                    {
                        TapSprite = IsBreak ? NoteSp.STAR_BREAK_DOUBLE_MINE : NoteSp.STAR_MINE_DOUBLE;
                        LineSprite = NoteSp.LINE_MINE;
                        ExColor = Ex_Mine;
                    }
                }
                else
                {
                    TapSprite = NoteSp.STAR;
                    LineSprite = NoteSp.LINE_STAR;
                    ExSprite = NoteSp.STAR_EX;
                    ExColor = Ex_Star;
                    if (IsEach)
                    {
                        TapSprite = NoteSp.STAR_EACH;
                        LineSprite = NoteSp.LINE_EACH;
                        ExColor = Ex_Each;
                    }
                    if (IsBreak)
                    {
                        TapSprite = NoteSp.STAR_BREAK;
                        LineSprite = NoteSp.LINE_BREAK;
                        ExColor = Ex_Break;
                    }
                    if (IsMine)
                    {
                        TapSprite = IsBreak ? NoteSp.STAR_BREAK_MINE : NoteSp.STAR_MINE;
                        LineSprite = NoteSp.LINE_MINE;
                        ExColor = Ex_Mine;
                    }
                }
            }
            else
            {
                TapSprite = NoteSp.TAP;
                LineSprite = NoteSp.LINE;
                ExSprite = NoteSp.TAP_EX;
                ExColor = Ex;
                if (IsEach)
                {
                    TapSprite = NoteSp.TAP_EACH;
                    LineSprite = NoteSp.LINE_EACH;
                    ExColor = Ex_Each;
                }
                if (IsBreak)
                {
                    TapSprite = NoteSp.TAP_BREAK;
                    LineSprite = NoteSp.LINE_BREAK;
                    ExColor = Ex_Break;
                }
                if (IsMine)
                {
                    TapSprite = IsBreak ? NoteSp.TAP_BREAK_MINE : NoteSp.TAP_MINE;
                    LineSprite = NoteSp.LINE_MINE;
                    ExColor = Ex_Mine;
                }
            }

            // Each Tap Line优先级高于break低于mine
            if (IsEach && !IsMine)
                LineSprite = NoteSp.LINE_EACH;
        }

        public readonly bool IsFoldable(TapData other) =>
            Time == other.Time &&
            Key == other.Key &&
            Speed == other.Speed &&

            IsStar == other.IsStar &&
            IsDouble == other.IsDouble &&
            RotateSpeed == other.RotateSpeed &&

            IsEach == other.IsEach &&
            IsEx == other.IsEx &&
            IsBreak == other.IsBreak &&
            IsMine == other.IsMine &&
            UsingSV == other.UsingSV;
    }
}