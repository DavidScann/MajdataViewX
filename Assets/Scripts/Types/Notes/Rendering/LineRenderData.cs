using System;
using Unity.Mathematics;

public struct LineRenderData : IComparable<LineRenderData>
{
    public float angRad;
    public float scale;
    public uint spriteId;
    public uint sort;

    public readonly int CompareTo(LineRenderData o) => o.sort.CompareTo(sort);
}
