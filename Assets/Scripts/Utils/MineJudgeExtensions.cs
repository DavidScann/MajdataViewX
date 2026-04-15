public static class MineJudgeExtensions
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