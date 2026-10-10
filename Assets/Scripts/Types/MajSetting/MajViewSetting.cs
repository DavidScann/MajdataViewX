using MajdataViewX.Types.Enums;
using MajdataViewX.Types.Rendering;
using Newtonsoft.Json;

namespace MajdataViewX.Types.MajSetting
{
    /// <summary>
    /// 线格式契约：成员声明顺序在 JSON 中无意义，但字段命名 / 默认值 / 类型必须与 Edit 端 MajViewSetting 完全一致。
    /// </summary>
    public class MajViewSetting
    {
        [JsonProperty("tapSpeed")]
        public float TapSpeed { get; set; } = 7.5f;

        [JsonProperty("touchSpeed")]
        public float TouchSpeed { get; set; } = 7.5f;

        [JsonProperty("smoothSlideAnime")]
        public bool SmoothSlideAnime { get; set; } = false;

        [JsonProperty("backgroundDim")]
        public float BackgroundDim { get; set; } = 0.7f;

        [JsonProperty("backgroundOutsideDim")]
        public float BackgroundOutsideDim { get; set; } = 0.3f;

        [JsonProperty("comboStatusType")]
        public BgInfoDisplay ComboStatusType { get; set; } = BgInfoDisplay.Combo;

        [JsonProperty("judgeDisplayMode")]
        public JudgeDisplayMode JudgeDisplayMode { get; set; } = JudgeDisplayMode.Both;

        [JsonProperty("autoMode")]
        public AutoPlayMode AutoMode { get; set; } = AutoPlayMode.Enable;

        /// <summary>Absolute perfect: the fixed-step sim grades autoplay with exact
        /// tick times. Off = practical play: upstream hands perform within physical limits.</summary>
        [JsonProperty("absolutePerfect")]
        public bool AbsolutePerfect { get; set; } = true;

        [JsonProperty("showHand")]
        public bool ShowHand { get; set; } = false;

        /// <summary>Wide hands: DJAuto's hands sweep adjacent sensors more easily (realistic sliding, DJAuto only).</summary>
        [JsonProperty("wideHands")]
        public bool WideHands { get; set; } = false;

        [JsonProperty("outputFps")]
        public int OutputFps { get; set; } = 60;

        [JsonProperty("exportQuality")]
        public ExportQuality ExportQuality { get; set; } = ExportQuality.High;

        [JsonProperty("exportWidth")]
        public int ExportWidth { get; set; } = 0;

        [JsonProperty("exportHeight")]
        public int ExportHeight { get; set; } = 0;

        [JsonProperty("exportCodec")]
        public ExportCodec ExportCodec { get; set; } = ExportCodec.H264;

        [JsonProperty("resizeBg")]
        public bool ResizeBg { get; set; } = false;

        [JsonProperty("uiType")]
        public UIType UIType { get; set; } = UIType.Legacy;

        [JsonProperty("globalAudioOffset")]
        public double GlobalAudioOffset { get; set; } = 0;

        [JsonProperty("legacySlideLayer")]
        public bool LegacySlideLayer { get; set; } = false;

        [JsonProperty("mineAutoSlide")]
        public bool MineAutoSlide { get; set; } = true;
    }
}
