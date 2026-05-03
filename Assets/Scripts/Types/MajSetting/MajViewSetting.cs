public class MajViewSetting
{
    public float TapSpeed { get; set; } = 7.5f;
    public float TouchSpeed { get; set; } = 7.5f;
    public bool SmoothSlideAnime { get; set; } = true;
    public float BackgroundDim { get; set; } = 0.7f;
    public BgInfoDisplay ComboStatusType { get; set; } = BgInfoDisplay.Combo;
    public JudgeDisplayMode JudgeDisplayMode { get; set; } = JudgeDisplayMode.Both;
    public AutoPlayMode AutoMode { get; set; } = AutoPlayMode.Enable;
    public int OutputFps { get; set; } = 60;
    public bool UseAlpha { get; set; } = false;
    public UIType UIType { get; set; } = UIType.Legacy;
}