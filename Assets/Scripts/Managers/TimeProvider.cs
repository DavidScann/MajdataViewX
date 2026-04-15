using System;
using UnityEngine;

public class TimeProvider : MonoBehaviour
{
    public bool isStart { get; private set; }
    public bool isRecord;
    
    public float AudioTime { get; private set; }
    //notes get this value
    public float NoteTime { get; private set; }
    private float offset;
    //for pause and resume
    private float lastPauseAt; //the beginning of the program is 0
    private float startTime; //the beginning of the program is 0
    public float speed;
    
    public float CurrentSpeed => isRecord ? Time.timeScale : speed;

    private void Awake()
    {
        Majdata<TimeProvider>.Instance = this;
    }
    
    private void Update()
    {
        if (!isStart) return;

        if (isRecord)
        {
            AudioTime = Time.time - startTime;
            NoteTime = AudioTime - offset;
        }
        else
        {
            AudioTime = (Time.realtimeSinceStartup - startTime) * speed;
            NoteTime = AudioTime - offset;
        }
    }

    public float GetFrame()
    {
        return NoteTime * 1000 / 16.6667f;
    }
    
    public void SetStartTime(double _startAt, double _offset, float _speed, bool _isRecord = false, int fps = 60)
    {
        offset = (float)_offset;
        isRecord = _isRecord;
        if (_isRecord)
        {
            startTime = Time.time + 5;
            Time.timeScale = _speed;
            Time.captureFramerate = fps;
        }
        else
        {
            startTime = Time.realtimeSinceStartup - (float)_startAt;
            speed = _speed;
            Time.captureFramerate = 0;
            
            //calculate immediately
            AudioTime = (Time.realtimeSinceStartup - startTime) * speed;
        }

        isStart = true;
    }

    public void Pause()
    {
        isStart = false;
        lastPauseAt = Time.realtimeSinceStartup;
    }

    public void Resume(float? _speed)
    {
        if (_speed != null) speed = _speed.Value;
        if (isStart) return;
        startTime += Time.realtimeSinceStartup - lastPauseAt;
        isStart = true;
    }
}