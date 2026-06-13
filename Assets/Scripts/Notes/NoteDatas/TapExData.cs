using Unity.Mathematics;

public struct TapExData
{
    public int ViewIndex { get; set; }

    // args
    public bool show;
    public float3 pos;
    public float3 scale;
    public quaternion ang;

    public int sort;
}