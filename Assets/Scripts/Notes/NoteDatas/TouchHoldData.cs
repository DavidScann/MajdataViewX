using MajdataViewX.Base;
using MajdataViewX.Types.Input;
using MajdataViewX.Types.Notes;
using Unity.Burst;
using Unity.Mathematics;
using static MajdataViewX.Managers.SkinManager;

namespace MajdataViewX.Notes.NoteDatas
{
    [BurstCompile]
    public struct TouchHoldData
    {
        public float time;
        public SensorType sensor;
        public float speed;
        public float LastFor;
        public int sensorOrderIndex;

        public bool isHanabi;
        public bool isEach;
        public bool isEx;
        public bool isBreak;
        public bool isMine;
        public bool usingSV;

        public bool isFolded;

        public bool isEnd;
        public float2 centerPos;

        public float fanAlpha;
        public float maskProgress;

        public NoteSp fanSprite;
        public NoteSp pointSprite;
        public NoteSp borderSprite;
        public NoteSp _borderOnSpriteCache;

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

        // 头判参与 touchGroup（与 touch 的 groupId 同语义，和同 timing 的 touch 一起多数通过）
        public int headGroupId;
        public int headCoverageId;
        // 按下 group：touchhold 专属的 touchHoldGroup（hold 期间多数按下）
        public int groupId;
        public int coverageId;

        public void Init()
        {
            headGroupId = -1;
            headCoverageId = -1;
            groupId = -1;
            coverageId = -1;
            fanAlpha = 0;
            maskProgress = 0;

            centerPos = MajPos.GetAreaPos(sensor);

            fanSprite = NoteSp.TOUCH_HOLD_0;
            pointSprite = NoteSp.TOUCH_POINT;
            _borderOnSpriteCache = borderSprite = NoteSp.TOUCH_HOLD_BORDER;

            if (isBreak)
            {
                fanSprite = NoteSp.TOUCH_HOLD_BREAK_0;
                pointSprite = NoteSp.TOUCH_POINT_BREAK;
                _borderOnSpriteCache = borderSprite = NoteSp.TOUCH_HOLD_BORDER_BREAK;
            }
            if (isMine)
            {
                fanSprite = NoteSp.TOUCH_HOLD_MINE_0;
                pointSprite = NoteSp.TOUCH_POINT_MINE;
                if (isBreak)
                    _borderOnSpriteCache = borderSprite = NoteSp.TOUCH_HOLD_BORDER_BREAK_MINE;
                else
                    _borderOnSpriteCache = borderSprite = NoteSp.TOUCH_HOLD_BORDER_MINE;
            }

            // 一开始放开时间无穷大，按下后才能重置为0，避免一开始就小于release忽略时间
            lastReleaseTimeSec = float.PositiveInfinity;
        }

        public readonly bool IsFoldable(TouchHoldData other) =>
            time == other.time &&
            sensor == other.sensor &&
            speed == other.speed &&
            LastFor == other.LastFor &&

            isEach == other.isEach &&
            isEx == other.isEx &&
            isBreak == other.isBreak &&
            isMine == other.isMine &&
            usingSV == other.usingSV;

    }
}