#nullable enable

/// <summary>
/// Wifi 池化时由 <see cref="DataLoader"/> 填充并交给 <see cref="WifiDrop.Init"/> 的数据。
/// </summary>
public struct WifiPoolingInfo
{
    public float Time;
    public float StartTime;
    public float LastFor;
    public int StartPosition;
    public int EndPosition;
    public float Speed;
    public int SortIndex;

    public bool IsJustR;
    public bool IsEach;
    public bool IsBreak;
    public bool IsMine;
    public bool UsingSV;

    public bool SmoothSlideAnime;
}
