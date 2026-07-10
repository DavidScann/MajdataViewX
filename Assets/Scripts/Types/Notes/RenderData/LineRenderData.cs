using System;
using Unity.Mathematics;

public struct LineRenderData : ISortableRenderData
{
    public float angRad;
    public float scale;
    public uint spriteId;
    public uint sort;

    public readonly uint SortKey => sort;
}
