#nullable enable

using UnityEngine;

/// <summary>
/// Star 池化时由 <see cref="DataLoader"/> 填充并交给 <see cref="StarDrop.Init"/> 的数据。
/// 适用普通 slide 头星与 force-star。
/// </summary>
public struct StarPoolingInfo
{
    public float Time;
    public int StartPosition;
    public float Speed;
    public int NoteSortOrder;
    public float RotateSpeed;

    public bool IsEach;
    public bool IsBreak;
    public bool IsEx;
    public bool IsMine;
    public bool UsingSV;

    public bool IsDouble;
    public bool IsNoHead;
    public bool IsFakeStar;
    public bool IsFakeStarRotate;
}
