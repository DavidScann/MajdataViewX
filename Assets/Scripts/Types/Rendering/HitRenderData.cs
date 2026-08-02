
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Mathematics;

public struct HitRenderData : ISortableRenderData
{
    public float2 pos;
    public float radius;
    public float4 color;

    public readonly uint SortKey => 0;
}
