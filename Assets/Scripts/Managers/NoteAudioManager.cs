using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MajSimai;
using ManagedBass;
using UnityEngine;

public class NoteAudioManager : MonoBehaviour
{
    static List<AnswerTimingPoint> answerTimingPoints = new();
    static bool[] noteSFXPlaybackRequests = new bool[14];
    static List<AudioSample> NoteSFXs = new(14);
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
    
    private bool isTouchHoldRiserPlaying = false;

    private void Awake()
    {
        Majdata<NoteAudioManager>.Instance = this;
        Bass.Init();
        
        //Note SFX
        foreach (var filename in new string[14]
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
                     "answer_clock.wav"
                 })
        {
            var path = Path.Combine(new DirectoryInfo(Application.dataPath).Parent!.FullName, 
                "SFX", filename);
            var sample = new AudioSample(path);
            
            sample.SampleType = filename switch
            {
                var p when p.StartsWith("answer") => SampleType.Answer,
                var p when p.StartsWith("break") => SampleType.Break,
                var p when p.StartsWith("slide") => SampleType.Slide,
                var p when p.StartsWith("tap") => SampleType.Tap,
                var p when p.StartsWith("touch") => SampleType.Touch,
                _ => sample.SampleType
            };
            NoteSFXs.Add(sample);
        }
    }

    private void Update()
    {
        //Answer SFX
        foreach (var timing in answerTimingPoints)
        {
            var thisFrameSec = Majdata<TimeProvider>.Instance!.AudioTime;

            var delta = thisFrameSec - (timing.Timing + ANSWER_PLAYBACK_OFFSET_SEC);
            if (delta > 0)
            {
                if (timing.IsClock) noteSFXPlaybackRequests[ANSWER_CLOCK] = true;
                else noteSFXPlaybackRequests[ANSWER] = true;
            }
        }
        
        //Note SFX
        for (var i = 0; i < noteSFXPlaybackRequests.Length; i++)
        {
            var isRequested = noteSFXPlaybackRequests[i];
            switch (i)
            {
                case TAP_PERFECT:
                    if (isRequested) NoteSFXs[TAP_PERFECT].PlayOneShot();
                    break;
                case TAP_GREAT:
                    if (isRequested) NoteSFXs[TAP_GREAT].PlayOneShot();
                    break;
                case TAP_GOOD:
                    if (isRequested) NoteSFXs[TAP_GOOD].PlayOneShot();
                    break;
                case TAP_EX:
                    if (isRequested) NoteSFXs[TAP_EX].PlayOneShot();
                    break;
                case BREAK_JUDGE:
                    if (isRequested) NoteSFXs[BREAK_JUDGE].PlayOneShot();
                    break;
                case BREAK_SFX:
                    if (isRequested) NoteSFXs[BREAK_SFX].PlayOneShot();
                    break;
                case SLIDE:
                    if (isRequested) NoteSFXs[SLIDE].PlayOneShot();
                    break;
                case BREAK_SLIDE:
                    if (isRequested) NoteSFXs[BREAK_SLIDE].PlayOneShot();
                    break;
                case BREAK_SLIDE_JUDGE:
                    if (isRequested)
                    {
                        NoteSFXs[BREAK_SLIDE_JUDGE].PlayOneShot();
                        NoteSFXs[BREAK_SFX].PlayOneShot();
                    }
                    break;
                case TOUCH:
                    if (isRequested) NoteSFXs[TOUCH].PlayOneShot();
                    break;
                case TOUCHHOLD:
                    if (isRequested)
                    {
                        if (isTouchHoldRiserPlaying)
                            break;
                        isTouchHoldRiserPlaying = true;
                        NoteSFXs[TOUCHHOLD].PlayOneShot();
                    }
                    else
                    {
                        if (!isTouchHoldRiserPlaying)
                            break;
                        isTouchHoldRiserPlaying = false;
                        NoteSFXs[TOUCHHOLD].Stop();
                    }
                    break;
                case FIREWORK:
                    if (isRequested) NoteSFXs[FIREWORK].PlayOneShot();
                    break;
                case ANSWER:
                    if (isRequested) NoteSFXs[ANSWER].PlayOneShot();
                    break;
                case ANSWER_CLOCK:
                    if (isRequested) NoteSFXs[ANSWER_CLOCK].PlayOneShot();
                    break;
            }
        }
        
        //clear
        for (var i = 0; i < noteSFXPlaybackRequests.Length; i++) noteSFXPlaybackRequests[i] = false;
    }

    private void OnDestroy()
    {
        Bass.Stop();
        Bass.Free();
    }

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
        foreach (var timingPoint in chart.NoteTimings)
        {
            var timing = (float)timingPoint.Timing;
            answerTimingPoints.Add(new AnswerTimingPoint(timing, false));
            var holds = Array.FindAll(timingPoint.Notes,
                o => o.Type is SimaiNoteType.Hold or SimaiNoteType.TouchHold);
            foreach (var hold in holds)
            {
                var newTime = (float)(timingPoint.Timing + hold.HoldTime);
                if (!chart.NoteTimings.Any(o => Math.Abs(o.Timing - newTime) < 0.001) &&
                    !answerTimingPoints.Any(o => Math.Abs(o.Timing - newTime) < 0.001))
                    answerTimingPoints.Add(new AnswerTimingPoint(newTime, false));
            }
        }
    }

    public void PlayTapSound(JudgeType judgeType)
    {
        
    }

    private struct AnswerTimingPoint
    {
        public readonly float Timing;
        public readonly bool IsClock;
       // public bool IsPlayed;

        public AnswerTimingPoint(float timing, bool isClock)
        {
            Timing = timing;
            IsClock = isClock;
            //IsPlayed = false;
        }
    }
}