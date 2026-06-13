#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using MajSimai;
using ManagedBass;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using static MajCtx;

#endregion

public class AudioManager
{
    public const int SFX_COUNT = 16;

    [CanBeNull] private AudioSample TrackSample;
    [CanBeNull] private float[] TrackSampleData;
    private float TrackSampleVolume;
    public bool IsTrackLoaded => TrackSample != null && TrackSampleData != null;

    //answer SFX
    List<AnswerTimingPoint> answerTimingPoints = new();
    private readonly object answerSfxLock = new();
    //note SFX
    public NativeArray<bool> noteSfxPlaybackRequests = new(SFX_COUNT, Allocator.Persistent);
    public unsafe bool* SfxRequestsPtr => (bool*)noteSfxPlaybackRequests.GetUnsafePtr();
    List<AudioSample> NoteSfxs = new(SFX_COUNT);

    //SFX for recording
    private List<float[]> noteSfxSamplesData = new(SFX_COUNT);
    private float[] recordingBuffer;
    private float recordingInitialAudioTime;
    private float recordingSpeed = 1f;
    private int[] sfxPlayPointers = new int[SFX_COUNT]; //-1 is not playing

    public double GlobalAudioOffset { get; private set; }

    public bool IsShowingSongDetail => _timeProvider.AudioTime <= recordingInitialAudioTime + TimeProvider.SONG_DETAIL_OFFSET;
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

    private List<Guid> touchholdRiserPlayingTask = new();
    private bool isTouchholdRiserPlaying;
    private bool waitingForTrackAudioStart;

    private bool isInited;

    public AudioManager()
    {
        _audioManager = this;
        Bass.Configure(Configuration.UpdatePeriod, 20);
        Bass.Configure(Configuration.PlaybackBufferLength, 40);
        Bass.Init(-1, 44100);

        //Note SFX
        var sfxPath = MajEnv.GetPath("SFX");
        foreach (var filename in new[]
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

        isInited = true;
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

    public void UpdateAnswerSfx()
    {
        lock (answerSfxLock)
        {
            for (var i = 0; i < answerTimingPoints.Count; i++)
            {
                var timing = answerTimingPoints[i];

                if (timing.IsPlayed) continue;

                var thisFrameSec = _timeProvider.NoteTime;

                var delta = thisFrameSec - (timing.Timing + TRACK_ANSWER_PLAYBACK_OFFSET_SEC);
                if (delta > 0)
                {
                    if (timing.IsClock) noteSfxPlaybackRequests[ANSWER_CLOCK] = true;
                    else noteSfxPlaybackRequests[ANSWER] = true;

                    timing.IsPlayed = true;
                }
            }
        }
    }

    public void OnUpdate()
    {
        if (!isInited || _timeProvider.IsRecord) return;

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
                        if (isTouchholdRiserPlaying) break;

                        isTouchholdRiserPlaying = true;
                        NoteSfxs[TOUCHHOLD].PlayOneShot();
                    }
                    else
                    {
                        if (!isTouchholdRiserPlaying) break;

                        isTouchholdRiserPlaying = false;
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

    public void OnDestroy()
    {
        noteSfxPlaybackRequests.Dispose();
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
        TrackSample.Speed = _timeProvider.CurrentSpeed;
        TrackSample.Volume = TrackSampleVolume;

        waitingForTrackAudioStart = true;
        WaitForTrackAudioStart().Forget();

        async UniTask WaitForTrackAudioStart()
        {
            var offset = TRACK_ANSWER_PLAYBACK_OFFSET_SEC + GlobalAudioOffset;
            while (_timeProvider.AudioTime - offset < 0)
            {
                if (waitingForTrackAudioStart == false) return; //canceled
                await UniTask.Yield();
            }

            TrackSample!.Play();
            TrackSample!.CurrentSec = _timeProvider.AudioTime - offset;
            waitingForTrackAudioStart = false;
        }
    }

    public void PauseTrack() => TrackSample?.Pause();

    public void StopTrack()
    {
        waitingForTrackAudioStart = false;
        TrackSample?.Stop();
    }

    public void ResetState()
    {
        StopTrack();
        //StopTouchHoldSound();
        noteSfxPlaybackRequests[TOUCHHOLD] = false;
        touchholdRiserPlayingTask.Clear();

        lock (answerSfxLock)
            answerTimingPoints.Clear();
        for (var i = 0; i < noteSfxPlaybackRequests.Length; i++)
            noteSfxPlaybackRequests[i] = false;
    }


    //Sfx control

    public void GenerateAnswerSFX(SimaiChart chart, double ignoreOffset, int clockCount = 0)
    {
        if (chart.NoteTimings.IsEmpty) return;

        //Generate ClockSounds
        var firstBpm = chart.NoteTimings[0].Bpm;

        lock (answerSfxLock)
        {
            answerTimingPoints.Clear();
            if (firstBpm > 0f)
            {
                var interval = 60 / firstBpm;
                for (var i = 0; i < clockCount; i++)
                {
                    var timing = i * interval;
                    answerTimingPoints.Add(new AnswerTimingPoint(timing, true));
                }
            }
        }

        //Generate AnswerSounds
        var rawTimings = new List<float>();

        foreach (var timingPoint in chart.NoteTimings)
        {
            var startTiming = (float)timingPoint.Timing;
            if (startTiming < ignoreOffset) continue;

            if (!timingPoint.Notes.All              //无头别叫
                            (o => o.Type is SimaiNoteType.Slide
                            && o.IsSlideNoHead == true))
            {
                rawTimings.Add(startTiming);
            }


            var holds = Array.FindAll(timingPoint.Notes,
                o => o.Type is SimaiNoteType.Hold or SimaiNoteType.TouchHold);

            foreach (var hold in holds)
            {
                var endTiming = (float)(timingPoint.Timing + hold.HoldTime);
                rawTimings.Add(endTiming);
            }
        }

        rawTimings.Sort();

        var lastAddedTime = -1f;
        var epsilon = 0.001f; // 1ms 阈值

        lock (answerSfxLock)
        {
            foreach (var t in rawTimings)
            {
                if (lastAddedTime < 0 || t - lastAddedTime > epsilon)
                {
                    answerTimingPoints.Add(new AnswerTimingPoint(t, false));
                    lastAddedTime = t;
                }
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
    public void PlayTouchHoldSound(Guid guid)
    {
        noteSfxPlaybackRequests[TOUCHHOLD] = true;
        if (!touchholdRiserPlayingTask.Contains(guid))
            touchholdRiserPlayingTask.Add(guid);
    }

    public void PauseTouchHoldSound()
    {
        if (isTouchholdRiserPlaying)
            NoteSfxs[TOUCHHOLD].Pause(); //seen as still playing
    }
    public void ResumeTouchHoldSound()
    {
        if (isTouchholdRiserPlaying)
            NoteSfxs[TOUCHHOLD].Play();
    }
    public void StopTouchHoldSound(Guid guid)
    {
        if (touchholdRiserPlayingTask.Contains(guid))
            touchholdRiserPlayingTask.Remove(guid);

        if (touchholdRiserPlayingTask.Count == 0)
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

    public void PrepareRecordingBuffer(float initialAudioTime, float speed)
    {
        recordingInitialAudioTime = initialAudioTime;
        recordingSpeed = Math.Max(speed, 0.01f);
        var trackOffset = TRACK_ANSWER_PLAYBACK_OFFSET_SEC + (float)GlobalAudioOffset;
        var trackOutputStartTime = trackOffset - recordingInitialAudioTime;
        var leadAndTail = Math.Max(TimeProvider.SONG_DETAIL_OFFSET + 8f, trackOutputStartTime + 8f);
        var totalLen = TrackSample!.Length / recordingSpeed + leadAndTail; // 留给开头演出和结尾AP音效
        var size = (int)(totalLen * SAMPLERATE * CHANNELS);
        recordingBuffer = new float[size];
        Array.Clear(recordingBuffer, 0, recordingBuffer.Length);
        for (var i = 0; i < sfxPlayPointers.Length; i++) sfxPlayPointers[i] = -1; // 初始化指针
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

    public void UpdateSfxRecording(float deltaTime, float recordingElapsedTime)
    {
        // 计算当前帧在 buffer 中的起始采样位置
        var bufferStartPos = (int)(recordingElapsedTime * SAMPLERATE) * CHANNELS;
        // 这一帧应该写入的采样长度
        var samplesToCopy = (int)(deltaTime * SAMPLERATE) * CHANNELS;

        for (var i = 0; i < sfxPlayPointers.Length; i++)
        {
            if (i == TRACK_START || sfxPlayPointers[i] == -1) continue;

            var sfxData = noteSfxSamplesData[i];
            var vol = NoteSfxs[i].Volume;

            for (var j = 0; j < samplesToCopy; j++)
            {
                var sfxIdx = sfxPlayPointers[i] + j;
                if (sfxIdx < sfxData.Length)
                {
                    var dstIdx = bufferStartPos + j;
                    if (dstIdx >= 0 && dstIdx < recordingBuffer.Length)
                    {
                        // 同种类指针重置，不会自叠加
                        var mixed = recordingBuffer[dstIdx] + sfxData[sfxIdx] * vol;
                        recordingBuffer[dstIdx] = Math.Clamp(mixed, -1.0f, 1.0f);
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
            {
                var mixed = recordingBuffer[i] + trackStartSampleData[i] * NoteSfxs[TRACK_START].Volume;
                recordingBuffer[i] = Math.Clamp(mixed, -1.0f, 1.0f);
            }
        }


        var trackOffset = TRACK_ANSWER_PLAYBACK_OFFSET_SEC +
                            (float)GlobalAudioOffset;
        var initialTrackSec = recordingInitialAudioTime - trackOffset;
        var trackStartFrameCount = (int)((initialTrackSec + TimeProvider.SONG_DETAIL_OFFSET) * SAMPLERATE * recordingSpeed);
        var trackFrameCount = TrackSampleData.Length / CHANNELS;
        var recordingFrameCount = recordingBuffer.Length / CHANNELS;

        for (var dstFrame = 0; dstFrame < recordingFrameCount; dstFrame++)
        {
            var srcFrame = (initialTrackSec * SAMPLERATE) + dstFrame * recordingSpeed;
            if (srcFrame < 0 || srcFrame <= trackStartFrameCount) continue;
            if (srcFrame >= trackFrameCount - 1) break;

            var srcFrameFloor = (int)srcFrame;
            var t = srcFrame - srcFrameFloor;
            var srcIdx = srcFrameFloor * CHANNELS;
            var nextSrcIdx = srcIdx + CHANNELS;
            var dstIdx = dstFrame * CHANNELS;

            for (var ch = 0; ch < CHANNELS; ch++)
            {
                var sample = Mathf.Lerp(TrackSampleData[srcIdx + ch], TrackSampleData[nextSrcIdx + ch], t);
                var mixed = recordingBuffer[dstIdx + ch] + sample * TrackSampleVolume;
                recordingBuffer[dstIdx + ch] = Math.Clamp(mixed, -1.0f, 1.0f);
            }
        }

        WavFileWriter.WriteFile(outputPath, SAMPLERATE, CHANNELS, recordingBuffer);
    }



    private float[] GetSampleDataFromFile(string path)
    {
        var stream = Bass.CreateStream(path, 0, 0, BassFlags.Decode | BassFlags.Float);
        if (stream == 0) return Array.Empty<float>();

        var info = Bass.ChannelGetInfo(stream);
        var lenBytes = Bass.ChannelGetLength(stream);
        var rawData = new float[lenBytes / 4];
        Bass.ChannelGetData(stream, rawData, (int)lenBytes);
        Bass.StreamFree(stream);

        unsafe
        {
            fixed (float* dataPtr = rawData)
            {
                using var sourceArray = rawData.AsUnsafeNativeArrayScope();

                var ratio = (float)info.Frequency / SAMPLERATE;
                var sourceFrames = rawData.Length / 2;
                var targetFrames = (int)(sourceFrames / ratio);
                var outputArray = new NativeArray<float>(targetFrames * 2, Allocator.TempJob);

                new AudioResampleJob
                {
                    Source = sourceArray.Array,
                    Output = outputArray,
                    Ratio = ratio,
                    SrcFrameLimit = (rawData.Length / 2) - 1
                }.Schedule(targetFrames, 64, default).Complete();

                var result = outputArray.ToArray();
                outputArray.Dispose();
                return result;
            }
        }
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

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
    public struct AudioResampleJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> Source;
        [NativeDisableParallelForRestriction][WriteOnly] public NativeArray<float> Output;
        [ReadOnly] public float Ratio;
        [ReadOnly] public int SrcFrameLimit;

        public void Execute(int index)
        {
            float sourceIdx = index * Ratio;
            int i1 = (int)math.floor(sourceIdx);
            int i2 = (i1 < SrcFrameLimit) ? i1 + 1 : i1;

            float frac = sourceIdx - i1;
            int s1 = i1 << 1;
            int s2 = i2 << 1;
            int d = index << 1;

            Output[d] = math.lerp(Source[s1], Source[s2], frac);         // 左声道
            Output[d + 1] = math.lerp(Source[s1 + 1], Source[s2 + 1], frac); // 右声道
        }
    }
}