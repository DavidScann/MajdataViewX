#nullable enable

/// <summary>
/// Slide 池化时由 <see cref="DataLoader"/> 填充并交给 <see cref="SlideDrop.Init"/> 的数据。
/// </summary>
public struct SlidePoolingInfo
{
    public float Time;
    public float StartTime;
    public float LastFor;
    public int StartPosition;
    public int EndPosition;
    public float Speed;
    public int SortIndex;

    public string SlideShape;   // line3, circle1, ppqq2, wifi, etc.
    public bool IsMirror;
    public bool IsSpecialFlip;
    public bool IsJustR;

    public bool IsEach;
    public bool IsBreak;
    public bool IsMine;
    public bool UsingSV;

    public bool SmoothSlideAnime;

    public ConnSlideInfo ConnectInfo;
}
