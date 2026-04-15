public class MajViewSetting
{
    public float TapSpeed { get; set; } = 7.5f;
    public float TouchSpeed { get; set; } = 7.5f;
    public bool SmoothSlideAnime { get; set; } = true;
    public float BackgroundDim { get; set; } = 0.7f;
    public EditorComboIndicator ComboStatusType { get; set; } = EditorComboIndicator.Combo;
    public JudgeDisplayMode JudgeDisplayMode { get; set; } = JudgeDisplayMode.Both;
    public int OutputFps { get; set; } = 60;
}