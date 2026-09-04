using MajdataViewX.Types.Enums;
using MemoryPack;

#nullable enable

namespace MajdataViewX.Types.MajWs
{
    /// <summary>
    /// 服务器 → 客户端 的响应（线格式）。成员顺序必须与 Edit 端一致。
    /// </summary>
    [MemoryPackable]
    public partial class MajWsResponse
    {
        public MajWsResponseType ResponseType { get; set; }
        public ViewSummary Summary { get; set; } = new ViewSummary();
        public string? Error { get; set; }
    }

    /// <summary>
    /// 播放器状态快照。State 直接用 ViewStatus 枚举（两端枚举成员一致，MemoryPack 按底层 int 传输）。
    /// </summary>
    [MemoryPackable]
    public partial class ViewSummary
    {
        public ViewStatus State { get; set; }
        public string ErrMsg { get; set; } = string.Empty;
    }
}
