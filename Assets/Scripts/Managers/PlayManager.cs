#nullable enable

using Cysharp.Threading.Tasks;
using MajdataViewX.Base;
using MajdataViewX.Notes;
using MajdataViewX.Notes.SlideUtils;
using MajdataViewX.Types.Enums;
using MajdataViewX.Types.MajSetting;
using MajdataViewX.Types.MajWs;
using Cimai;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Threading;
using Unity.Properties;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using static MajdataViewX.Base.MajCtx;

namespace MajdataViewX.Managers
{
    public class PlayManager : MonoBehaviour
    {
        public static ViewSummary Summary => new()
        {
            State = _state,
            ErrMsg = _errMsg,
        };

        private static ViewStatus _state = ViewStatus.Idle;
        private static string _errMsg = string.Empty;

        private static Thread? _audioManagerThread;
        private static int _audioManagerThreadRunning;

        private static MajViewSetting _setting = new();
        private static float _currentOffset;
        private static SimaiChart? _chart;

        private SpriteRenderer _bgCover = null!;
        private SpriteRenderer _bgOutsideCover = null!;
        private GameObject _canvasButtons = null!;

        private void Awake()
        {
            _playManager = this;
        }

        // 这里是游戏内部的东西的启动初始化
        private void Start()
        {
            _bgCover = GameObject.Find("BgCover").GetComponent<SpriteRenderer>();
            _bgOutsideCover = GameObject.Find("BgOutsideCover").GetComponent<SpriteRenderer>();
            _canvasButtons = GameObject.Find("CanvasButtons");

            _ = new AudioManager();
            Volatile.Write(ref _audioManagerThreadRunning, 1);
            _audioManagerThread = new Thread(() =>
            {
                while (Volatile.Read(ref _audioManagerThreadRunning) != 0)
                {
                    _audioManager.OnUpdate();
                    Thread.Sleep(1);
                }
            })
            {
                IsBackground = true,
                Name = "Majdata SFX Trigger",
                Priority = System.Threading.ThreadPriority.AboveNormal,
            };
            _audioManagerThread.Start();

            MajBurst.__DataSS.Data = new MajBurstData
            {
                TimeData = new(),
                InputData = new(),
                MultTouchHandler = new(),
                GlobalRandom = new((uint)"MajdataX".GetHashCode()),
            };
            //MajBurst.TimeData.Init();
            MajBurst.InputData.Init();
            MajBurst.MultTouchHandler.Init();

            _ = new InputManager();


            SlideTableNeo.InitializeStandardSlideTable();
        }

        private bool CheckIsLoaded() => _audioManager.IsTrackLoaded &&
                                        _bgManager.IsBgLoaded &&
                                        _bgManager.IsVideoLoaded;

        public async UniTask LoadAsync(string audioPath, string bgPath, string? pvPath)
        {
            while (_state is ViewStatus.Busy)
                await UniTask.Yield();
            _state = ViewStatus.Busy;

            try
            {
                await UniTask.SwitchToMainThread();

                //audio
                _audioManager.LoadTrack(audioPath);

                //bg
                if (File.Exists(bgPath))
                {
                    BgManager.HasBg = true;
                    _bgManager.LoadBg(bgPath);
                }
                else
                {
                    BgManager.HasBg = false;
                }

                //video
                if (pvPath is not null && File.Exists(pvPath))
                {
                    BgManager.HasVideo = true;
                    _bgManager.LoadVideo(pvPath);
                }
                else
                {
                    BgManager.HasVideo = false;
                }

                _state = ViewStatus.Loaded;
            }
            catch (Exception ex)
            {
                _errMsg = ex.ToString();
                _state = ViewStatus.Error;
            }
        }

        public void Setting(MajViewSetting setting, MajVolumeSetting volumeSetting)
        {
            _setting = setting;

            NoteHelper.NoteSettingsSS.Data = new NoteSettings()
            {
                AutoPlayMode = _setting.AutoMode,
                TapSpeed = (float)(107.25 / (71.4184491 * Mathf.Pow(_setting.TapSpeed + 0.9975f, -0.985558604f))),
                TouchSpeed = _setting.TouchSpeed,
                LegacySlideLayer = _setting.LegacySlideLayer,
                SmoothSlideAnime = _setting.SmoothSlideAnime,
                MineAutoSlide = _setting.MineAutoSlide,
                AbsolutePerfect = _setting.AbsolutePerfect,
            };
            //audio
            _audioManager.Setting(setting.GlobalAudioOffset, volumeSetting);
            //simulate
            _inputManager.ShowHand = _setting.ShowHand;
            //counter
            _objectCounter.Setting(_setting.ComboStatusType, _setting.UIType);
            //bg
            _bgCover.color = new Color(0f, 0f, 0f, _setting.BackgroundDim);
            _bgOutsideCover.color = new Color(0f, 0f, 0f, _setting.BackgroundOutsideDim);
            _bgManager.resizeBg = _setting.ResizeBg;
            //fork: Fast/Late judge display + realistic wide-hand DJAuto
            _effectManager.SetDisplayMode(setting.JudgeDisplayMode);
            InputManager.WideHands = _setting.WideHands;
        }

        public async UniTask UpdateAsync(
            string? chartText,
            int selectedDifficulty,
            string title,
            string artist,
            string level,
            string designer,
            float offset,
            int clockCount)
        {
            while (_state is ViewStatus.Busy)
                await UniTask.Yield();

            var previousState = _state;
            _state = ViewStatus.Busy;
            
            var chart = SimaiChart.Parse(chartText ?? string.Empty);

            _chart?.Dispose();
            _chart = chart;
            _currentOffset = offset;
            _timeProvider.offset = offset;

            //answer
            _audioManager.GenerateAnswerSFX(chart, clockCount);

            //counter
            _objectCounter.ResetLoaded();
            _objectCounter.CountNoteSum(chart);
            _objectCounter.ReportMeterBpm(chart);

            await _dataLoader.Load(chart, title, artist, level, designer, selectedDifficulty);

            _state = previousState;
        }

        public async UniTask PlayAsync(PlaybackMode playmode, double startAt, float speed, string recordPath)
        {
            while (_state is ViewStatus.Busy)
                await UniTask.Yield();

            if (_state is not (ViewStatus.Loaded or ViewStatus.Paused))
                return;

            _state = ViewStatus.Busy;
            try
            {
                await UniTask.SwitchToMainThread();
                var ignoreOffset = startAt - _currentOffset;

                //bg
                _bgManager.ShowBg();
                // Async: Linux decodes bg video via the ffmpeg pipe (Unity
                // VideoPlayer has no H.264 decoder there); elsewhere this
                // runs the VideoPlayer path.
                await _bgManager.ShowVideoAsync(_setting.ResizeBg, startAt);
                //sfx
                _audioManager.ResetAnswerSFX(ignoreOffset);
                //counter
                _objectCounter.ResetCur();
                _objectCounter.CountIgnoreNoteCountAsync(_chart, ignoreOffset);
                //notes
                //MajBurst.InputData.ResetState(); in ResetLoadedNote
                _noteManager.ResetState(); //reset DJAuto hands (PlayUpdateJob is still running when IsStart==false)
                _noteManager.ResetLoadedNote(ignoreOffset);
                _noteManager.ResetLoadedPlay(ignoreOffset);
                MajBurst.MultTouchHandler.ResetMultTouchState();

                switch (playmode)
                {
                    case PlaybackMode.Normal:
                        _allPerfectManager.enabled = false;

                        _timeProvider.SetStartTime(startAt, _currentOffset, speed, playmode);
                        _audioManager.PlayTrack();
                        break;
                    case PlaybackMode.IncludeOp:
                        _allPerfectManager.enabled = true;

                        _bgManager.PlaySongDetail();
                        _audioManager.noteSfxPlaybackRequests[AudioManager.TRACK_START] = true; //track_start

                        _timeProvider.SetStartTime(startAt, _currentOffset, speed, playmode);
                        _audioManager.PlayTrack();
                        break;
                    case PlaybackMode.Record:
                        if (!Directory.Exists(recordPath))
                        {
                            throw new InvalidPathException($"maidata path is required");
                        }

                        _canvasButtons.SetActive(false);
                        _allPerfectManager.enabled = true;

                        _bgManager.PlaySongDetail();
                        // The export ends at the last note's END plus the All
                        // Perfect banner tail (see ComputeRecordEndTime); the
                        // loop's own overrun is only a safety net for a
                        // missing/late banner.
                        var recordEndTime = ComputeRecordEndTime();
                        _screenRecorder.StartRecording(recordPath,
                            _setting.OutputFps, _setting.ExportQuality,
                            _setting.ExportWidth, _setting.ExportHeight, _setting.ExportCodec,
                            recordEndTime,
                            () =>
                            {
                                _timeProvider.SetStartTime(startAt, _currentOffset, speed, playmode, _setting.OutputFps);
                            }).ContinueWith(() =>
                        {
                            _canvasButtons.SetActive(true);
                            _state = ViewStatus.Loaded;
                            // The AP finish does not stop the record (it runs
                            // to the chart end), so report completion here.
                            _wsServer.SendStopResponse();
                        }).Forget();
                        break;
                    case PlaybackMode.Preview:
                        _allPerfectManager.enabled = false;
                        _state = ViewStatus.Paused;
                        return;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(playmode), playmode, null);
                }

                _state = ViewStatus.Playing;
                return;
            }
            catch (Exception ex)
            {
                _errMsg = ex.ToString();
                _state = ViewStatus.Error;
                return;
            }
        }

        /// <summary>
        /// Record window end: last note's END plus the All Perfect banner
        /// tail, capped at the audio end. Chart time and AudioTime share the
        /// offset basis (first note = 0, AudioTime = NoteTime + offset), so
        /// both caps convert the same way.
        /// </summary>
        private double ComputeRecordEndTime()
        {
            const float ApShowSec = 3.5f;
            double lastNoteEnd = 0;
            if (_chart is not null && !_chart.IsDisposed)
            {
                var timings = _chart.Timings;
                for (var ti = 0; ti < timings.Length; ti++)
                {
                    var timing = timings[ti];
                    var notes = timing.Notes;
                    for (var ni = 0; ni < notes.Length; ni++)
                    {
                        var note = notes[ni];
                        var end = note.Type switch
                        {
                            SimaiNoteType.HOLD or SimaiNoteType.TOUCHHOLD =>
                                timing.Time + note.Duration,
                            // Slide end = shoot time + slide duration, matching
                            // NoteManager.Loader (shootTime = Time + SlideShootDelay).
                            SimaiNoteType.SLIDE =>
                                timing.Time + note.SlideShootDelay + note.Duration,
                            _ => timing.Time,
                        };
                        if (end > lastNoteEnd) lastNoteEnd = end;
                    }
                }
            }
            var recordEndTime = Math.Min(
                _audioManager.TrackLength + _currentOffset,
                lastNoteEnd + ApShowSec + _currentOffset);
            Debug.Log($"[PlayManager] record: TrackLength={_audioManager.TrackLength:F2}s " +
                      $"lastNoteEnd={lastNoteEnd:F2}s recordEndTime={recordEndTime:F2}s");
            if (recordEndTime <= 0)
            {
                // A failed track decode collapses the record window to zero
                // and ends the export after ~1s with no notes judged. Run to
                // the chart-derived end instead of silently truncating.
                recordEndTime = lastNoteEnd + ApShowSec;
                Debug.LogWarning($"[PlayManager] track length unavailable ({_audioManager.TrackLength:F2}s); " +
                                 $"export will run to the chart end ({recordEndTime:F2}s)");
            }
            return recordEndTime;
        }

        public async UniTask PauseAsync()
        {
            while (_state is ViewStatus.Busy)
                await UniTask.Yield();

            if (_state is not ViewStatus.Playing)
                return;

            _state = ViewStatus.Busy;
            try
            {
                await UniTask.SwitchToMainThread();

                _timeProvider.Pause();

                _bgManager.PauseVideo();

                _audioManager.PauseTrack();
                _audioManager.PauseTouchHoldSound();

                _state = ViewStatus.Paused;
            }
            catch (Exception ex)
            {
                _errMsg = ex.ToString();
                _state = ViewStatus.Error;
            }
        }

        public async UniTask StopAsync()
        {
            while (_state is ViewStatus.Busy)
                await UniTask.Yield();

            if (_state is not (ViewStatus.Playing or ViewStatus.Paused or ViewStatus.Error))
                return;

            _state = ViewStatus.Busy;
            try
            {
                await UniTask.SwitchToMainThread();

                _screenRecorder.StopRecording();
                await UniTask.Yield();

                //_objectCounter.ResetCur();
                _timeProvider.ResetState();
                _audioManager.ResetState();
                _bgManager.ResetState();
                _effectManager.ResetState();
                _allPerfectManager.ResetState();

                _state = CheckIsLoaded() ? ViewStatus.Loaded : ViewStatus.Idle;
            }
            catch (Exception ex)
            {
                _errMsg = ex.ToString();
                _state = ViewStatus.Error;
            }
        }

        private void OnDestroy()
        {
            Volatile.Write(ref _audioManagerThreadRunning, 0);
            if (_audioManagerThread is { IsAlive: true } &&
                _audioManagerThread != Thread.CurrentThread)
            {
                _audioManagerThread.Join();
            }
            _audioManagerThread = null;

            _audioManager.OnDestroy();
            _inputManager.OnDestroy();
            MajBurst.InputData.Dispose();
        }
    }
}