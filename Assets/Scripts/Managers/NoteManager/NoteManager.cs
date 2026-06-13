using System.Collections.Generic;
using MajSimai;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

using static MajCtx;

public partial class NoteManager : MonoBehaviour
{


    NativeList<TapData> taps = new(1024, Allocator.Persistent);


    private NoteViewPoolManager _pool => _noteViewPoolManager;
    private JobHandle _currentUpdateJob;

    void Awake()
    {
        _noteManager = this;
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

        if (!_timeProvider.IsStart) return;
        if (taps.Length == 0) return;

        SyncTap();
    }
    void OnDestroy()
    {
        if (taps.IsCreated) taps.Dispose();
    }


    public void ResetState()
    {
        foreach (var tap in taps)
        {
            _pool.Release(tap.ViewIndex);
            _pool.Release(tap.tapLine.ViewIndex);
            _pool.Release(tap.tapEx.ViewIndex);
        }
        taps.Clear();

        _noteSortOrder = 0;
    }
}