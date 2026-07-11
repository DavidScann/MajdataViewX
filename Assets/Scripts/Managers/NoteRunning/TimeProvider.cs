#pragma warning disable CS8500 // 这会获取托管类型的地址、获取其大小或声明指向它的指针
#region

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using MajSimai;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

using static MajCtx;
using static MajBurst;

#endregion

public class TimeProvider : MonoBehaviour
{
    public bool IsStart { get; private set; }
    public bool IsRecord { get; private set; }

    //audio get this value
    public float AudioTime { get; private set; }
    //notes get this value
    public float NoteTime { get; private set; }
    public float FakeNoteTime => TimeData.GetPositionAtTime(NoteTime);
    public float CurrentSpeed => IsRecord ? Time.timeScale : speed;

    private NativeArray<TimeDataB> _timeDataContainer = new(1, Allocator.Persistent); // == Native Reference

    private static NativeList<(float time, float sVeloc)> SVList = new(20, Allocator.Persistent);
    private static NativeArray<(float k, float b)> SVFuncArgs;

    private double startRealtime; //the beginning of the program is 0
    private float startAt; //the beginning of the audio is 0
    private float offset;
    private float speed;
    //for pause and resume
    private double accumulated;


    private string mmfAudioTimePath => Path.Combine(MajEnv.MajBase, "majdata_time.dat");
    private MemoryMappedFile mmfAudioTime;
    private MemoryMappedViewAccessor mmvAudioTime;

    public const float SONG_DETAIL_OFFSET = 5f;

    private void Awake()
    {
        _timeProvider = this;

        var mmfAudioTimeFileStream = new FileStream(
            mmfAudioTimePath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.ReadWrite
        );
        if (mmfAudioTimeFileStream.Length < sizeof(float))
            mmfAudioTimeFileStream.SetLength(sizeof(float));
        mmfAudioTime = MemoryMappedFile.CreateFromFile(
            mmfAudioTimeFileStream,
            null,
            sizeof(float),
            MemoryMappedFileAccess.ReadWrite,
            HandleInheritability.None,
            false
        );
        mmvAudioTime = mmfAudioTime.CreateViewAccessor();
    }

    private unsafe void Update()
    {
        if (!IsStart) return;

        if (IsRecord)
        {
            AudioTime = (float)(startAt + accumulated + (Time.timeAsDouble - startRealtime));
            NoteTime = AudioTime - offset;
        }
        else
        {
            if (MajCtx._audioManager != null && MajCtx._audioManager.IsTrackPlaying)
            {
                double trackOffset = AudioManager.TRACK_ANSWER_PLAYBACK_OFFSET_SEC + MajCtx._audioManager.GlobalAudioOffset;
                double targetAudioTime = MajCtx._audioManager.TrackCurrentSec + trackOffset;
                
                double currentExpected = startAt + accumulated + (Time.realtimeSinceStartupAsDouble - startRealtime) * speed;
                double diff = targetAudioTime - currentExpected;
                
                // If the difference is large (e.g. paused/resumed or huge lag), snap it.
                if (System.Math.Abs(diff) > 0.1)
                {
                    accumulated += diff;
                }
                else
                {
                    // Smoothly pull the system clock towards the audio hardware clock to fix "notes getting faster"
                    accumulated += diff * Time.deltaTime * 5.0;
                }
            }

            AudioTime = (float)(startAt + accumulated + (Time.realtimeSinceStartupAsDouble - startRealtime) * speed);
            NoteTime = AudioTime - offset;
        }

        mmvAudioTime.Write(0, AudioTime);

        TimeData.IsStart = IsStart;
        TimeData.NoteTime = NoteTime;
        TimeData.deltaTime = Time.deltaTime;
    }

    public unsafe void LoadSV(ReadOnlySpan<SimaiTimingPoint> commaTimings)
    {
        SVList.Clear();
        if (SVFuncArgs.IsCreated) SVFuncArgs.Dispose();
        foreach (var timing in commaTimings)
        {
            if (SVList.Length == 0 || SVList[^1].sVeloc != timing.SVeloc)
            {
                SVList.Add(((float)timing.Timing, timing.SVeloc));
            }
        }
        if (SVList.Length == 0)
        {
            return;
        }

        SVFuncArgs = new(SVList.Length + 1, Allocator.Persistent);

        float pos = 0f;
        float lastTime = 0f;
        float lastSpeed = 1f;

        SVFuncArgs[0] = (1, 0);

        for (var i = 0; i < SVList.Length; i++)
        {
            var (time, sveloc) = SVList[i];
            pos += lastSpeed * (time - lastTime);

            // 使用当前点的时间和当前新的速度构建往后的运动方程
            SVFuncArgs[i + 1] = (sveloc, pos - sveloc * time);
            lastTime = time;
            lastSpeed = sveloc;
        }

        TimeData.__SVListPtr = SVList.GetUnsafeReadOnlyPtr();
        TimeData.__SVListLength = SVList.Length;
        TimeData.__SVFuncArgsPtr = SVFuncArgs.GetUnsafeReadOnlyPtr();
        TimeData.__SVFuncArgsLength = SVFuncArgs.Length;
    }

    public void SetStartTime(double _startAt, double _offset, float _speed, PlaybackMode mode, int fps = 60)
    {
        IsStart = false;
        IsRecord = false;
        AudioTime = 0f;
        NoteTime = 0f;
        accumulated = 0f;
        Time.timeScale = 1f;

        startAt = (float)_startAt;
        offset = (float)_offset;
        speed = _speed;

        switch (mode)
        {
            case PlaybackMode.Normal:
                {
                    startRealtime = Time.realtimeSinceStartupAsDouble;
                    speed = _speed;
                    Time.captureFramerate = 0;
                }
                break;
            case PlaybackMode.IncludeOp:
                {
                    startRealtime = Time.realtimeSinceStartupAsDouble;
                    startAt -= SONG_DETAIL_OFFSET;
                    speed = _speed;
                    Time.captureFramerate = 0;
                }
                break;
            case PlaybackMode.Record:
                {
                    IsRecord = true;
                    startRealtime = Time.timeAsDouble;
                    startAt -= SONG_DETAIL_OFFSET;
                    Time.timeScale = _speed;
                    Time.captureFramerate = fps;
                }
                break;
        }

        IsStart = true;
        //calculate immediately
        Update();
    }

    public void Pause()
    {
        if (!IsStart) return;

        var now = IsRecord ? Time.timeAsDouble : Time.realtimeSinceStartupAsDouble;
        accumulated += IsRecord
            ? now - startRealtime
            : (now - startRealtime) * speed;

        IsStart = false;
    }

    public void Resume(float? _speed)
    {
        if (_speed != null) speed = _speed.Value;
        if (IsStart) return;

        startRealtime = IsRecord ? Time.timeAsDouble : Time.realtimeSinceStartupAsDouble;

        IsStart = true;
    }

    public void ResetState()
    {
        IsStart = false;
        IsRecord = false;
        AudioTime = 0f;
        NoteTime = 0f;
        startRealtime = 0f;
        startAt = 0f;
        offset = 0f;
        accumulated = 0f;
        speed = 1f;
        Time.timeScale = 1f;
        Time.captureFramerate = 0;
    }

    private void OnDestroy()
    {
        mmvAudioTime?.Dispose();
        mmfAudioTime?.Dispose();

        if (_timeDataContainer.IsCreated) _timeDataContainer.Dispose();
        if (SVList.IsCreated) SVList.Dispose();
        if (SVFuncArgs.IsCreated) SVFuncArgs.Dispose();
    }
}

[BurstCompile]
public struct TimeDataB
{
    public bool IsStart;
    public float NoteTime;
    public readonly float FakeNoteTime => GetPositionAtTime(NoteTime);

    public float deltaTime;

    // public NativeList<(float time, float sVeloc)> SVList;
    // public NativeArray<(float k, float b)> SVFuncArgs;
    public unsafe void* __SVListPtr;
    public int __SVListLength;
    public unsafe void* __SVFuncArgsPtr;
    public int __SVFuncArgsLength;

    readonly unsafe ReadOnlySpan<(float time, float sVeloc)> SVList => new(__SVListPtr, __SVListLength);
    readonly unsafe ReadOnlySpan<(float k, float b)> SVFuncArgs => new(__SVFuncArgsPtr, __SVFuncArgsLength);


    [BurstCompile]
    public readonly float GetPositionAtTime(float t)
    {
        if (SVList.Length == 0)
            return t;
        if (t < SVList[0].time)
            return t;
        if (t >= SVList[^1].time)
            return SVFuncArgs[^1].k * t + SVFuncArgs[^1].b;

        // 二分查找
        int low = 0;
        int high = SVList.Length - 1;
        while (low <= high)
        {
            int mid = (low + high) >> 1;
            if (t < SVList[mid].time)
            {
                if (mid == 0 || t >= SVList[mid - 1].time) return SVFuncArgs[mid].k * t + SVFuncArgs[mid].b;
                high = mid - 1;
            }
            else low = mid + 1;
        }
        return SVFuncArgs[^1].k * t + SVFuncArgs[^1].b;
    }

    public readonly float GetFrame() => NoteTime * 1000 / 16.6667f;
}