using Newtonsoft.Json;

namespace MajdataViewX.Types.MajSetting
{
    /// <summary>
    /// 线格式契约：字段命名 / 默认值必须与 Edit 端 MajVolumeSetting 完全一致。
    /// </summary>
    public class MajVolumeSetting
    {
        [JsonProperty("track")]
        public float Track { get; set; } = 0.9f;
        [JsonProperty("answer")]
        public float Answer { get; set; } = 0.9f;
        [JsonProperty("tap")]
        public float Tap { get; set; } = 0.9f;
        [JsonProperty("slide")]
        public float Slide { get; set; } = 0.9f;
        [JsonProperty("break")]
        public float Break { get; set; } = 0.9f;
        [JsonProperty("breakSlide")]
        public float BreakSlide { get; set; } = 0.9f;
        [JsonProperty("ex")]
        public float Ex { get; set; } = 0.9f;
        [JsonProperty("touch")]
        public float Touch { get; set; } = 0.9f;
        [JsonProperty("hanabi")]
        public float Hanabi { get; set; } = 0.9f;
    }
}
