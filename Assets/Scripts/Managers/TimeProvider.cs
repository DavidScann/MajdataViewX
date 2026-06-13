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

#endregion

public class TimeProvider : MonoBehaviour
{
    public bool IsStart { get; private set; }
    public bool IsRecord { get; private set; }

    //audio get this value
    public float AudioTime { get; private set; }
    //notes get this value
    public float NoteTime { get; private set; }
    public float FakeNoteTime => GetPositionAtTime(NoteTime);
    public float CurrentSpeed => IsRecord ? Time.timeScale : speed;

    private NativeArray<BurstTimeData> _timeDataContainer = new(1, Allocator.Persistent); // == Native Reference
    public unsafe BurstTimeData* TimeDataPtr => (BurstTimeData*)_timeDataContainer.GetUnsafePtr();

    private static NativeList<(float time, float sVeloc)> SVList = new(20, Allocator.Persistent);
    private static NativeArray<(float k, float b)> SVFuncArgs;

    private float startRealtime; //the beginning of the program is 0
    private float startAt; //the beginning of the audio is 0
    private float offset;
    private float speed;
    //for pause and resume
    private float accumulated;


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
            AudioTime = startAt + accumulated + (Time.time - startRealtime);
            NoteTime = AudioTime - offset;
        }
        else
        {
            AudioTime = startAt + accumulated + (Time.realtimeSinceStartup - startRealtime) * speed;
            NoteTime = AudioTime - offset;
        }

        mmvAudioTime.Write(0, AudioTime);

        BurstTimeData* ptr = TimeDataPtr;
        ptr->IsStart = IsStart;
        ptr->NoteTime = NoteTime;
        ptr->CurrentSpeed = CurrentSpeed;
        ptr->deltaTime = Time.deltaTime;
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

            //PositionFunctions.Add((t) => pos + lastSpeed * (t - lastTime));
            SVFuncArgs[i + 1] = (lastSpeed, pos - lastSpeed * lastTime);

            lastTime = time;
            lastSpeed = sveloc;
        }

        BurstTimeData* ptr = TimeDataPtr;
        ptr->SVListPtr = SVList.GetUnsafeReadOnlyPtr();
        ptr->SVListLength = SVList.Length;
        ptr->SVFuncArgsPtr = SVFuncArgs.GetUnsafeReadOnlyPtr();
        ptr->SVFuncArgsLength = SVFuncArgs.Length;
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
                    startRealtime = Time.realtimeSinceStartup;
                    speed = _speed;
                    Time.captureFramerate = 0;
                }
                break;
            case PlaybackMode.IncludeOp:
                {
                    startRealtime = Time.realtimeSinceStartup;
                    startAt -= SONG_DETAIL_OFFSET;
                    speed = _speed;
                    Time.captureFramerate = 0;
                }
                break;
            case PlaybackMode.Record:
                {
                    IsRecord = true;
                    startRealtime = Time.time;
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

        var now = IsRecord ? Time.time : Time.realtimeSinceStartup;
        accumulated += IsRecord
            ? now - startRealtime
            : (now - startRealtime) * speed;

        IsStart = false;
    }

    public void Resume(float? _speed)
    {
        if (_speed != null) speed = _speed.Value;
        if (IsStart) return;

        startRealtime = IsRecord ? Time.time : Time.realtimeSinceStartup;

        IsStart = true;
    }

    // for before migrating only
    public unsafe float GetFrame() => TimeDataPtr->GetFrame();
    public unsafe float GetPositionAtTime(float t) => TimeDataPtr->GetPositionAtTime(t);


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

public struct BurstTimeData
{
    public bool IsStart;
    public float NoteTime;
    public readonly float FakeNoteTime => GetPositionAtTime(NoteTime);
    public float CurrentSpeed;

    public float deltaTime;

    // public NativeList<(float time, float sVeloc)> SVList;
    // public NativeArray<(float k, float b)> SVFuncArgs;
    public unsafe void* SVListPtr;
    public int SVListLength;
    public unsafe void* SVFuncArgsPtr;
    public int SVFuncArgsLength;
    public readonly unsafe ReadOnlySpan<(float time, float sVeloc)> SVList => new(SVListPtr, SVListLength);
    public readonly unsafe ReadOnlySpan<(float k, float b)> SVFuncArgs => new(SVFuncArgsPtr, SVFuncArgsLength);


    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
    public unsafe readonly float GetPositionAtTime(float t)
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
        return SVFuncArgs[^1].k * t + SVFuncArgs[^1].k;
    }

    public readonly float GetFrame() => NoteTime * 1000 / 16.6667f;
}