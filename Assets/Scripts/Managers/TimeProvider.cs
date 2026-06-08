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
        
        try { if (File.Exists(mmfAudioTimePath)) File.Delete(mmfAudioTimePath); }
        catch (IOException) { }
        
        mmfAudioTime = MemoryMappedFile.CreateFromFile(mmfAudioTimePath, 
            FileMode.Create, null, sizeof(float));
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
        // 初始化变量
        float lastPosition = 0f;
        float lastTime = 0f;
        float lastSpeed = 1f;

        PositionFunctions.Clear();
        //第一个预留为SV*1
        PositionFunctions.Add((t) => t);
        if (SVList.Count == 1)
        {
            if (SVList[0].time > 0)
            {
                PositionFunctions.Add((t) => t);
                lastPosition = SVList[0].time;
                lastTime = SVList[0].time;
            }
            PositionFunctions.Add((t) => lastPosition + SVList[0].sVeloc * (t - lastTime));
            Debug.Log($"Single Segment Case: Start = {lastPosition}, Speed = {SVList[0].sVeloc}");
            return;
        }
        for (int i = 0; i < SVList.Count - 1; i++)
        {
            float segmentDuration = SVList[i].time - lastTime; // 上一个区间的持续时间
            lastPosition += lastSpeed * segmentDuration; // 计算上一个区间结束时的累积位置
            float speed = SVList[i].sVeloc; // 当前区间的速度
            lastSpeed = speed; // 更新速度
            lastTime = SVList[i].time; // 更新上一个时间点
                                       // 创建分段函数：Position(t) = Position_i + Speed_i * (t - SVTime[i])
            Debug.Log($"Segment Case {i}: startTime = {lastTime}, Start = {lastPosition}, Speed = {lastSpeed}");
            float lP = lastPosition;
            float lS = lastSpeed;
            float lT = lastTime;
            float segmentFunction(float t)
            {
                return lP + lS * (t - lT);
            }
            PositionFunctions.Add(segmentFunction);

        }
        lastPosition += lastSpeed * (SVList[^1].time - lastTime);
        lastTime = SVList[^1].time;
        lastSpeed = SVList[^1].sVeloc;
        float llP = lastPosition;
        float llS = lastSpeed;
        float llT = lastTime;
        PositionFunctions.Add((t) => llP + llS * (t - llT));
        Debug.Log($"Segment Case Last: StartTime = {lastTime}, Start = {lastPosition}, Speed = {lastSpeed}");
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