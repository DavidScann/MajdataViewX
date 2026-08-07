using MajdataViewX.Utils.Extensions;
using Unity.Burst;
using static MajdataViewX.Managers.SkinManager;

namespace MajdataViewX.Notes.NoteDatas
{
    [BurstCompile]
    public struct EachLineData
    {
        public float time;
        public int key;
        public int curvLength;
        public float speed;
        public bool usingSV;

        public bool isEnd;

        public NoteSp lineSprite;
        public float ang;
        public float scale;

        public void Init()
        {
            ang = -45f * key;
            lineSprite = NoteSp.EACH_LINE_0.Offset(curvLength - 1);
        }

        public readonly bool IsFoldable(EachLineData other) =>
            time == other.time &&
            key == other.key &&
            curvLength == other.curvLength &&
            speed == other.speed &&
            usingSV == other.usingSV;
    }
}