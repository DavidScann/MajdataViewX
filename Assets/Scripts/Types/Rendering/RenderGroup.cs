using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

using Unity.Jobs;

public class RenderGroup<T> : IDisposable where T : unmanaged, ISortableRenderData
{
    const int MULTIPLE_COUNT = 3;
    int _maxInstances;

    GraphicsBuffer[] _buffers = new GraphicsBuffer[MULTIPLE_COUNT];
    GraphicsBuffer[] _argsBuffers = new GraphicsBuffer[MULTIPLE_COUNT];
    GraphicsBuffer.IndirectDrawIndexedArgs[][] _args = new GraphicsBuffer.IndirectDrawIndexedArgs[MULTIPLE_COUNT][];
    NativeArray<int> _counts;
    NativeArray<T> _sortTemp;

    MaterialPropertyBlock _mpb;
    RenderParams _rp;
    Mesh _mesh;

    int _writeIndex = -1;
    int _renderIndex = -1;

    NativeArray<T> _noteRenderDatasThisFrame;

    public RenderGroup(Material mat, Mesh mesh, int priority, int maxInstances = 65536)
    {
        _mesh = mesh;
        _mpb = new();
        _rp = new(mat)
        {
            worldBounds = new Bounds(new Vector3(0, 0, -0.1f * priority), Vector3.one * 10000),
            //rendererPriority = rendererPriority,
            matProps = _mpb
        };

        _maxInstances = maxInstances;

        uint quadIndexCount = mesh.GetIndexCount(0);

        for (int i = 0; i < MULTIPLE_COUNT; i++)
        {
            _buffers[i] = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                _maxInstances,
                UnsafeUtility.SizeOf<T>());

            _argsBuffers[i] = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments, 1,
                GraphicsBuffer.IndirectDrawIndexedArgs.size);
            _args[i] = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
            _args[i][0].indexCountPerInstance = quadIndexCount;
        }

        _counts = new NativeArray<int>(MULTIPLE_COUNT, Allocator.Persistent);
        _sortTemp = new NativeArray<T>(_maxInstances, Allocator.Persistent);
    }

    public void AdvanceWrite()
    {
        _writeIndex = (_writeIndex + 1) % MULTIPLE_COUNT;
    }

    public NativeArray<T> LockForWrite()
    {
        _noteRenderDatasThisFrame = _buffers[_writeIndex].LockBufferForWrite<T>(0, _maxInstances);
        return _noteRenderDatasThisFrame;
    }

    public unsafe void UnlockWrite(bool sort = true)
    {
        var count = Math.Min(_counts[_writeIndex], _maxInstances);
        _counts[_writeIndex] = count;
        if (sort && count > 1)
        {
            new RadixSort.RadixSortJob<T>
            {
                Data = _noteRenderDatasThisFrame,
                Temp = _sortTemp,
                CountPtr = WriteCountPtr
            }.Run();
        }
        _buffers[_writeIndex].UnlockBufferAfterWrite<T>(count);
    }

    public unsafe JobHandle ScheduleSort(JobHandle dependency)
    {
        return new RadixSort.RadixSortJob<T>
        {
            Data = _noteRenderDatasThisFrame,
            Temp = _sortTemp,
            CountPtr = WriteCountPtr
        }.Schedule(dependency);
    }

    public unsafe int* WriteCountPtr
    {
        get { return (int*)_counts.GetUnsafePtr() + _writeIndex; }
    }

    public void ResetCount()
    {
        _counts[_writeIndex] = 0;
    }

    public int RenderCount
    {
        get { return _renderIndex >= 0 ? _counts[_renderIndex] : 0; }
    }

    public void Render()
    {
        if (_renderIndex < 0) return;
        int count = _counts[_renderIndex];
        if (count == 0) return;

        _mpb.SetBuffer("_NoteBuffer", _buffers[_renderIndex]);

        var args = _args[_renderIndex];
        args[0].instanceCount = (uint)count;
        _argsBuffers[_renderIndex].SetData(args, 0, 0, 1);

        Graphics.RenderMeshIndirect(_rp, _mesh, _argsBuffers[_renderIndex]);
    }

    public void Swap()
    {
        _renderIndex = _writeIndex;
    }

    public void Dispose()
    {
        for (int i = 0; i < MULTIPLE_COUNT; i++)
        {
            _buffers[i]?.Dispose();
            _argsBuffers[i]?.Dispose();
        }
        if (_counts.IsCreated) _counts.Dispose();
        if (_sortTemp.IsCreated) _sortTemp.Dispose();

        GC.SuppressFinalize(this);
    }
}
