#region

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using MajSimai;
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


    public List<(float time, float sVeloc)> SVList { get; } = new();
    private List<Func<float, float>> PositionFunctions { get; } = new();

    private float startRealtime; //the beginning of the program is 0
    private float startAt; //the beginning of the audio is 0
    private float offset;
    private float speed;
    //for pause and resume
    private float accumulated;

    public float CurrentSpeed => IsRecord ? Time.timeScale : speed;

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

    private void Update()
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
    }

    public float GetFrame()
    {
        return NoteTime * 1000 / 16.6667f;
    }

    public void LoadSV(ReadOnlySpan<SimaiTimingPoint> commaTimings)
    {
        SVList.Clear();
        PositionFunctions.Clear();
        foreach (var timing in commaTimings)
        {
            if (SVList.Count == 0 || SVList[^1].sVeloc != timing.SVeloc)
            {
                SVList.Add(((float)timing.Timing, timing.SVeloc));
            }
        }
        if (SVList.Count > 0) CalcSVPos();
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

    public void CalcSVPos()
    {
        PositionFunctions.Clear();

        float pos = 0f;
        float lastTime = 0f;
        float lastSpeed = 1f;

        foreach (var (time, sVeloc) in SVList)
        {
            pos += lastSpeed * (time - lastTime);
            pos += lastSpeed * (time - lastTime);

            PositionFunctions.Add((t) => pos + lastSpeed * (t - lastTime));

            lastTime = time;
            lastSpeed = sVeloc;
        }
    }
    public float GetPositionAtTime(float AudioT)
    {
        if (SVList.Count == 0) //无SV修改
            return AudioT;
        if (AudioT < SVList[0].time) //在第一个SV修改之前
            return AudioT;
        if (AudioT >= SVList[^1].time) //在最后一个SV修改之后
            return PositionFunctions[SVList.Count](AudioT);
        for (int i = 0; i < SVList.Count; i++) //在两个SV修改之间
        {
            if (AudioT < SVList[i].time)
                return PositionFunctions[i](AudioT);
        }
        return PositionFunctions[SVList.Count](AudioT); //理论上不会到这里
    }

    private void OnDestroy()
    {
        mmvAudioTime?.Dispose();
        mmfAudioTime?.Dispose();
    }
}