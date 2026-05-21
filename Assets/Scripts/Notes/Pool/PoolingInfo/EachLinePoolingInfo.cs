#nullable enable

/// <summary>
/// EachLine 池化时由 <see cref="DataLoader"/> 填充并交给 <see cref="EachLineDrop.Init"/> 的数据。
/// </summary>
public struct EachLinePoolingInfo
{
    public float Time;
    public int StartPosition;
    public int CurvLength;
    public float Speed;
    public bool UsingSV;
}
