using System;
using Unity.Mathematics;

public struct SimpleRenderData : IComparable<SimpleRenderData>, ISortableRenderData
{
    public float2 pos;
    public float angRad;
    public float2 scale;
    public uint spriteId;
    public float4 color;
    public uint sort;

    public readonly int CompareTo(SimpleRenderData o) => o.sort.CompareTo(sort);
    public readonly uint SortKey => sort;
}
