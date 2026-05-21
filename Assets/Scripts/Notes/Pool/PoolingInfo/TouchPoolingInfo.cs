#nullable enable

/// <summary>
/// Touch 池化时由 <see cref="DataLoader"/> 填充并交给 <see cref="TouchDrop.Init"/> 的数据。
/// </summary>
public struct TouchPoolingInfo
{
    public float Time;
    public int StartPosition;
    public char AreaPosition;
    public float Speed;
    public int NoteSortOrder;

    public bool IsEach;
    public bool IsBreak;
    public bool IsMine;
    public bool IsFirework;
    public bool UsingSV;

    /// <summary>同时刻其他 touch 的分组信息（多 touch 共享判定）。</summary>
    public TouchGroup? GroupInfo;
}
