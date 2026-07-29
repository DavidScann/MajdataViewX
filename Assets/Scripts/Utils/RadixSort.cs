using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

[BurstCompile]
public static class RadixSort
{
    [BurstCompile]
    public struct RadixSortJob<T> : IJob where T : unmanaged, ISortableRenderData
    {
        public NativeArray<T> Data;
        public NativeArray<T> Temp;
        [NativeDisableUnsafePtrRestriction]
        public unsafe int* CountPtr;
        
        public unsafe void Execute()
        {
            int n = CountPtr == null ? Data.Length : *CountPtr;
            if (n > Data.Length) n = Data.Length;
            if (n < 0) n = 0;
            if (n < 2) return;

            // Sort descending: invert the key
            int* counts = stackalloc int[1024];
            for (int i = 0; i < 1024; i++) counts[i] = 0;
            
            for (int i = 0; i < n; i++)
            {
                uint key = ~Data[i].SortKey;
                counts[(int)(key & 0xFF)]++;
                counts[256 + (int)((key >> 8) & 0xFF)]++;
                counts[512 + (int)((key >> 16) & 0xFF)]++;
                counts[768 + (int)((key >> 24) & 0xFF)]++;
            }
            
            int* offsets = stackalloc int[1024];
            int sum0 = 0, sum1 = 0, sum2 = 0, sum3 = 0;
            for (int i = 0; i < 256; i++)
            {
                offsets[i] = sum0;
                sum0 += counts[i];
                
                offsets[256 + i] = sum1;
                sum1 += counts[256 + i];
                
                offsets[512 + i] = sum2;
                sum2 += counts[512 + i];
                
                offsets[768 + i] = sum3;
                sum3 += counts[768 + i];
            }
            
            for (int i = 0; i < n; i++)
            {
                uint key = ~Data[i].SortKey;
                int dest = offsets[(int)(key & 0xFF)]++;
                Temp[dest] = Data[i];
            }
            
            for (int i = 0; i < n; i++)
            {
                uint key = ~Temp[i].SortKey;
                int dest = offsets[256 + (int)((key >> 8) & 0xFF)]++;
                Data[dest] = Temp[i];
            }
            
            for (int i = 0; i < n; i++)
            {
                uint key = ~Data[i].SortKey;
                int dest = offsets[512 + (int)((key >> 16) & 0xFF)]++;
                Temp[dest] = Data[i];
            }
            
            for (int i = 0; i < n; i++)
            {
                uint key = ~Temp[i].SortKey;
                int dest = offsets[768 + (int)((key >> 24) & 0xFF)]++;
                Data[dest] = Temp[i];
            }
            
        }
    }
}
