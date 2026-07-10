using System;
using Unity.Mathematics;

public struct MaskRenderData : ISortableRenderData
{
    public float2 pos;
    public float angRad;
    public float2 scale;
    public uint spriteId;
    public float4 color;
    public float maskCutoff;
    public uint sort;

    public readonly uint SortKey => sort;
}
