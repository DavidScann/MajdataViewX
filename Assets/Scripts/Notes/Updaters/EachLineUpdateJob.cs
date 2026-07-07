#pragma warning disable CS8500
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

using static MajBurst;

[BurstCompile]
public unsafe struct EachLineUpdateJob : IJobParallelFor
{
    public NativeArray<EachLineData> eachLines;

    [NativeDisableParallelForRestriction]
    public NativeArray<LineRenderData> eachLinesRender;
    [NativeDisableUnsafePtrRestriction]
    public int* EachLinesWriteCountPtr;

    public void Execute(int index)
    {
        var el = eachLines[index];
        if (el.isEnd) return;

        var timing = el.usingSV
            ? TimeData.FakeNoteTime - TimeData.GetPositionAtTime(el.time)
            : TimeData.NoteTime - el.time;

        if (timing >= 0)
        {
            el.isEnd = true;
            return;
        }

        var rawDistance = timing * el.speed + 4.8f;
        var clampedDistance = math.max(rawDistance, 1.225f);
        var destScale = math.min(rawDistance * 0.4f + 0.51f, 1f);

        if (destScale < 0f) return;

        el.scale = clampedDistance / 4.8f;

        var idx = Interlocked.Increment(ref *EachLinesWriteCountPtr) - 1;
        eachLinesRender[idx] = new LineRenderData
        {
            angRad = math.radians(el.ang),
            scale = el.scale,
            spriteId = el.lineSprite,
            sort = (uint)math.clamp(el.time * 100f, 0f, 0xFFFFF),
        };

        eachLines[index] = el;
    }
}