using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using MajSimai;
using ManagedBass;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    private TimeProvider timeProvider;
    
    [CanBeNull] static AudioSample trackSample = null;
    
    //answer SFX
    List<AnswerTimingPoint> answerTimingPoints = new();
    //note SFX
    public static bool[] noteSfxPlaybackRequests = new bool[15];
    List<AudioSample> NoteSfxs = new(15);
    
    //SFX for recording
    private float[] trackSampleData;
    private List<float[]> noteSfxSamplesData = new(15);
    private float[] recordingBuffer; 
    
    const int SAMPLERATE = 44100;
    const int CHANNELS = 2;
    
    const float ANSWER_PLAYBACK_OFFSET_SEC = -(16.66666f * 1) / 1000;
    
    const int TAP_PERFECT = 0;
    const int TAP_GREAT = 1;
    const int TAP_GOOD = 2;
    const int TAP_EX = 3;
    const int BREAK_JUDGE = 4;
    const int BREAK_SFX = 5;
    const int SLIDE = 6;
    const int BREAK_SLIDE = 7;
    const int BREAK_SLIDE_JUDGE = 8;
    const int TOUCH = 9;
    const int TOUCHHOLD = 10;
    const int FIREWORK = 11;
    const int ANSWER = 12;
    const int ANSWER_CLOCK = 13;
    const int TRACK_START = 14;
    
    private bool isTouchHoldRiserPlaying = false;

    private void Awake()
    {
        Majdata<AudioManager>.Instance = this;
        Bass.Init();
        
        //Note SFX
        foreach (var filename in new []
                 {
                     "tap_perfect.wav",
                     "tap_great.wav",
                     "tap_good.wav",
                     "tap_ex.wav",
                     "break_tap.wav",
                     "break.wav",
                     "slide.wav",
                     "slide_break_start.wav",
                     "slide_break_slide.wav",
                     "touch.wav",
                     "touch_Hold_riser.wav",
                     "touch_hanabi.wav",
                     "answer.wav",
                     "answer_clock.wav",
                     "track_start.wav"
                 })
        {
            var path = Path.Combine(new DirectoryInfo(Application.dataPath).Parent!.FullName, 
                "SFX", filename);
            
            //sample
            var sample = new AudioSample(path);
            sample.SampleType = filename switch
            {
                var p when p.StartsWith("answer") => SampleType.Answer,
                var p when p.StartsWith("break") => SampleType.Break,
                var p when p.StartsWith("slide") => SampleType.Slide,
                var p when p.StartsWith("tap") => SampleType.Tap,
                var p when p.StartsWith("touch") => SampleType.Touch,
                var p when p.StartsWith("track") => SampleType.Track,
                _ => sample.SampleType
            };
            NoteSfxs.Add(sample);

            //data
            noteSfxSamplesData.Add(GetSampleDataFromFile(path));
        }
    }



    private void Start()
    {
        timeProvider = Majdata<TimeProvider>.Instance!;
    }

    public void UpdateAnswerSfx()
    {
        for (var i = 0; i < answerTimingPoints.Count; i++)
        {
            var timing = answerTimingPoints[i];
            
            if (timing.IsPlayed) continue;
            
            var thisFrameSec = Majdata<TimeProvider>.Instance!.NoteTime;

            var delta = thisFrameSec - (timing.Timing + ANSWER_PLAYBACK_OFFSET_SEC);
            if (delta > 0)
            {
                if (timing.IsClock) noteSfxPlaybackRequests[ANSWER_CLOCK] = true;
                else noteSfxPlaybackRequests[ANSWER] = true;

                timing.IsPlayed = true;
            }
        }
    }
    
    private void Update()
    {
        if (timeProvider.isRecord) return;
        
        UpdateAnswerSfx();
        
        for (var i = 0; i < noteSfxPlaybackRequests.Length; i++)
        {
            var isRequested = noteSfxPlaybackRequests[i];
            switch (i)
            {
                case TAP_PERFECT:
                    if (isRequested) NoteSfxs[TAP_PERFECT].PlayOneShot();
                    break;
                case TAP_GREAT:
                    if (isRequested) NoteSfxs[TAP_GREAT].PlayOneShot();
                    break;
                case TAP_GOOD:
                    if (isRequested) NoteSfxs[TAP_GOOD].PlayOneShot();
                    break;
                case TAP_EX:
                    if (isRequested) NoteSfxs[TAP_EX].PlayOneShot();
                    break;
                case BREAK_JUDGE:
                    if (isRequested) NoteSfxs[BREAK_JUDGE].PlayOneShot();
                    break;
                case BREAK_SFX:
                    if (isRequested) NoteSfxs[BREAK_SFX].PlayOneShot();
                    break;
                case SLIDE:
                    if (isRequested) NoteSfxs[SLIDE].PlayOneShot();
                    break;
                case BREAK_SLIDE:
                    if (isRequested) NoteSfxs[BREAK_SLIDE].PlayOneShot();
                    break;
                case BREAK_SLIDE_JUDGE:
                    if (isRequested)
                    {
                        NoteSfxs[BREAK_SLIDE_JUDGE].PlayOneShot();
                        NoteSfxs[BREAK_SFX].PlayOneShot();
                    }
                    break;
                case TOUCH:
                    if (isRequested) NoteSfxs[TOUCH].PlayOneShot();
                    break;
                case TOUCHHOLD:
                    if (isRequested)
                    {
                        if (isTouchHoldRiserPlaying)
                            break;
                        isTouchHoldRiserPlaying = true;
                        NoteSfxs[TOUCHHOLD].PlayOneShot();
                    }
                    else
                    {
                        if (!isTouchHoldRiserPlaying)
                            break;
                        isTouchHoldRiserPlaying = false;
                        NoteSfxs[TOUCHHOLD].Stop();
                    }
                    break;
                case FIREWORK:
                    if (isRequested) NoteSfxs[FIREWORK].PlayOneShot();
                    break;
                case ANSWER:
                    if (isRequested) NoteSfxs[ANSWER].PlayOneShot();
                    break;
                case ANSWER_CLOCK:
                    if (isRequested) NoteSfxs[ANSWER_CLOCK].PlayOneShot();
                    break;
                case TRACK_START:
                    if (isRequested) NoteSfxs[TRACK_START].PlayOneShot();
                    break;
            }
        }
        //clear
        for (var i = 0; i < noteSfxPlaybackRequests.Length; i++) noteSfxPlaybackRequests[i] = false;
    }

    private void OnDestroy()
    {
        Bass.Stop();
        Bass.Free();
    }
    
    public void LoadTrack(string path)
    {
        trackSample?.Dispose();
        trackSample = new AudioSample(path)
        {
            SampleType = SampleType.Track
        };
        trackSampleData = GetSampleDataFromFile(path);
    }
    
    public void PlayTrack()
    {
        if (trackSample == null) return;
        trackSample.Speed = timeProvider.CurrentSpeed;
        StartCoroutine(WaitForTrackAudioStart());
    }
    
    public void PauseTrack() => trackSample?.Pause();
    
    public void StopTrack() => trackSample?.Stop();
    

    public void GenerateAnswerSFX(SimaiChart chart, int clockCount = 0)
    {
        //Generate ClockSounds
        var firstBpm = 0f;
        if (!chart.NoteTimings.IsEmpty)
        {
            firstBpm = chart.NoteTimings[0].Bpm;
        }

        var interval = 60 / firstBpm;

        for (var i = 0; i < clockCount; i++)
        {
            var timing = i * interval;
            answerTimingPoints.Add(new AnswerTimingPoint(timing, true));
        }

        //Generate AnswerSounds
        var rawTimings = new List<float>();

        foreach (var timingPoint in chart.NoteTimings)
        {
            var startTiming = (float)timingPoint.Timing;
            rawTimings.Add(startTiming);
            
            var holds = Array.FindAll(timingPoint.Notes,
                o => o.Type is SimaiNoteType.Hold or SimaiNoteType.TouchHold);

            foreach (var hold in holds)
            {
                var endTiming = (float)(timingPoint.Timing + hold.HoldTime);
                rawTimings.Add(endTiming);
            }
        }
        
        rawTimings.Sort();

        answerTimingPoints.Clear();
        float lastAddedTime = -1f;
        float epsilon = 0.001f; // 1ms 阈值

        foreach (var t in rawTimings)
        {
            // 如果是第一个元素，或者当前时间与上一个添加的时间点差距超过阈值
            if (lastAddedTime < 0 || t - lastAddedTime > epsilon)
            {
                answerTimingPoints.Add(new AnswerTimingPoint(t, false));
                lastAddedTime = t;
            }
        }
    }

    public void PlayTapSound(JudgeType judgeType)
    {
        
    }
    
    
    public void PrepareRecordingBuffer()
    {
        var totalLen = trackSample!.Length + 13; // 留给开头5秒和结尾AP音效
        var size = (int)(totalLen * SAMPLERATE * CHANNELS);
        recordingBuffer = new float[size];
        Array.Clear(recordingBuffer, 0, recordingBuffer.Length);
    }
    
    public void MixSfxToBuffer(int index)
    {
        if (index < 0 || index >= noteSfxSamplesData.Count) return;
        
        var sfx = noteSfxSamplesData[index];
        var time = Majdata<TimeProvider>.Instance!.NoteTime + TimeProvider.SONG_DETAIL_OFFSET;
        var startPos = (int)(time * SAMPLERATE) * CHANNELS;

        for (var i = 0; i < sfx.Length; i++)
        {
            if (startPos + i < recordingBuffer.Length)
                recordingBuffer[startPos + i] += sfx[i] * NoteSfxs[index].Volume; //TODO: Volume
        }
    }
    
    public void ExportFinalWav(string outputPath)
    {
        // track start
        var trackStartSampleData = noteSfxSamplesData[TRACK_START];
        for (var i = 0; i < trackStartSampleData.Length; i++)
        {
            if (i < recordingBuffer.Length)
                recordingBuffer[i] = trackStartSampleData[i] * NoteSfxs[TRACK_START].Volume;
        }

        
        // BGM
        var bgmStartSample = (int)(TimeProvider.SONG_DETAIL_OFFSET * SAMPLERATE) * CHANNELS;
        for (var i = 0; i < trackSampleData.Length; i++)
        {
            if (bgmStartSample + i < recordingBuffer.Length)
            {
                var s = recordingBuffer[bgmStartSample + i] + trackSampleData[i];
                recordingBuffer[bgmStartSample + i] = Math.Clamp(s, -1.0f, 1.0f);
            }
        }
        
        WavFileWriter.WriteFile(outputPath, SAMPLERATE, CHANNELS, recordingBuffer);
    }

    private IEnumerator WaitForTrackAudioStart()
    {
        while (Majdata<TimeProvider>.Instance!.AudioTime < 0) yield return null;
        
        trackSample!.CurrentSec = Majdata<TimeProvider>.Instance!.AudioTime;
        trackSample.Play();
    }

    private float[] GetSampleDataFromFile(string path)
    {
        var stream = Bass.CreateStream(path, 0, 0, BassFlags.Decode | BassFlags.Float);
        var lenBytes = Bass.ChannelGetLength(stream);
        var buffer = new float[lenBytes / 4]; 
        Bass.ChannelGetData(stream, buffer, (int)lenBytes);
        Bass.StreamFree(stream);
        return buffer;
    }

    private class AnswerTimingPoint
    {
        public readonly float Timing;
        public readonly bool IsClock;
        public bool IsPlayed;

        public AnswerTimingPoint(float timing, bool isClock)
        {
            Timing = timing;
            IsClock = isClock;
            IsPlayed = false;
        }
    }
}