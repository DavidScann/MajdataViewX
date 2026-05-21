#nullable enable

/// <summary>
/// Tap 池化时由 <see cref="DataLoader"/> 填充并交给 <see cref="TapDrop.Init"/> 的数据。
/// </summary>
public struct TapPoolingInfo
{
    public float Time;
    public int StartPosition;
    public float Speed;
    public int NoteSortOrder;

    public bool IsEach;
    public bool IsBreak;
    public bool IsEx;
    public bool IsMine;
    public bool UsingSV;
}
