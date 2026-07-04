using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

public class NoteRenderGroup<T> : IDisposable where T : unmanaged, IComparable<T>
{
    const int TRIPLE_COUNT = 3;
    const int MAX_INSTANCES = 65536;

    GraphicsBuffer[] _buffers = new GraphicsBuffer[TRIPLE_COUNT];
    GraphicsBuffer[] _argsBuffers = new GraphicsBuffer[TRIPLE_COUNT];
    GraphicsBuffer.IndirectDrawIndexedArgs[][] _args = new GraphicsBuffer.IndirectDrawIndexedArgs[TRIPLE_COUNT][];
    NativeArray<int> _counts;

    MaterialPropertyBlock _mpb;
    RenderParams _rp;
    Mesh _mesh;

    int _writeIndex = -1;
    int _renderIndex = -1;

    NativeArray<T> _noteRenderDatasThisFrame;

    public NoteRenderGroup(Material mat, Mesh mesh, int priority)
    {
        _mesh = mesh;
        _mpb = new();
        _rp = new(mat)
        {
            worldBounds = new Bounds(new Vector3(0, 0, -0.1f * priority), Vector3.one * 10000),
            //rendererPriority = rendererPriority,
            matProps = _mpb
        };

        uint quadIndexCount = mesh.GetIndexCount(0);

        for (int i = 0; i < TRIPLE_COUNT; i++)
        {
            _buffers[i] = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                MAX_INSTANCES,
                UnsafeUtility.SizeOf<T>());

            _argsBuffers[i] = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments, 1,
                GraphicsBuffer.IndirectDrawIndexedArgs.size);
            _args[i] = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
            _args[i][0].indexCountPerInstance = quadIndexCount;
        }

        _counts = new NativeArray<int>(TRIPLE_COUNT, Allocator.Persistent);
    }

    public void AdvanceWrite()
    {
        _writeIndex = (_writeIndex + 1) % TRIPLE_COUNT;
    }

    public NativeArray<T> LockForWrite()
    {
        _noteRenderDatasThisFrame = _buffers[_writeIndex].LockBufferForWrite<T>(0, MAX_INSTANCES);
        return _noteRenderDatasThisFrame;
    }

    public void UnlockWrite(bool sort = true)
    {
        var count = _counts[_writeIndex];
        if (sort) _noteRenderDatasThisFrame.GetSubArray(0, count).Sort();
        _buffers[_writeIndex].UnlockBufferAfterWrite<T>(count);
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
        for (int i = 0; i < TRIPLE_COUNT; i++)
        {
            _buffers[i]?.Dispose();
            _argsBuffers[i]?.Dispose();
        }
        if (_counts.IsCreated) _counts.Dispose();

        GC.SuppressFinalize(this);
    }
}
