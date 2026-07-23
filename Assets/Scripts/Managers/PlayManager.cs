#nullable enable

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MajSimai;
using Unity.Properties;
using UnityEngine;

using static MajCtx;

#endregion

public class PlayManager : MonoBehaviour
{
    public static ViewSummary Summary => new()
    {
        State = _state,
        ErrMsg = _errMsg,
        Timeline = _thisFrameSec
    };

    public static bool IsReloading;

    private static SimaiChart _chart = SimaiChart.Empty;

    private static ViewStatus _state = ViewStatus.Idle;
    private static string _errMsg = string.Empty;
    private static float _thisFrameSec = 0f;

    private static double? _trackTime;
    private static double? _offset;
    private static float? _speed;

    private static MajViewSetting _setting = new();

    private static Thread _audioThread;

    private SpriteRenderer bgCover;
    private GameObject canvasButtons;

    private void Awake()
    {
        _playManager = this;
    }

    // 这里是游戏内部的东西的启动初始化
    private void Start()
    {
        IsReloading = false;
        _ = new AudioManager();

        bgCover = GameObject.Find("BackgroundCover").GetComponent<SpriteRenderer>();
        canvasButtons = GameObject.Find("CanvasButtons");

        _audioThread = new Thread(() => 
        {  
            while (_audioManager != null)
            {
                try { _audioManager.OnUpdate(); } catch { break; }
                Thread.Sleep(1);
            }
        })
        {
            IsBackground = true
        };
        _audioThread.Start();

        _state = CheckIsLoaded() ? ViewStatus.Loaded : ViewStatus.Idle;
    }

    private bool CheckIsLoaded() => _audioManager.IsTrackLoaded &&
                                    _bgManager.IsBgLoaded &&
                                    _bgManager.IsVideoLoaded;

    public void Setting(MajViewSetting setting, MajVolumeSetting volumeSetting)
    {
        _setting = setting;
        _audioManager.Setting(setting.GlobalAudioOffset, volumeSetting);
    }

    public async UniTask LoadAsync(string audioPath, string bgPath, string? pvPath)
    {
        Debug.Log($"[PlayManager] LoadAsync: START audioPath='{audioPath}' bgPath='{bgPath}' pvPath='{pvPath}'");
        while (_state is ViewStatus.Busy)
            await UniTask.Yield();
        _state = ViewStatus.Busy;

        try
        {
            await UniTask.SwitchToMainThread();

            //audio
            Debug.Log("[PlayManager] LoadAsync: loading audio...");
            _audioManager.LoadTrack(audioPath);
            Debug.Log($"[PlayManager] LoadAsync: audio done, IsTrackLoaded={_audioManager.IsTrackLoaded}");

            //bg
            if (File.Exists(bgPath))
            {
                BgManager.hasBg = true;
                Debug.Log("[PlayManager] LoadAsync: loading BG...");
                _bgManager.LoadBG(bgPath);
                Debug.Log("[PlayManager] LoadAsync: BG done");
            }
            else
            {
                BgManager.hasBg = false;
                Debug.Log("[PlayManager] LoadAsync: no BG file");
            }

            //video
            if (pvPath is not null && File.Exists(pvPath))
            {
                BgManager.hasVideo = true;
                Debug.Log("[PlayManager] LoadAsync: loading video...");
                _bgManager.LoadVideo(pvPath);
                Debug.Log("[PlayManager] LoadAsync: video done");
            }
            else
            {
                BgManager.hasVideo = false;
                Debug.Log("[PlayManager] LoadAsync: no video");
            }

            _state = ViewStatus.Loaded;
            Debug.Log($"[PlayManager] LoadAsync: COMPLETE, state={_state}");
        }
        catch (Exception ex)
        {
            _errMsg = ex.ToString();
            _state = ViewStatus.Error;
            Debug.LogError($"[PlayManager] LoadAsync: EXCEPTION: {ex}");
            throw;
        }
    }

    public async UniTask<bool> PlayAsync(PlaybackMode playmode,
        double startAt, float speed,
        string title, string artist, float offset,
        string designer, string level, string fumen,
        IList<SimaiCommand> commands, int difficulty,
        string? maidataPath = null)
    {
        while (_state is ViewStatus.Busy)
            await UniTask.Yield();

        var lastState = _state;
        _state = ViewStatus.Busy;
        try
        {
            await UniTask.SwitchToMainThread();

            //chart
            _chart = await SimaiParser.ParseChartAsync(level, designer, fumen);

            _dataLoader.noteSpeed = (float)(107.25 / (71.4184491 * Mathf.Pow(_setting.TapSpeed + 0.9975f, -0.985558604f)));
            _dataLoader.touchSpeed = _setting.TouchSpeed;
            _dataLoader.smoothSlideAnime = _setting.SmoothSlideAnime;
            var ignoreOffset = startAt - offset;
            //UI
            _objectCounter.StartOutput(_setting.ComboStatusType, _setting.UIType);
            _effectManager.SetDisplayMode(_setting.JudgeDisplayMode);
            //simulate
            _inputManager.Mode = _setting.AutoMode;
            _inputManager.ButtonFirst = _setting.ButtonFirst;
            //bg
            bgCover.color = new Color(0f, 0f, 0f, _setting.BackgroundDim);
            _bgManager.ShowBG();
            _bgManager.ShowVideo();
            //sfx
            var clockCount = 0;
            if (playmode != PlaybackMode.Normal)
            {
                var clockCommand = commands.FirstOrDefault(c => c.Prefix == "clock_count");
                if (clockCommand != default) int.TryParse(clockCommand.Value, out clockCount);
            }
            _audioManager.GenerateAnswerSFX(_chart, ignoreOffset, clockCount);

            switch (playmode)
            {
                case PlaybackMode.Normal:
                    await _dataLoader.Load(_chart,
                    ignoreOffset, title, artist, difficulty, _setting.LegacySlideLayer);

                    _allPerfectManager.enabled = false;
                    _timeProvider.SetStartTime(startAt, offset, speed, playmode);
                    _audioManager.PlayTrack();
                    break;
                case PlaybackMode.IncludeOp:
                    await _dataLoader.Load(_chart,
                    ignoreOffset, title, artist, difficulty, _setting.LegacySlideLayer);

                    _bgManager.PlaySongDetail();
                    _audioManager.noteSfxPlaybackRequests[AudioManager.TRACK_START] = true; //track_start

                    _allPerfectManager.enabled = true;
                    _timeProvider.SetStartTime(startAt, offset, speed, playmode);
                    _audioManager.PlayTrack();
                    break;
                case PlaybackMode.Record:
                    canvasButtons.SetActive(false);
                    if (!Directory.Exists(maidataPath))
                    {
                        throw new InvalidPathException($"maidata path is required");
                    }

                    await _dataLoader.Load(_chart,
                    ignoreOffset, title, artist, difficulty, _setting.LegacySlideLayer);

                    _bgManager.PlaySongDetail();

                    _allPerfectManager.enabled = true;
                    _state = ViewStatus.Playing;
                    _screenRecorder.StartRecording(maidataPath,
                        _setting.OutputFps,
                        _setting.ResizeBg,
                        () =>
                        {
                            _timeProvider.SetStartTime(startAt, offset, speed, playmode, _setting.OutputFps);
                        }).ContinueWith(() =>
                    {
                        canvasButtons.SetActive(true);
                        _state = ViewStatus.Loaded;
                    }).Forget();
                    return true; //directly return
            }

            //save last speed for resume
            _speed = speed;

            _state = ViewStatus.Playing;
            return true;
        }
        catch (Exception ex)
        {
            _errMsg = ex.ToString();
            _state = ViewStatus.Error;
            throw;
        }
    }

    public async UniTask ResumeAsync()
    {
        await ResumeAsync(_speed!.Value);
    }

    public async UniTask ResumeAsync(float speed)
    {
        while (_state is ViewStatus.Busy)
            await UniTask.Yield();

        _state = ViewStatus.Busy;
        try
        {
            await UniTask.SwitchToMainThread();

            _timeProvider.Resume(speed);

            _bgManager.ContinueVideo();

            _audioManager.PlayTrack();
            _audioManager.ResumeTouchHoldSound();

            _state = ViewStatus.Playing;
        }
        catch (Exception ex)
        {
            _errMsg = ex.ToString();
            _state = ViewStatus.Error;
            throw;
        }
    }

    public async UniTask PauseAsync()
    {
        while (_state is ViewStatus.Busy)
            await UniTask.Yield();

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
            throw;
        }
    }

    public async UniTask StopAsync()
    {
        while (_state is ViewStatus.Busy)
            await UniTask.Yield();

        _state = ViewStatus.Busy;
        try
        {
            await UniTask.SwitchToMainThread();

            _screenRecorder.StopRecording();
            //if not so, the last frame will be like after ResetAllManagers
            await UniTask.Yield();

            IsReloading = true;
            ResetAllManagers();
            // IsReloading = false;
            // in NoteManager, wait for notes cleared
        }
        catch (Exception ex)
        {
            _errMsg = ex.ToString();
            _state = ViewStatus.Error;
            throw;
        }
    }

    private void ResetAllManagers()
    {
        _screenRecorder.ResetState();
        _objectCounter.ResetState();
        UniTask.WhenAll(_noteJudgeManager.ResetState());
        _noteManager.ResetState();
        _multTouchHandler.ResetState();
        _timeProvider.ResetState();
        _audioManager.ResetState();
        _screenRecorder.ResetState();
        _bgManager.ResetState();
        //_effectManager.ResetState();
        _inputManager.ResetState();
        _allPerfectManager.ResetState();
        _dataLoader.ResetState();

        _state = CheckIsLoaded() ? ViewStatus.Loaded : ViewStatus.Idle;
        bgCover.color = new Color(0f, 0f, 0f, 0f);
    }

    private void OnDestroy()
    {
        if (_audioThread != null && _audioThread.IsAlive)
            _audioThread.Join(1000);
        _audioManager.OnDestroy();
        _audioThread = null;
    }
}