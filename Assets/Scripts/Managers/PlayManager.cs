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

    private static AudioSample? _audioSample = null;

    private static double? _startAt;
    private static double? _startTime;
    private static float? _speed;
 
    private static MajViewSetting _setting = new();
    
    private DataLoader loader;
    private TimeProvider timeProvider;
    private BgManager bgManager;
    private ScreenRecorder screenRecorder;
    private MultTouchHandler multTouchHandler;
    private ObjectCounter objectCounter;
    private EffectManager effectManager;

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
        multTouchHandler = Majdata<MultTouchHandler>.Instance!;
        objectCounter = Majdata<ObjectCounter>.Instance!;
        effectManager = Majdata<EffectManager>.Instance!;
        
        bgCover = GameObject.Find("BackgroundCover").GetComponent<SpriteRenderer>();
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
            if(_audioSample is not null) _audioSample.Dispose();
            _audioSample = new AudioSample(audioPath)
            {
                SampleType = SampleType.Track
            };
            
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
    
    public async UniTask<bool> PlayAsync(PlaybackMode mode, double startAt, double startTime, float speed, 
        string fumen, string title, string artist, int diff, string? maidataPath)
    {
        while (_state is ViewStatus.Busy)
            await UniTask.Yield();
        
        _state = ViewStatus.Busy;
        try
        {
            var isRecord = false;
            
            await UniTask.SwitchToMainThread();
            
            _chart = await SimaiParser.ParseChartAsync(string.Empty, string.Empty, fumen);
            loader.Load(_chart, startTime, title, artist, diff);
            
            _audioSample!.Speed = speed;
            bgManager.SetSpeed(speed);
            loader.noteSpeed = (float)(107.25 / (71.4184491 * Mathf.Pow(_setting.TapSpeed + 0.9975f, -0.985558604f)));
            loader.touchSpeed = _setting.TouchSpeed;
            
            loader.smoothSlideAnime = _setting.SmoothSlideAnime;
            objectCounter.ComboSetActive(_setting.ComboStatusType);
            effectManager.SetDisplayMode(_setting.JudgeDisplayMode);
            bgCover.color = new Color(0f, 0f, 0f, _setting.BackgroundDim);
            
            Majdata<PlayAllPerfect>.Instance!.enabled = false;
            Majdata<MultTouchHandler>.Instance!.clearSlots();

            switch (mode)
            {
                case PlaybackMode.IncludeOp:
                    bgManager.PlaySongDetail();
                    break;
                case PlaybackMode.Record:
                    bgManager.PlaySongDetail();
                    GameObject.Find("CanvasButtons").SetActive(false);
                    if (!Directory.Exists(maidataPath))
                    {
                        throw new InvalidPathException($"maidata path is required");
                    }
                    screenRecorder.StartRecording(maidataPath);
                    isRecord = true;
                    break;
            }

            _startAt = startAt;
            _startTime = startTime;
            _speed = speed;
            
            _state = ViewStatus.Playing;
            _audioSample!.Play();
            timeProvider.SetStartTime(startAt, startTime, speed, isRecord);
            
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
        if (_startAt is not null && _startTime is not null && _speed is not null)
            await ResumeAsync(_startAt.Value, _startTime.Value, _speed.Value);
    }
    
    public async UniTask ResumeAsync(double startAt, double startTime, float speed)
    {
        while (_state is ViewStatus.Busy)
            await UniTask.Yield();
        
        _state = ViewStatus.Busy;
        try
        {
            await UniTask.SwitchToMainThread();
            
            _audioSample!.Play();
            
            bgManager.SetSpeed(speed);
            bgManager.ContinueVideo();
            timeProvider.SetStartTime(startAt, startTime, speed);
            
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
            
            _audioSample!.Pause();
            
            bgManager.PauseVideo();
            timeProvider.isStart = false;
            
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
            
            _audioSample!.Stop();
            
            screenRecorder.StopRecording();
            timeProvider.ResetStartTime();
            IsReloading = false;
            _state = ViewStatus.Idle;
            SceneManager.LoadScene(1); //TODO: dont use reloading scene
        }
        catch (Exception ex)
        {
            _errMsg = ex.ToString();
            _state = ViewStatus.Error;
            throw;
        }
    }
}