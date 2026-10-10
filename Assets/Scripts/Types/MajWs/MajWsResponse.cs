using MajdataViewX.Types.Enums;
using Newtonsoft.Json;

#nullable enable

namespace MajdataViewX.Types.MajWs
{
    /// <summary>
    /// 服务器 → 客户端 的响应（线格式：JSON 文本帧，Newtonsoft.Json 序列化）。
    /// 字段命名属于线格式契约，必须与 Edit 端 MajWsResponse 完全一致。
    /// </summary>
    public class MajWsResponse
    {
        [JsonProperty("responseType")]
        public MajWsResponseType ResponseType { get; set; }

        [JsonProperty("summary", NullValueHandling = NullValueHandling.Ignore)]
        public ViewSummary? Summary { get; set; }

        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public string? Error { get; set; }
    }

    /// <summary>
    /// 播放器状态快照。State 用 ViewStatus 枚举（两端枚举成员一致，序列化为底层 int）。
    /// </summary>
    public class ViewSummary
    {
        [JsonProperty("state")]
        public ViewStatus State { get; set; }
        [JsonProperty("errMsg")]
        public string ErrMsg { get; set; } = string.Empty;
    }
}
