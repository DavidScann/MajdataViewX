#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;
using MajSimai;
using ManagedBass;
using ManagedBass.Mix;
using UnityEngine;

#endregion

public class AudioManager : MonoBehaviour
{
    private TimeProvider timeProvider;

    [CanBeNull] private AudioSample TrackSample;
    [CanBeNull] private float[] TrackSampleData;
    private float TrackSampleVolume;
    public bool IsTrackLoaded => TrackSample != null && TrackSampleData != null;
    
    //answer SFX
    List<AnswerTimingPoint> answerTimingPoints = new();
    //note SFX
    public static bool[] noteSfxPlaybackRequests = new bool[16];
    List<AudioSample> NoteSfxs = new(16);
    
    //SFX for recording
    private List<float[]> noteSfxSamplesData = new(16);
    private float[] recordingBuffer; 
    private int[] sfxPlayPointers = new int[16]; //-1 is not playing

    public double GlobalAudioOffset { get; private set; }
    
    const int SAMPLERATE = 44100;
    const int CHANNELS = 2;
    
    const float TRACK_ANSWER_PLAYBACK_OFFSET_SEC = (16.66666f * 1) / 1000;
    
    public const int TAP_PERFECT = 0;
    public const int TAP_GREAT = 1;
    public const int TAP_GOOD = 2;
    public const int TAP_EX = 3;
    public const int BREAK_JUDGE = 4;
    public const int BREAK_SFX = 5;
    public const int SLIDE = 6;
    public const int BREAK_SLIDE = 7;
    public const int BREAK_SLIDE_JUDGE = 8;
    public const int TOUCH = 9;
    public const int TOUCHHOLD = 10;
    public const int FIREWORK = 11;
    public const int ANSWER = 12;
    public const int ANSWER_CLOCK = 13;
    public const int TRACK_START = 14;
    public const int ALL_PERFECT = 15;
    
    private bool isTouchHoldRiserPlaying = false;

    private void Awake()
    {
        Majdata<AudioManager>.Instance = this;
        Bass.Configure(Configuration.UpdatePeriod, 20);
        Bass.Configure(Configuration.PlaybackBufferLength, 40);
        Bass.Init(-1, 44100);

        Bass.PluginLoad("bassmix");
        
        //Note SFX
        var sfxPath = Path.Combine(new DirectoryInfo(Application.dataPath).Parent!.FullName, "SFX");
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
                     "track_start.wav",
                     "all_perfect.wav"
                 })
        {
            //sample
            var path = Path.Combine(sfxPath, filename);
            var sample = new AudioSample(path, AudioSampleMode.Sample);
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

    public void Setting(double globalAudioOffset, MajVolumeSetting v)
    {
        GlobalAudioOffset = globalAudioOffset;
        
        foreach (var sample in NoteSfxs)
            switch (sample.SampleType)
            {
                case SampleType.Answer:
                    sample.Volume = v.Answer;
                    break;
                case SampleType.Break:
                    sample.Volume = v.Break;
                    break;
                case SampleType.Slide:
                    sample.Volume = v.Slide;
                    break;
                case SampleType.Tap:
                    sample.Volume = v.Tap;
                    break;
                case SampleType.Touch:
                    sample.Volume = v.Touch;
                    break;
                case SampleType.Track:
                default:
                    sample.Volume = v.Track;
                    break;
            }

        TrackSampleVolume = v.Track;
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

            var delta = thisFrameSec - (timing.Timing + TRACK_ANSWER_PLAYBACK_OFFSET_SEC);
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
        if (timeProvider.IsRecord) return;
        
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
                case ALL_PERFECT:
                    if (isRequested) NoteSfxs[ALL_PERFECT].PlayOneShot();
                    break;
            }
        }
        //clear
        for (var i = 0; i < noteSfxPlaybackRequests.Length; i++)
        {
            if (i != TOUCHHOLD) //manual control
                noteSfxPlaybackRequests[i] = false;
        }
    }

    private void OnDestroy()
    {
        Bass.Stop();
        Bass.Free();
    }
    
    
    //track control
    
    public void LoadTrack(string path)
    {
        TrackSample?.Dispose();
        TrackSample = new AudioSample(path, AudioSampleMode.Stream)
        {
            SampleType = SampleType.Track,
        };
        TrackSampleData = GetSampleDataFromFile(path);
    }
    
    public void PlayTrack()
    {
        if (TrackSample == null) return;
        TrackSample.Speed = timeProvider.CurrentSpeed;
        TrackSample.Volume = TrackSampleVolume;
        StartCoroutine(WaitForTrackAudioStart());
        
        IEnumerator WaitForTrackAudioStart()
        {
            var offset = TRACK_ANSWER_PLAYBACK_OFFSET_SEC + GlobalAudioOffset;
            while (Majdata<TimeProvider>.Instance!.AudioTime < offset) yield return null;
        
            TrackSample!.CurrentSec = Majdata<TimeProvider>.Instance!.AudioTime - offset;
            TrackSample.Play();
        }
    }
    
    public void PauseTrack() => TrackSample?.Pause();
    
    public void StopTrack() => TrackSample?.Stop();

    public void ResetState()
    {
        StopTrack();
        StopTouchHoldSound();

        answerTimingPoints.Clear();
        for (var i = 0; i < noteSfxPlaybackRequests.Length; i++)
            noteSfxPlaybackRequests[i] = false;
    }
    
    
    //Sfx control

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
        var lastAddedTime = -1f;
        var epsilon = 0.001f; // 1ms 阈值

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

    public void PlayTapSound(in JudgeType judgeType, bool isEx, bool isBreak)
    {
        if (isBreak)
        {
            if (isEx)
            {
                noteSfxPlaybackRequests[TAP_EX] = true;
            }
        
            switch (judgeType)
            {
                case JudgeType.LateGood:
                case JudgeType.FastGood:
                case JudgeType.LateGreat:
                case JudgeType.LateGreat1:
                case JudgeType.LateGreat2:
                case JudgeType.FastGreat2:
                case JudgeType.FastGreat1:
                case JudgeType.FastGreat:
                case JudgeType.LatePerfect2:
                case JudgeType.FastPerfect2:
                case JudgeType.LatePerfect1:
                case JudgeType.FastPerfect1:
                    noteSfxPlaybackRequests[BREAK_JUDGE] = true;
                    break;
                case JudgeType.Perfect:
                    noteSfxPlaybackRequests[BREAK_JUDGE] = true;
                    noteSfxPlaybackRequests[BREAK_SFX] = true;
                    break;
                case JudgeType.Miss:
                default:
                    break;
            }
            return;
        }
        
        if (isEx)
        {
            noteSfxPlaybackRequests[TAP_EX] = true;
            return;
        }

        switch (judgeType)
        {
            case JudgeType.LateGood:
            case JudgeType.FastGood:
                noteSfxPlaybackRequests[TAP_GOOD] = true;
                break;
            case JudgeType.LateGreat:
            case JudgeType.LateGreat1:
            case JudgeType.LateGreat2:
            case JudgeType.FastGreat2:
            case JudgeType.FastGreat1:
            case JudgeType.FastGreat:
                noteSfxPlaybackRequests[TAP_GREAT] = true;
                break;
            case JudgeType.LatePerfect2:
            case JudgeType.FastPerfect2:
            case JudgeType.LatePerfect1:
            case JudgeType.FastPerfect1:
            case JudgeType.Perfect:
                noteSfxPlaybackRequests[TAP_PERFECT] = true;
                break;
            case JudgeType.Miss:
            default:
                break;
        }
    }
    
    public void PlayTouchSound()
    {
        noteSfxPlaybackRequests[TOUCH] = true;
    }
    public void PlayHanabiSound()
    {
        noteSfxPlaybackRequests[FIREWORK] = true;
    }
    public void PlayTouchHoldSound()
    {
        noteSfxPlaybackRequests[TOUCHHOLD] = true;
    }

    public void PauseTouchHoldSound()
    {
        if (isTouchHoldRiserPlaying)
            NoteSfxs[TOUCHHOLD].Pause(); //seen as still playing
    }
    public void ResumeTouchHoldSound()
    {
        if (isTouchHoldRiserPlaying)
            NoteSfxs[TOUCHHOLD].Play();
    }
    public void StopTouchHoldSound()
    {
        noteSfxPlaybackRequests[TOUCHHOLD] = false;
    }
    public void PlaySlideSound(bool isBreak)
    {
        if (isBreak)
        {
            noteSfxPlaybackRequests[BREAK_SLIDE] = true;
        }
        else
        {
            noteSfxPlaybackRequests[SLIDE] = true;
        }
    }
    public void PlayBreakSlideEndSound()
    {
        noteSfxPlaybackRequests[BREAK_SLIDE_JUDGE] = true;
        noteSfxPlaybackRequests[BREAK_SFX] = true;
    }
    
    
    //recording control
    
    public void PrepareRecordingBuffer()
    {
        var totalLen = TrackSample!.Length + 13; // 留给开头5秒和结尾AP音效
        var size = (int)(totalLen * SAMPLERATE * CHANNELS);
        recordingBuffer = new float[size];
        Array.Clear(recordingBuffer, 0, recordingBuffer.Length);
        for (var i = 0; i < 15; i++) sfxPlayPointers[i] = -1; // 初始化指针
    }
    
    public void TriggerSfxRecording(int index)
    {
        if (index < 0 || index >= noteSfxSamplesData.Count) return;
        sfxPlayPointers[index] = 0;
    }
    public void StopSfxRecording(int index)
    {
        if (index < 0 || index >= noteSfxSamplesData.Count) return;
        sfxPlayPointers[index] = -1;
    }
    
    public void UpdateSfxRecording(float deltaTime, float currentNoteTime)
    {
        // 计算当前帧在 buffer 中的起始采样位置
        var bufferStartPos = (int)((currentNoteTime + TimeProvider.SONG_DETAIL_OFFSET) * SAMPLERATE) * CHANNELS;
        // 这一帧应该写入的采样长度
        var samplesToCopy = (int)(deltaTime * SAMPLERATE) * CHANNELS;

        for (var i = 0; i < 15; i++)
        {
            if (sfxPlayPointers[i] == -1) continue;

            var sfxData = noteSfxSamplesData[i];
            var vol = NoteSfxs[i].Volume <= 0 ? 1.0f : NoteSfxs[i].Volume;

            for (var j = 0; j < samplesToCopy; j++)
            {
                var sfxIdx = sfxPlayPointers[i] + j;
                if (sfxIdx < sfxData.Length)
                {
                    if (bufferStartPos + j < recordingBuffer.Length)
                    {
                        // 同种类指针重置，不会自叠加
                        recordingBuffer[bufferStartPos + j] += sfxData[sfxIdx] * vol;
                    }
                }
                else
                {
                    sfxPlayPointers[i] = -1;
                    break;
                }
            }
        
            if (sfxPlayPointers[i] != -1)
                sfxPlayPointers[i] += samplesToCopy;
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
        for (var i = 0; i < TrackSampleData.Length; i++)
        {
            if (bgmStartSample + i < recordingBuffer.Length)
            {
                var s = recordingBuffer[bgmStartSample + i] + TrackSampleData[i];
                recordingBuffer[bgmStartSample + i] = Math.Clamp(s, -1.0f, 1.0f);
            }
        }
        
        WavFileWriter.WriteFile(outputPath, SAMPLERATE, CHANNELS, recordingBuffer);
    }



    private float[] GetSampleDataFromFile(string path)
    { 
        var sourceStream = Bass.CreateStream(path, 0, 0, BassFlags.Decode | BassFlags.Float);
        if (sourceStream == 0) return Array.Empty<float>();
        
        var mixer = BassMix.CreateMixerStream(SAMPLERATE, CHANNELS, BassFlags.Decode | BassFlags.Float);
        
        BassMix.MixerAddChannel(mixer, sourceStream, BassFlags.Default);
        
        var lenBytes = Bass.ChannelGetLength(sourceStream);
        var duration = Bass.ChannelBytes2Seconds(sourceStream, lenBytes);
        var targetLenBytes = Bass.ChannelSeconds2Bytes(mixer, duration);

        var buffer = new float[targetLenBytes / 4];
        Bass.ChannelGetData(mixer, buffer, (int)targetLenBytes);
        
        Bass.StreamFree(mixer);
        Bass.StreamFree(sourceStream);

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