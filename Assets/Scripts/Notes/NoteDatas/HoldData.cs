using MajdataViewX.Types.Input;
using MajdataViewX.Types.Notes;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Mathematics;

using static MajdataViewX.Managers.SkinManager;

namespace MajdataViewX.Notes.NoteDatas
{
    [BurstCompile]
    public struct HoldData
    {
        public float time;
        public SensorType Key;
        public float hspeed;
        public readonly float speed => NoteHelper.Settings.TapSpeed * hspeed;
        public float LastFor;
        public int ButtonOrderIndex;
        public int SensorOrderIndex;

        public bool isEach;
        public bool isEx;
        public bool isBreak;
        public bool isMine;
        public bool usingSV;

        public bool isFolded;

        public bool isEnd;
        public float2 pos;
        public float ang;
        public float scale;
        public float stretchY;
        public float2 holdEndPos;
        public float holdEndScale;

        public NoteSp bodySprite;
        public NoteSp _bodySpriteCache;
        public NoteSp endSprite;
        public NoteSp lineSprite;
        public NoteSp exSprite;
        public float4 exColor;

        public float2 sliceBorder;
        public float brightness;

        public bool isHeadJudged;
        public float headDiff;
        public JudgeGrade judgeGrade;
        public bool isHolding;
        public float holdPercent;
        public float playerIdleTimeSec;
        public float lastReleaseTimeSec;

        // DJAuto simulation records the exact tick time the press was issued;
        // the render jobs judge with this instead of the render-frame time.
        public bool DjAutoPressed { get; set; }
        public float DjAutoPressTime { get; set; }

        public void Init()
        {
            pos = float2.zero;
            ang = -22.5f + -45f * (int)Key;
            scale = 1f;

            bodySprite = NoteSp.HOLD;
            endSprite = NoteSp.HOLD_END;
            lineSprite = NoteSp.LINE;
            exSprite = NoteSp.HOLD_EX;
            exColor = Ex;
            sliceBorder = HoldSliceBorder;

            if (isEach)
            {
                bodySprite = NoteSp.HOLD_EACH;
                endSprite = NoteSp.HOLD_END_EACH;
                lineSprite = NoteSp.LINE_EACH;
                exColor = Ex_Each;
            }
            if (isBreak)
            {
                bodySprite = NoteSp.HOLD_BREAK;
                endSprite = NoteSp.HOLD_END_BREAK;
                lineSprite = NoteSp.LINE_BREAK;
                exColor = Ex_Break;
            }
            if (isMine)
            {
                bodySprite = isBreak ? NoteSp.HOLD_BREAK_MINE : NoteSp.HOLD_MINE;
                endSprite = NoteSp.HOLD_END_MINE;
                lineSprite = NoteSp.LINE_MINE;
                exColor = Ex_Mine;
            }

            // 缓存下来，重置要重置回去
            _bodySpriteCache = bodySprite;

            // 一开始放开时间无穷大，按下后才能重置为0，避免一开始就小于release忽略时间
            lastReleaseTimeSec = float.PositiveInfinity;
        }

        public void Reset()
        {
            isEnd = default;

            pos = float2.zero;
            //ang = -22.5f + -45f * (int)Key;
            scale = 1f;
            brightness = 1f;

            stretchY = default;
            holdEndPos = default;
            holdEndScale = default;

            isHeadJudged = default;
            headDiff = default;
            judgeGrade = default;
            isHolding = default;
            holdPercent = default;
            playerIdleTimeSec = default;
            lastReleaseTimeSec = float.PositiveInfinity;

            bodySprite = _bodySpriteCache;
        }

        public readonly bool IsFoldable(HoldData other) =>
            time == other.time &&
            Key == other.Key &&
            speed == other.speed &&
            LastFor == other.LastFor &&

            isEach == other.isEach &&
            isEx == other.isEx &&
            isBreak == other.isBreak &&
            isMine == other.isMine &&
            usingSV == other.usingSV;
    }
}