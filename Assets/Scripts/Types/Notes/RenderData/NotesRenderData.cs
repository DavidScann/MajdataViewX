using System;
using Unity.Mathematics;

public struct NotesRenderData : ISortableRenderData
{
    public float2 pos;
    public float angRad;
    public float scale;
    public float stretchY;
    public uint spriteId;
    public float4 color;
    public float brightness;
    public uint exSprite;
    public float4 exColor;
    public float2 sliceBorder;   // (topFrac, botFrac), (0,0) = normal
    public uint sort;

    public readonly uint SortKey => sort;
}
