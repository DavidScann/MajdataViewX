using Unity.Mathematics;

namespace MajdataViewX.Types.Rendering
{
    public struct HitRenderData : ISortableRenderData
    {
        public float2 pos;
        public float radius;
        public float4 color;

        public readonly uint SortKey => 0;
    }
}