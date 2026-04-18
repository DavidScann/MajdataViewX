using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MajSimai;
using Unity.Properties;
using UnityEngine;
using UnityEngine.SceneManagement;

#nullable enable

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
    
    private DataLoader loader;
    private TimeProvider timeProvider;
    private BgManager bgManager;
    private ScreenRecorder screenRecorder;
    private ObjectCounter objectCounter;
    private EffectManager effectManager;
    private AudioManager audioManager;

    private SpriteRenderer bgCover;
    
    private void Awake()
    {
        Majdata<PlayManager>.Instance = this;
    }

    private void Start()
    {
        loader = Majdata<DataLoader>.Instance!;
        timeProvider = Majdata<TimeProvider>.Instance!;
        bgManager = Majdata<BgManager>.Instance!;
        screenRecorder = Majdata<ScreenRecorder>.Instance!;
        objectCounter = Majdata<ObjectCounter>.Instance!;
        effectManager = Majdata<EffectManager>.Instance!;
        audioManager = Majdata<AudioManager>.Instance!;
        
        bgCover = GameObject.Find("BackgroundCover").GetComponent<SpriteRenderer>();
        
        IsReloading = false;
        _state = ViewStatus.Idle;
    }

    public void SyncSetting(MajViewSetting setting)
    {
        _setting = setting;
    }

    public async UniTask LoadAsync(string audioPath, string bgPath, string? pvPath)
    {
        while (_state is ViewStatus.Busy)
            await UniTask.Yield();
        _state = ViewStatus.Busy;
        try
        {
            //audio
            audioManager.LoadTrack(audioPath);
            
            //bg
            await UniTask.SwitchToMainThread();
            if (File.Exists(bgPath))
            {
                bgManager.LoadBG(bgPath);
            }
            
            //video
            if (pvPath is not null && File.Exists(pvPath))
            {
                bgManager.LoadVideo(pvPath);
            }
                
            _state = ViewStatus.Loaded;
        }
        catch (Exception ex)
        {
            _errMsg = ex.ToString();
            _state = ViewStatus.Error;
            throw;
        }
    }
    
    public async UniTask<bool> PlayAsync(PlaybackMode playmode, 
        double startAt, float speed, 
        string title, string artist, float offset, 
        string designer, string level, string fumen, 
        SimaiCommand[] commands, int difficulty, string? maidataPath = null)
    {
        while (_state is ViewStatus.Busy)
            await UniTask.Yield();
        
        _state = ViewStatus.Busy;
        try
        {
            await UniTask.SwitchToMainThread();

            _chart = await SimaiParser.ParseChartAsync(level, designer, fumen);
            
            bgManager.SetSpeed(speed);
            
            loader.noteSpeed = (float)(107.25 / (71.4184491 * Mathf.Pow(_setting.TapSpeed + 0.9975f, -0.985558604f)));
            loader.touchSpeed = _setting.TouchSpeed;
            loader.smoothSlideAnime = _setting.SmoothSlideAnime;
            loader.Load(_chart, startAt - offset, title, artist, difficulty);
            
            objectCounter.ComboSetActive(_setting.ComboStatusType);
            effectManager.SetDisplayMode(_setting.JudgeDisplayMode);
            InputManager.Mode = _setting.AutoMode;
            bgCover.color = new Color(0f, 0f, 0f, _setting.BackgroundDim);
            bgManager.ShowBG();
            bgManager.ShowVideo();

            var clockCount = 0;
            var clockCommand = Array.Find(commands, c => c.Prefix == "clock_count");
            if (clockCommand != default) int.TryParse(clockCommand.Value, out clockCount);
            audioManager.GenerateAnswerSFX(_chart, clockCount);
            
            Majdata<AllPerfectManager>.Instance!.enabled = false;
            Majdata<MultTouchHandler>.Instance!.clearSlots();

            switch (playmode)
            {
                case PlaybackMode.Normal:
                    timeProvider.SetStartTime(startAt, offset, speed, playmode);
                    audioManager.PlayTrack();
                    break;
                case PlaybackMode.IncludeOp:
                    bgManager.PlaySongDetail();
                    AudioManager.noteSfxPlaybackRequests[14] = true; //track_start
                    
                    timeProvider.SetStartTime(startAt, offset, speed, playmode);
                    audioManager.PlayTrack();
                    break;
                case PlaybackMode.Record:
                    bgManager.PlaySongDetail();
                    
                    GameObject.Find("CanvasButtons").SetActive(false);
                    if (!Directory.Exists(maidataPath))
                    {
                        throw new InvalidPathException($"maidata path is required");
                    }
                    timeProvider.SetStartTime(startAt, offset, speed, playmode, _setting.OutputFps);
                    screenRecorder.StartRecording(maidataPath, _setting.OutputFps);
                    break;
            }
            
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
        await ResumeAsync(_speed.Value);
    }
    
    public async UniTask ResumeAsync(float speed)
    {
        while (_state is ViewStatus.Busy)
            await UniTask.Yield();
        
        _state = ViewStatus.Busy;
        try
        {
            await UniTask.SwitchToMainThread();
            
            bgManager.SetSpeed(speed);
            bgManager.ContinueVideo();
            timeProvider.Resume(speed);
            
            audioManager.PlayTrack();
            
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
            
            audioManager.PauseTrack();
            
            bgManager.PauseVideo();
            timeProvider.Pause();
            
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
            
            audioManager.StopTrack();
            
            screenRecorder.StopRecording();
            IsReloading = true;
            SceneManager.LoadScene(1);
        }
        catch (Exception ex)
        {
            _errMsg = ex.ToString();
            _state = ViewStatus.Error;
            throw;
        }
    }
}