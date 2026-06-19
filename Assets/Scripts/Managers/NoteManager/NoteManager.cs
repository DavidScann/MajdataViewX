using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using MajSimai;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using static MajCtx;

public partial class NoteManager : MonoBehaviour
{
    //TODO: streaming load and play
    NativeList<TapData> taps = new(2048, Allocator.Persistent);


    NoteRenderGroup _tapLineGroup;
    NoteRenderGroup _tapGroup;

    GraphicsBuffer _noteUvsBuffer;
    Mesh _octagon;
    Mesh _quad;
    Material _mat;

    private JobHandle _currentUpdateJob;
    bool _isJobScheduledThisFrame;

    void Awake()
    {
        _noteManager = this;
    }
    void Start()
    {
        _octagon = MeshBuilder.CreateOctagonMesh();
        _quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        _mat = new Material(Shader.Find("Custom/NoteIndirect"));

        _tapLineGroup = new NoteRenderGroup(_mat, _octagon, 0);
        _tapGroup = new NoteRenderGroup(_mat, _octagon, 1);

        _noteUvsBuffer = new(
            GraphicsBuffer.Target.Structured,
            _noteSkinManager.Uvs.Length,
            sizeof(float) * 4);
        _noteUvsBuffer.SetData(_noteSkinManager.Uvs, 0, 0, _noteSkinManager.Uvs.Length);

        _mat.SetBuffer("_SpriteRects", _noteUvsBuffer);
        _mat.SetTexture("_MainTex", _noteSkinManager.Atlas);
        _mat.SetFloat("_AtlasSize", 8192);
        _mat.SetFloat("_PixelsPerUnit", 100);
    }
    void Update()
    {
        if (taps.Length == 0) return;

        _tapLineGroup.AdvanceWrite();
        _tapGroup.AdvanceWrite();

        var tapLineRender = _tapLineGroup.LockForWrite();
        var tapsRender = _tapGroup.LockForWrite();

        unsafe
        {
            _tapLineGroup.ResetCount();
            _tapGroup.ResetCount();

            _currentUpdateJob = new TapUpdateJob
            {
                AutoPlayMode = _inputManager.Mode,
                TimeDataPtr = _timeProvider.TimeDataPtr,
                SfxRequestsPtr = _audioManager.SfxRequestsPtr,
                JudgeEffectRequestsPtr = _effectManager.JudgeEffectRequestsPtr,
                FastLateRequestsPtr = _effectManager.FastLateRequestsPtr,
                ReportRequestsPtr = _objectCounter.ReportRequestsPtr,
                ReportCountPtr = _objectCounter.ReportCountPtr,

                taps = taps.AsArray(),

                tapLinesRender = tapLineRender,
                tapsRender = tapsRender,

                TapLinesWriteCountPtr = _tapLineGroup.WriteCountPtr,
                TapWriteCountPtr = _tapGroup.WriteCountPtr,
            }
            .Schedule(taps.Length, 32);
        }
        _isJobScheduledThisFrame = true;
    }

    void LateUpdate()
    {
        _currentUpdateJob.Complete();


        if (!_isJobScheduledThisFrame) return;

        _tapLineGroup.UnlockAndSortWrite();
        _tapGroup.UnlockAndSortWrite();

        _tapLineGroup.Render();
        _tapGroup.Render();

        _tapLineGroup.Swap();
        _tapGroup.Swap();

        _isJobScheduledThisFrame = false;
    }

    void OnDestroy()
    {
        _tapLineGroup?.Dispose();
        _tapGroup?.Dispose();

        _noteUvsBuffer?.Dispose();

        if (taps.IsCreated) taps.Dispose();
    }


    public void ResetState()
    {
        taps.Clear();

        _noteSortOrder = 0;
    }
}

public struct NoteRenderData : IComparable<NoteRenderData>
{
    public float2 pos;
    public float angRad;
    public float scale;
    public uint spriteId;   // 贴图UV表索引
    public float4 color;
    public float brightness;

    public uint exSpriteId;
    public float4 exColor;

    public uint sort;

    public readonly int CompareTo(NoteRenderData other)
    {
        // reverse
        return other.sort.CompareTo(sort);
    }
}