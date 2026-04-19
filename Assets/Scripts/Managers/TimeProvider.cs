using System;
using UnityEngine;

public class TimeProvider : MonoBehaviour
{
    public bool isStart { get; private set; }
    public bool isRecord;
    
    //audio get this value
    public float AudioTime { get; private set; }
    //notes get this value
    public float NoteTime { get; private set; }
    
    private float startRealtime; //the beginning of the program is 0
    private float startAt; //the beginning of the audio is 0
    private float offset;
    //for pause and resume
    private float accumulated;
    
    public float speed;
    
    public float CurrentSpeed => isRecord ? Time.timeScale : speed;
    
    public const float SONG_DETAIL_OFFSET = 5f;

    private void Awake()
    {
        Majdata<TimeProvider>.Instance = this;
    }
    
    private void Update()
    {
        if (!isStart) return;
        
        if (isRecord)
        {
            AudioTime = startAt + accumulated + (Time.time - startRealtime);
            NoteTime = AudioTime - offset;
        }
        else
        {
            AudioTime = startAt + accumulated + (Time.realtimeSinceStartup - startRealtime) * speed;
            NoteTime = AudioTime - offset;
        }
    }

    public float GetFrame()
    {
        return NoteTime * 1000 / 16.6667f;
    }
    
    public void SetStartTime(double _startAt, double _offset, float _speed, PlaybackMode mode, int fps = 60)
    {
        startAt = (float)_startAt;
        offset = (float)_offset;

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
                isRecord = true;
                startRealtime = Time.time;
                startAt -= SONG_DETAIL_OFFSET;
                Time.timeScale = _speed;
                Time.captureFramerate = fps;
            }
                break;
        }

        isStart = true;
        //calculate immediately
        Update();
    }

    public void Pause()
    {
        if (!isStart) return;

        var now = isRecord ? Time.time : Time.realtimeSinceStartup;
        accumulated += (now - startRealtime) * speed;

        isStart = false;
    }

    public void Resume(float? _speed)
    {
        if (_speed != null) speed = _speed.Value;
        if (isStart) return;

        startRealtime = isRecord ? Time.time : Time.realtimeSinceStartup;

        isStart = true;
    }
}