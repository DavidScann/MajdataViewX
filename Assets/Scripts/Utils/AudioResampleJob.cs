using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct AudioResampleJob : IJob
{
    [ReadOnly] public NativeArray<float> Source;
    public NativeArray<float> Output;
    public float Ratio;
    public int TargetFrames;
    public int SrcFrameLimit;

    public void Execute()
    {
        for (int i = 0; i < TargetFrames; i++)
        {
            float sourceIdx = i * Ratio;
            int i1 = (int)math.floor(sourceIdx);
            int i2 = (i1 < SrcFrameLimit) ? i1 + 1 : i1;

            float frac = sourceIdx - i1;

            int s1 = i1 << 1; 
            int s2 = i2 << 1;
            int d = i << 1;

            // math.lerp 是 Burst 优化的核心
            Output[d] = math.lerp(Source[s1], Source[s2], frac);
            Output[d + 1] = math.lerp(Source[s1 + 1], Source[s2 + 1], frac);
        }
    }
}