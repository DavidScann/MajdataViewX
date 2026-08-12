using MajdataViewX.Base;
using MajdataViewX.Types.Input;
using MajdataViewX.Types.Notes;
using Unity.Burst;
using Unity.Mathematics;
using static MajdataViewX.Managers.SkinManager;

namespace MajdataViewX.Notes.NoteDatas
{
    [BurstCompile]
    public struct TouchData
    {
        public float time;
        public SensorType sensor;
        public float speed;
        public int sensorOrderIndex;

        public bool isHanabi;
        public bool isEach;
        public bool isEx;
        public bool isBreak;
        public bool isMine;
        public bool usingSV;

        public bool isFolded;
        public bool isSlideGuide;

        public bool isAppeared;
        public bool isEnd;
        public float2 centerPos;

        public float wholeDuration;
        public float moveDuration;
        public float displayDuration;

        public float fanAlpha;

        public NoteSp fanSprite;
        public NoteSp pointSprite;
        public NoteSp justSprite;

        public bool isJudged;
        public JudgeGrade judgeGrade;
        public float diff;

        // DJAuto simulation records the exact tick time the press was issued;
        // the render jobs judge with this instead of the render-frame time.
        public bool DjAutoPressed { get; set; }
        public float DjAutoPressTime { get; set; }

        public int groupId;
        public int coverageId;

        public void Init()
        {
            groupId = -1;
            coverageId = -1;
            fanAlpha = 0;

            centerPos = MajPos.GetAreaPos(sensor);

            wholeDuration = 3.209385682f * math.pow(math.abs(speed), -0.9549621752f);
            displayDuration = 0.2f * wholeDuration;
            moveDuration = 0.8f * wholeDuration;

            fanSprite = NoteSp.TOUCH;
            pointSprite = NoteSp.TOUCH_POINT;
            justSprite = NoteSp.TOUCH_JUST;

            if (isEach)
            {
                fanSprite = NoteSp.TOUCH_EACH;
                pointSprite = NoteSp.TOUCH_POINT_EACH;
            }
            if (isBreak)
            {
                fanSprite = NoteSp.TOUCH_BREAK;
                pointSprite = NoteSp.TOUCH_POINT_BREAK;
            }
            if (isMine)
            {
                if (isBreak)
                {
                    fanSprite = NoteSp.TOUCH_BREAK_MINE;
                    pointSprite = NoteSp.TOUCH_POINT_BREAK_MINE;
                }
                else
                {
                    fanSprite = NoteSp.TOUCH_MINE;
                    pointSprite = NoteSp.TOUCH_POINT_MINE;
                }
            }
        }

        public readonly bool IsFoldable(TouchData other) =>
            time == other.time &&
            sensor == other.sensor &&
            speed == other.speed &&

            isHanabi == other.isHanabi &&
            isEach == other.isEach &&
            isEx == other.isEx &&
            isBreak == other.isBreak &&
            isMine == other.isMine &&
            usingSV == other.usingSV;
    }
}
