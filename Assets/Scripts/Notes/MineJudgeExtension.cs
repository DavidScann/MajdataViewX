using Assets.Scripts.Types;

namespace Assets.Scripts.Notes
{
    public static class MineJudgeExtension
    {
        public static JudgeType GetMineJudge(this JudgeType judge)
        {
            return judge switch
            {
                JudgeType.Miss => JudgeType.Perfect,
                _ => JudgeType.Miss
            };
        }
    }
}