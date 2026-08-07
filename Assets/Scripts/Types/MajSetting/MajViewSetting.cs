using MajdataViewX.Types.Enums;
using MajdataViewX.Types.Rendering;

namespace MajdataViewX.Types.MajSetting
{
    public class MajViewSetting
    {
        public float TapSpeed { get; set; } = 7.5f;
        public float TouchSpeed { get; set; } = 7.5f;
        public bool SmoothSlideAnime { get; set; } = false;
        public float BackgroundDim { get; set; } = 0.7f;
        public float BackgroundOutsideDim { get; set; } = 0.3f;
        public BgInfoDisplay ComboStatusType { get; set; } = BgInfoDisplay.Combo;
        public JudgeDisplayMode JudgeDisplayMode { get; set; } = JudgeDisplayMode.Both;
        public AutoPlayMode AutoMode { get; set; } = AutoPlayMode.Enable;
        public bool ShowHand { get; set; } = false;
        public int OutputFps { get; set; } = 60;
        public ExportQuality ExportQuality { get; set; } = ExportQuality.High;
        public bool ResizeBg { get; set; } = false;
        public UIType UIType { get; set; } = UIType.Legacy;
        public double GlobalAudioOffset { get; set; } = 0;
        public bool LegacySlideLayer { get; set; } = false;
        public bool MineAutoSlide { get; set; } = true;
    }
}