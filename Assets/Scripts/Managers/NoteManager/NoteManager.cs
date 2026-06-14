using System.Collections.Generic;
using System.Drawing;
using MajSimai;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using static MajCtx;

public partial class NoteManager : MonoBehaviour
{
    NativeList<TapData> taps = new(1024, Allocator.Persistent);


    GraphicsBuffer _noteRenderBuffer;
    NativeList<NoteRenderData> _noteRenderData = new(1024, Allocator.Persistent);
    GraphicsBuffer _noteArgsBuffer;
    private GraphicsBuffer.IndirectDrawIndexedArgs[] _noteArgs = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
    GraphicsBuffer _noteUvsBuffer;

    Mesh _quad;
    Material _mat;

    private JobHandle _currentUpdateJob;

    void Awake()
    {
        _noteManager = this;
    }
    void Start()
    {
        _quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        _mat = new Material(Shader.Find("Custom/NoteIndirect"));

        _noteRenderBuffer = new(
            GraphicsBuffer.Target.Structured,
            65536,
            UnsafeUtility.SizeOf<NoteRenderData>());

        _noteArgsBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.IndirectArguments,
            1,
            GraphicsBuffer.IndirectDrawIndexedArgs.size
        );

        _noteUvsBuffer = new(
            GraphicsBuffer.Target.Structured,
            _noteSkinManager.Uvs.Length,
            sizeof(float) * 4);
        _noteUvsBuffer.SetData(_noteSkinManager.Uvs, 0, 0, _noteSkinManager.Uvs.Length);

        _mat.SetBuffer("_NoteBuffer", _noteRenderBuffer);
        _mat.SetBuffer("_SpriteRects", _noteUvsBuffer);
        _mat.SetTexture("_MainTex", _noteSkinManager.Atlas);
        _mat.SetFloat("_AtlasSize", 8192);
        _mat.SetFloat("_PixelsPerUnit", 100);
    }
    void Update()
    {
        if (!_timeProvider.IsStart) return;
        if (taps.Length == 0) return;

        unsafe
        {
            _currentUpdateJob = new TapUpdateJob
            {
                AutoPlayMode = _inputManager.Mode,
                TimeDataPtr = _timeProvider.TimeDataPtr,
                SfxRequestsPtr = _audioManager.SfxRequestsPtr,
                JudgeEffectRequestsPtr = _effectManager.JudgeEffectRequestsPtr,
                FastLateRequestsPtr = _effectManager.FastLateRequestsPtr,
                ReportRequestsPtr = _objectCounter.ReportRequestsPtr,
                ReportCountPtr = _objectCounter.ReportCountPtr,

                taps = taps.AsArray()
            }
            .Schedule(taps.Length, 16);
        }
    }
    void LateUpdate()
    {
        // 不管有没有先完成掉避免占着
        _currentUpdateJob.Complete();

        //if (!_timeProvider.IsStart) return;
        if (taps.Length == 0) return;

        SyncTap();

        _noteRenderBuffer.SetData(_noteRenderData.AsArray(), 0, 0, _noteRenderData.Length);

        _noteArgs[0].indexCountPerInstance = _quad.GetIndexCount(0);
        _noteArgs[0].instanceCount = (uint)_noteRenderData.Length;
        _noteArgsBuffer.SetData(_noteArgs);

        var rp = new RenderParams(_mat)
        {
            worldBounds = new Bounds(Vector3.zero, Vector3.one * 10000)
        };

        Graphics.RenderMeshIndirect(rp, _quad, _noteArgsBuffer);
    }
    void OnDestroy()
    {
        if (taps.IsCreated) taps.Dispose();
    }


    public void ResetState()
    {
        taps.Clear();

        _noteSortOrder = 0;
    }
}

public struct NoteRenderData
{
    public float2 pos;
    public float angRad;
    public float scale;
    public uint spriteId;   // 贴图UV表索引
    public float4 color;
    public float brightness;
}