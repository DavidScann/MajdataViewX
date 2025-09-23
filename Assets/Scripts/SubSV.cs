using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

public class SubSV : MonoBehaviour
{
    public float AudioTime; //notes get this value
    public List<float> SVList = new();
    public List<float> SVTime = new();
    private List<Func<float, float>> positionFunctions = new List<Func<float, float>>();
    private List<float> segmentStarts = new List<float>();
    public float ScrollDist; //表示当前状况在正常流速且无SC下的时间，没有出现scene control的时候与Audio Time相同
    public bool isStart;
    public bool isRecord;
    public float offset;
    public float speed;

    private float startTime;
    private long ticks;

    public float CurrentSpeed => isRecord ? Time.timeScale : speed;

    private void Start()
    {
        AudioTimeProvider timeProvider = GameObject.Find("AudioTimeProvider").GetComponent<AudioTimeProvider>();
        ticks = timeProvider.ticks;
        offset = timeProvider.offset;
        AudioTime = timeProvider.AudioTime;
        isRecord = timeProvider.isRecord;
        startTime = timeProvider.startTime;
        speed = timeProvider.speed;
        isStart = timeProvider.isStart;
        CalcSVPos();
    }

    // Update is called once per frame
    private void Update()
    {
        if (isStart)
        {
            if (isRecord)
                AudioTime = Time.time - startTime + offset;
            else
                AudioTime = (Time.realtimeSinceStartup - startTime) * speed + offset;
            ScrollDist = GetPositionAtTime(AudioTime);
        }
    }
    public float GetFrame()
    {
        var _audioTime = AudioTime * 1000;

        return _audioTime / 16.6667f;
    }
    
    public void ResetStartTime()
    {
        offset = 0f;
        isStart = false;
    }

    public void CalcSVPos()
    {
        // 初始化变量
        float lastPosition = 0f;
        float lastTime = 0f;
        float lastSpeed = 1f;

        positionFunctions.Clear();
        segmentStarts.Clear();
        if (SVTime.Count == 1 && SVList.Count == 1)
        {
            if (SVTime[0] > 0)
            {
                positionFunctions.Add((t) => t);
                segmentStarts.Add(0);
                lastPosition = SVTime[0];
                lastTime = SVTime[0];
            }
            positionFunctions.Add((t) => lastPosition + SVList[0] * (t - lastTime));
            segmentStarts.Add(lastPosition);
            UnityEngine.Debug.Log($"Single Segment Case: Start = {lastPosition}, Speed = {SVList[0]}");
            return;
        }
        positionFunctions.Add((t) => t);
        segmentStarts.Add(0);
        for (int i = 0; i < SVTime.Count - 1; i++)
        {
            float segmentDuration = SVTime[i] - lastTime; // 上一个区间的持续时间
            lastPosition += lastSpeed * segmentDuration; // 计算上一个区间结束时的累积位置
            float speed = SVList[i]; // 当前区间的速度
            lastSpeed = speed; // 更新速度
            lastTime = SVTime[i]; // 更新上一个时间点
            // 创建分段函数：Position(t) = Position_i + Speed_i * (t - SVTime[i])
            UnityEngine.Debug.Log($"Segment Case {i}: startTime = {lastTime}, Start = {lastPosition}, Speed = {lastSpeed}");
            float lP = lastPosition;
            float lS = lastSpeed;
            float lT = lastTime;
            Func<float, float> segmentFunction = (t) =>
            {
                return lP + lS * (t - lT);
            };
            positionFunctions.Add(segmentFunction);
            segmentStarts.Add(lastPosition);
            
        }
        lastPosition += lastSpeed * (SVTime[SVTime.Count - 1] - lastTime);
        lastTime = SVTime[SVTime.Count - 1];
        lastSpeed = SVList[SVList.Count - 1];
        float llP = lastPosition;
        float llS = lastSpeed;
        float llT = lastTime;
        positionFunctions.Add((t) => llP + llS * (t - llT));
        segmentStarts.Add(lastPosition);
        UnityEngine.Debug.Log($"Segment Case Last: StartTime = {lastTime}, Start = {lastPosition}, Speed = {lastSpeed}");
    }


    public float GetPositionAtTime(float AudioT)
    {
        if (SVTime.Count == 0)
            return AudioT;
        if (AudioT < SVTime[0])
            return AudioT;
        if (AudioT >= SVTime[SVTime.Count - 1])
            return positionFunctions[SVTime.Count](AudioT);
        for (int i = 0; i < SVTime.Count; i++)
        {
            if (AudioT < SVTime[i])
                return positionFunctions[i](AudioT);
        }
        return positionFunctions[SVTime.Count](AudioT);
    }

}