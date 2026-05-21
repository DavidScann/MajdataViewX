#nullable enable

/// <summary>
/// TouchHold 池化时由 <see cref="DataLoader"/> 填充并交给 <see cref="TouchHoldDrop.Init"/> 的数据。
/// </summary>
public struct TouchHoldPoolingInfo
{
    public float Time;
    public float LastFor;
    public int StartPosition;
    public char AreaPosition;
    public float Speed;
    public int NoteSortOrder;

    public bool IsEach;
    public bool IsBreak;
    public bool IsMine;
    public bool IsFirework;
    public bool UsingSV;
}
