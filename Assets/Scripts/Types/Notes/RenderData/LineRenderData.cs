using System;
using Unity.Mathematics;
using static SkinManager;

public struct LineRenderData : ISortableRenderData
{
    public float angRad;
    public float scale;
    public NoteSp spriteId;
    public uint sort;

    public readonly uint SortKey => sort;
}
