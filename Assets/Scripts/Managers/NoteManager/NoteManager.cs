using System;
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
    NativeList<EachLineData> eachLines = new(512, Allocator.Persistent);
    NativeList<HoldData> holds = new(1024, Allocator.Persistent);
    NativeList<SlideData> slides = new(1024, Allocator.Persistent);
    NativeList<TouchData> touches = new(1024, Allocator.Persistent);
    NativeList<TouchHoldData> touchHolds = new(1024, Allocator.Persistent);

    NoteRenderGroup<LineRenderData> _tapLineGroup;
    NoteRenderGroup<LineRenderData> _eachLineGroup;
    NoteRenderGroup<SimpleRenderData> _slideGroup;
    NoteRenderGroup<SimpleRenderData> _holdEndGroup;
    NoteRenderGroup<NotesRenderData> _notesGroup;
    NoteRenderGroup<SimpleRenderData> _touchGroup;
    NoteRenderGroup<MaskRenderData> _touchHoldGroup;

    GraphicsBuffer _noteUvsBuffer;
    Mesh _quad;
    Material _matLine;
    Material _matSimple;
    Material _matNotes;
    Material _matMask;

    JobHandle _prevChain;
    bool _isJobScheduledThisFrame;

    void Awake()
    {
        _noteManager = this;
    }
    void Start()
    {
        _quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

        _matLine = new Material(Shader.Find("Custom/NoteLine"));
        _matSimple = new Material(Shader.Find("Custom/NoteSimple"));
        _matNotes = new Material(Shader.Find("Custom/NoteRich"));
        _matMask = new Material(Shader.Find("Custom/NoteMask"));

        _tapLineGroup = new NoteRenderGroup<LineRenderData>(_matLine, _quad, 0);
        _eachLineGroup = new NoteRenderGroup<LineRenderData>(_matLine, _quad, 1);
        _slideGroup = new NoteRenderGroup<SimpleRenderData>(_matSimple, _quad, 2);
        _holdEndGroup = new NoteRenderGroup<SimpleRenderData>(_matSimple, _quad, 3);
        _notesGroup = new NoteRenderGroup<NotesRenderData>(_matNotes, _quad, 4);
        _touchGroup = new NoteRenderGroup<SimpleRenderData>(_matSimple, _quad, 5);
        _touchHoldGroup = new NoteRenderGroup<MaskRenderData>(_matMask, _quad, 6);

        _noteUvsBuffer = new(
            GraphicsBuffer.Target.Structured,
            _noteSkinManager.Uvs.Length,
            sizeof(float) * 4);
        _noteUvsBuffer.SetData(_noteSkinManager.Uvs, 0, 0, _noteSkinManager.Uvs.Length);

        void SetupMaterial(Material mat)
        {
            mat.SetBuffer("_SpriteRects", _noteUvsBuffer);
            mat.SetTexture("_MainTex", _noteSkinManager.Atlas);
            mat.SetFloat("_AtlasSize", 8192);
            mat.SetFloat("_PixelsPerUnit", 100);
        }
        SetupMaterial(_matLine);
        SetupMaterial(_matSimple);
        SetupMaterial(_matNotes);
        SetupMaterial(_matMask);
    }

    void Update()
    {
        _prevChain.Complete();

        if (taps.Length + eachLines.Length + holds.Length + slides.Length + touches.Length + touchHolds.Length == 0) return;

        _tapLineGroup.AdvanceWrite();
        _eachLineGroup.AdvanceWrite();
        _slideGroup.AdvanceWrite();
        _holdEndGroup.AdvanceWrite();
        _notesGroup.AdvanceWrite();
        _touchGroup.AdvanceWrite();
        _touchHoldGroup.AdvanceWrite();

        var tapLinesRender = _tapLineGroup.LockForWrite();
        var eachLinesRender = _eachLineGroup.LockForWrite();
        var slidesRender = _slideGroup.LockForWrite();
        var holdEndRender = _holdEndGroup.LockForWrite();
        var notesRender = _notesGroup.LockForWrite();
        var touchesRender = _touchGroup.LockForWrite();
        var maskRender = _touchHoldGroup.LockForWrite();

        unsafe
        {
            _tapLineGroup.ResetCount();
            _eachLineGroup.ResetCount();
            _slideGroup.ResetCount();
            _holdEndGroup.ResetCount();
            _notesGroup.ResetCount();
            _touchGroup.ResetCount();
            _touchHoldGroup.ResetCount();

            JobHandle h = default;

            if (taps.Length > 0)
                h = new TapUpdateJob
                {
                    TimeDataPtr = _timeProvider.TimeDataPtr,
                    taps = taps.AsArray(),
                    tapLinesRender = tapLinesRender,
                    notesRender = notesRender,
                    TapLinesWriteCountPtr = _tapLineGroup.WriteCountPtr,
                    NotesWriteCountPtr = _notesGroup.WriteCountPtr,
                }.Schedule(taps.Length, 32, h);

            if (eachLines.Length > 0)
                h = new EachLineUpdateJob
                {
                    TimeDataPtr = _timeProvider.TimeDataPtr,
                    eachLines = eachLines.AsArray(),
                    eachLinesRender = eachLinesRender,
                    EachLinesWriteCountPtr = _eachLineGroup.WriteCountPtr,
                }.Schedule(eachLines.Length, 32, h);

            if (holds.Length > 0)
                h = new HoldUpdateJob
                {
                    TimeDataPtr = _timeProvider.TimeDataPtr,
                    holds = holds.AsArray(),
                    tapLinesRender = tapLinesRender,
                    notesRender = notesRender,
                    simpleRender = holdEndRender,
                    TapLinesWriteCountPtr = _tapLineGroup.WriteCountPtr,
                    NotesWriteCountPtr = _notesGroup.WriteCountPtr,
                    SimpleWriteCountPtr = _holdEndGroup.WriteCountPtr,
                }.Schedule(holds.Length, 32, h);

            if (slides.Length > 0)
                h = new SlideUpdateJob
                {
                    TimeDataPtr = _timeProvider.TimeDataPtr,
                    slides = slides.AsArray(),
                    slidesRender = slidesRender,
                    notesRender = notesRender,
                    SlidesWriteCountPtr = _slideGroup.WriteCountPtr,
                    NotesWriteCountPtr = _notesGroup.WriteCountPtr,
                }.Schedule(slides.Length, 32, h);

            if (touches.Length > 0)
                h = new TouchUpdateJob
                {
                    TimeDataPtr = _timeProvider.TimeDataPtr,
                    touches = touches.AsArray(),
                    touchesRender = touchesRender,
                    TouchesWriteCountPtr = _touchGroup.WriteCountPtr,
                }.Schedule(touches.Length, 32, h);

            if (touchHolds.Length > 0)
                h = new TouchHoldUpdateJob
                {
                    TimeDataPtr = _timeProvider.TimeDataPtr,
                    touchHolds = touchHolds.AsArray(),
                    simpleRender = touchesRender,
                    SimpleWriteCountPtr = _touchGroup.WriteCountPtr,
                    maskRender = maskRender,
                    MaskWriteCountPtr = _touchHoldGroup.WriteCountPtr,
                }.Schedule(touchHolds.Length, 32, h);

            _prevChain = h;
        }
        _isJobScheduledThisFrame = true;
    }

    void LateUpdate()
    {
        _prevChain.Complete();

        if (!_isJobScheduledThisFrame) return;

        _tapLineGroup.UnlockWrite();
        _eachLineGroup.UnlockWrite();
        _slideGroup.UnlockWrite();
        _holdEndGroup.UnlockWrite();
        _notesGroup.UnlockWrite();
        _touchGroup.UnlockWrite();
        _touchHoldGroup.UnlockWrite();

        _tapLineGroup.Render();
        _eachLineGroup.Render();
        _slideGroup.Render();
        _holdEndGroup.Render();
        _notesGroup.Render();
        _touchGroup.Render();
        _touchHoldGroup.Render();

        _tapLineGroup.Swap();
        _eachLineGroup.Swap();
        _slideGroup.Swap();
        _holdEndGroup.Swap();
        _notesGroup.Swap();
        _touchGroup.Swap();
        _touchHoldGroup.Swap();

        _isJobScheduledThisFrame = false;
    }

    void OnDestroy()
    {
        _tapLineGroup?.Dispose();
        _eachLineGroup?.Dispose();
        _slideGroup?.Dispose();
        _holdEndGroup?.Dispose();
        _notesGroup?.Dispose();
        _touchGroup?.Dispose();
        _touchHoldGroup?.Dispose();

        _noteUvsBuffer?.Dispose();

        if (taps.IsCreated) taps.Dispose();
        if (eachLines.IsCreated) eachLines.Dispose();
        if (holds.IsCreated) holds.Dispose();
        if (slides.IsCreated) slides.Dispose();
        if (touches.IsCreated) touches.Dispose();
        if (touchHolds.IsCreated) touchHolds.Dispose();
    }

    public void ResetState()
    {
        taps.Clear();
        eachLines.Clear();
        holds.Clear();
        slides.Clear();
        touches.Clear();
        touchHolds.Clear();
    }
}

public struct NoteRenderData : IComparable<NoteRenderData>
{
    public float2 pos;
    public float angRad;
    public float2 scale;
    public uint spriteId;
    public float4 color;
    public float brightness;

    public uint exSprite;
    public float4 exColor;

    public uint sort;

    public readonly int CompareTo(NoteRenderData other)
    {
        // reverse
        return other.sort.CompareTo(sort);
    }
}
