#region

using System;
using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

#endregion

public static class WavFileWriter
{
    public static void WriteFile(string filePath, int sampleRate, int channels, float[] dataSource)
    {
        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 65536);
        using var bw = new BinaryWriter(fs);

        bw.Write("RIFF".ToCharArray());
        bw.Write(36 + dataSource.Length * 2);
        bw.Write("WAVE".ToCharArray());
        bw.Write("fmt ".ToCharArray());
        bw.Write(16); // Chunk size
        bw.Write((short)1); // PCM
        bw.Write((short)channels);
        bw.Write(sampleRate);
        bw.Write(sampleRate * channels * 2);
        bw.Write((short)(channels * 2));
        bw.Write((short)16);
        bw.Write("data".ToCharArray());
        bw.Write(dataSource.Length * 2);

        unsafe
        {
            fixed (float* dataPtr = dataSource)
            {
                using var sourceArray = dataSource.AsUnsafeNativeArrayScope();
                var outputArray = new NativeArray<byte>(dataSource.Length * 2, Allocator.TempJob);

                new WriteBufferJob
                {
                    Source = sourceArray.Array,
                    Output = outputArray,
                }.Schedule(dataSource.Length, default).Complete();

                var buffer = outputArray.AsReadOnlySpan();
                bw.Write(buffer);
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
    private struct WriteBufferJob : IJobFor
    {
        [ReadOnly] public NativeArray<float> Source;
        [NativeDisableParallelForRestriction][WriteOnly] public NativeArray<byte> Output;

        public void Execute(int index)
        {
            short s = (short)(math.clamp(Source[index], -1f, 1f) * 32767f);
            Output[index * 2] = (byte)s;
            Output[index * 2 + 1] = (byte)(s >> 8);
        }
    }
}