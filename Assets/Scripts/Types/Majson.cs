#region

using System.Collections.Generic;
using MajSimai;

#endregion

internal class Majson
{
    public string artist = "default";
    public string designer = "default";
    public string difficulty = "EZ";
    public int diffNum = 0;
    public string level = "1";
    public List<SimaiTimingPoint> timingList = new();
    public string title = "default";
}
internal class EditRequestjson
{
    public float audioSpeed;
    public float backgroundCover;
    public BgInfoDisplay comboStatusType;
    public EditorPlayMethod editorPlayMethod;
    public EditorControlMethod control;
    public JudgeDisplayMode judgeDisplayMode;
    public string jsonPath;
    public float noteSpeed;
    public long startAt;
    public float startTime;
    public float touchSpeed;
    public bool smoothSlideAnime;
}

public enum BgInfoDisplay
{
    None,
    Combo,
    Achievement_101,
    Achievement_100,
    Achievement,
    AchievementClassical,
    AchievementClassical_100,
    DXScore,
    S_Border,
    SS_Border,
    SSS_Border,
}

internal enum EditorControlMethod
{
    Start,
    Stop,
    OpStart,
    Pause,
    Continue,
    Record
}

public enum EditorPlayMethod
{
    Classic, DJAuto, Random, Disabled
}