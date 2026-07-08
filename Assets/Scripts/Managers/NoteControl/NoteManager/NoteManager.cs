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
    NoteRenderGroup<NotesRenderData> _notesGroup;
    NoteRenderGroup<MaskRenderData> _thBorderGroup;
    NoteRenderGroup<SimpleRenderData> _touchGroup;

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
        var lineMesh = MeshGenerator.CreateRingMesh(10, 0.5f, 0.45f);
        var noteMesh = MeshGenerator.CreateCircleMesh(8);

        //REMEMBER TO FORCE INCLUDE
        _matLine = new Material(Shader.Find("Custom/NoteLine"));
        _matSimple = new Material(Shader.Find("Custom/NoteSimple"));
        _matNotes = new Material(Shader.Find("Custom/NoteRich"));
        _matMask = new Material(Shader.Find("Custom/NoteMask"));

        _tapLineGroup = new NoteRenderGroup<LineRenderData>(_matLine, lineMesh, 0);
        _eachLineGroup = new NoteRenderGroup<LineRenderData>(_matLine, lineMesh, 1);
        _slideGroup = new NoteRenderGroup<SimpleRenderData>(_matSimple, _quad, 2);
        _notesGroup = new NoteRenderGroup<NotesRenderData>(_matNotes, noteMesh, 3);
        _thBorderGroup = new NoteRenderGroup<MaskRenderData>(_matMask, _quad, 4);
        _touchGroup = new NoteRenderGroup<SimpleRenderData>(_matSimple, _quad, 5);

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
        _notesGroup.AdvanceWrite();
        _thBorderGroup.AdvanceWrite();
        _touchGroup.AdvanceWrite();

        var tapLinesRender = _tapLineGroup.LockForWrite();
        var eachLinesRender = _eachLineGroup.LockForWrite();
        var slidesRender = _slideGroup.LockForWrite();
        var notesRender = _notesGroup.LockForWrite();
        var maskRender = _thBorderGroup.LockForWrite();
        var touchesRender = _touchGroup.LockForWrite();

        unsafe
        {
            _tapLineGroup.ResetCount();
            _eachLineGroup.ResetCount();
            _slideGroup.ResetCount();
            _notesGroup.ResetCount();
            _thBorderGroup.ResetCount();
            _touchGroup.ResetCount();

            JobHandle h = default;

            if (taps.Length > 0)
                h = new TapUpdateJob
                {
                    taps = taps.AsArray(),

                    tapLinesRender = tapLinesRender,
                    notesRender = notesRender,

                    tapLinesWriteCountPtr = _tapLineGroup.WriteCountPtr,
                    notesWriteCountPtr = _notesGroup.WriteCountPtr,
                    SfxRequests = _audioManager.SfxRequestsPtr,
                    JudgeEffectRequests = _effectManager.JudgeEffectRequestsPtr,
                    ReportResults = _objectCounter.ReportRequestsWriter,
                }.Schedule(taps.Length, 32, h);

            if (eachLines.Length > 0)
                h = new EachLineUpdateJob
                {
                    eachLines = eachLines.AsArray(),
                    eachLinesRender = eachLinesRender,
                    EachLinesWriteCountPtr = _eachLineGroup.WriteCountPtr,
                }.Schedule(eachLines.Length, 32, h);

            if (holds.Length > 0)
                h = new HoldUpdateJob
                {
                    holds = holds.AsArray(),
                    tapLinesRender = tapLinesRender,
                    notesRender = notesRender,
                    TapLinesWriteCountPtr = _tapLineGroup.WriteCountPtr,
                    NotesWriteCountPtr = _notesGroup.WriteCountPtr,
                    SfxRequests = _audioManager.SfxRequestsPtr,
                    JudgeEffectRequests = _effectManager.JudgeEffectRequestsPtr,
                    ReportResults = _objectCounter.ReportRequestsWriter,
                }.Schedule(holds.Length, 32, h);

            if (slides.Length > 0)
                h = new SlideUpdateJob
                {
                    slides = slides.AsArray(),
                    slidesRender = slidesRender,
                    notesRender = notesRender,
                    SlidesWriteCountPtr = _slideGroup.WriteCountPtr,
                    NotesWriteCountPtr = _notesGroup.WriteCountPtr,
                    SfxRequests = _audioManager.SfxRequestsPtr,
                    ReportResults = _objectCounter.ReportRequestsWriter,
                }.Schedule(slides.Length, 32, h);

            if (touchHolds.Length > 0)
                h = new TouchHoldUpdateJob
                {
                    touchHolds = touchHolds.AsArray(),
                    simpleRender = touchesRender,
                    SimpleWriteCountPtr = _touchGroup.WriteCountPtr,
                    maskRender = maskRender,
                    MaskWriteCountPtr = _thBorderGroup.WriteCountPtr,
                    SfxRequests = _audioManager.SfxRequestsPtr,
                    JudgeEffectRequests = _effectManager.JudgeEffectRequestsPtr,
                    ReportResults = _objectCounter.ReportRequestsWriter,
                }.Schedule(touchHolds.Length, 32, h);

            if (touches.Length > 0)
                h = new TouchUpdateJob
                {
                    touches = touches.AsArray(),
                    touchesRender = touchesRender,
                    TouchesWriteCountPtr = _touchGroup.WriteCountPtr,
                    SfxRequests = _audioManager.SfxRequestsPtr,
                    JudgeEffectRequests = _effectManager.JudgeEffectRequestsPtr,
                    ReportResults = _objectCounter.ReportRequestsWriter,
                }.Schedule(touches.Length, 32, h);

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
        _notesGroup.UnlockWrite();
        _thBorderGroup.UnlockWrite();
        _touchGroup.UnlockWrite();

        _tapLineGroup.Render();
        _eachLineGroup.Render();
        _slideGroup.Render();
        _notesGroup.Render();
        _thBorderGroup.Render();
        _touchGroup.Render();

        _tapLineGroup.Swap();
        _eachLineGroup.Swap();
        _slideGroup.Swap();
        _notesGroup.Swap();
        _thBorderGroup.Swap();
        _touchGroup.Swap();

        _objectCounter.ProcessReportRequests();

        _isJobScheduledThisFrame = false;
    }

    void OnDestroy()
    {
        _tapLineGroup?.Dispose();
        _eachLineGroup?.Dispose();
        _slideGroup?.Dispose();
        _notesGroup?.Dispose();
        _thBorderGroup?.Dispose();
        _touchGroup?.Dispose();

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
        unsafe
        {
            if (slideAreaPool != null)
                UnsafeUtility.Free(slideAreaPool, Allocator.Persistent);
            if (slidePosePool != null)
                UnsafeUtility.Free(slidePosePool, Allocator.Persistent);
            slideAreaPool = null;
            slidePosePool = null;
        }
        MajBurst.MultTouchHandler.Clear();
    }
}
