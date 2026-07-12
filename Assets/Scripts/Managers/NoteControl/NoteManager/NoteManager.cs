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

    NativeList<int> touchGroupTotalCounts = new(256, Allocator.Persistent);
    NativeList<int> touchGroupJudgedCounts = new(256, Allocator.Persistent);
    NativeList<CoverResult> touchGroupCoverResults = new(256, Allocator.Persistent);

    NativeList<int> touchHoldGroupTotalCounts = new(256, Allocator.Persistent);
    NativeList<int> touchHoldGroupPressedCounts = new(256, Allocator.Persistent);
    NativeList<CoverResult> touchHoldGroupCoverResults = new(256, Allocator.Persistent);

    RenderGroup<LineRenderData> _tapLineGroup;
    RenderGroup<LineRenderData> _eachLineGroup;
    RenderGroup<SimpleRenderData> _slideGroup;
    RenderGroup<NotesRenderData> _notesGroup;
    RenderGroup<MaskRenderData> _thBorderGroup;
    RenderGroup<SimpleRenderData> _touchGroup;

    GraphicsBuffer _noteUvsBuffer;
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
        var lineMesh = MeshGenerator.CreateRingMesh(32, 0.5f, 0.3f);
        var _quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

        //REMEMBER TO FORCE INCLUDE
        _matLine = new Material(Shader.Find("Custom/NoteLine"));
        _matSimple = new Material(Shader.Find("Custom/NoteSimple"));
        _matNotes = new Material(Shader.Find("Custom/NoteRich"));
        _matMask = new Material(Shader.Find("Custom/NoteMask"));

        _tapLineGroup = new RenderGroup<LineRenderData>(_matLine, lineMesh, 0);
        _eachLineGroup = new RenderGroup<LineRenderData>(_matLine, lineMesh, 1);
        _slideGroup = new RenderGroup<SimpleRenderData>(_matSimple, _quad, 2, 262144); //slide真的会爆，神了
        _notesGroup = new RenderGroup<NotesRenderData>(_matNotes, _quad, 3);
        _thBorderGroup = new RenderGroup<MaskRenderData>(_matMask, _quad, 4);
        _touchGroup = new RenderGroup<SimpleRenderData>(_matSimple, _quad, 5);

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

            if (touchHoldGroupTotalCounts.Length > 0)
            {
                for (int i = 0; i < touchHoldGroupTotalCounts.Length; i++)
                    touchHoldGroupPressedCounts[i] = 0;
                for (int i = 0; i < touchHolds.Length; i++)
                {
                    var t = touchHolds[i];
                    if (t.groupId != -1 && !t.isEnd)
                    {
                        if (MajBurst.InputData.GetSensorState(t.sensor).Status)
                            touchHoldGroupPressedCounts[t.groupId]++;
                    }
                }
            }

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
                    touchHoldGroupTotalCounts = touchHoldGroupTotalCounts.AsArray(),
                    touchHoldGroupPressedCounts = touchHoldGroupPressedCounts.AsArray(),
                    touchHoldGroupCoverResults = touchHoldGroupCoverResults.AsArray(),
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
                    touchGroupTotalCounts = touchGroupTotalCounts.AsArray(),
                    touchGroupJudgedCounts = touchGroupJudgedCounts.AsArray(),
                    touchGroupCoverResults = touchGroupCoverResults.AsArray(),
                }.Schedule(touches.Length, 32, h);

            _prevChain = h;
        }
        _isJobScheduledThisFrame = true;
    }

    void LateUpdate()
    {
        _prevChain.Complete();

        if (_isJobScheduledThisFrame)
        {
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
            MajBurst.InputData.ApplyNextIndices();

            // 思来想去在th内做加减确实不比在这里遍历一次快
            // 去他妈的可读性 反正我ArrayBoundCheck off
            {
                int activeTouchHolds = 0;
                for (int i = 0; i < touchHolds.Length; i++)
                {
                    if (touchHolds[i].isHolding) activeTouchHolds++;
                }
                _audioManager.ActiveTouchHoldCount = activeTouchHolds;
            }

            _isJobScheduledThisFrame = false;
        }

        _inputManager.RenderHit();
    }

    void OnDestroy()
    {
        _prevChain.Complete();
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

        if (touchGroupTotalCounts.IsCreated) touchGroupTotalCounts.Dispose();
        if (touchGroupJudgedCounts.IsCreated) touchGroupJudgedCounts.Dispose();
        if (touchGroupCoverResults.IsCreated) touchGroupCoverResults.Dispose();
        if (touchHoldGroupTotalCounts.IsCreated) touchHoldGroupTotalCounts.Dispose();
        if (touchHoldGroupPressedCounts.IsCreated) touchHoldGroupPressedCounts.Dispose();
        if (touchHoldGroupCoverResults.IsCreated) touchHoldGroupCoverResults.Dispose();
    }

    public void ResetState()
    {
        _prevChain.Complete();
        taps.Clear();
        eachLines.Clear();
        holds.Clear();
        slides.Clear();
        touches.Clear();
        touchHolds.Clear();

        touchGroupTotalCounts.Clear();
        touchGroupJudgedCounts.Clear();
        touchGroupCoverResults.Clear();
        touchHoldGroupTotalCounts.Clear();
        touchHoldGroupPressedCounts.Clear();
        touchHoldGroupCoverResults.Clear();
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
