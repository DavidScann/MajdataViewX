public static class JudgeTypeExtensions
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