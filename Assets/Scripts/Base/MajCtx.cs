

public static class MajCtx
{
    public static AllPerfectManager _allPerfectManager { get; set; }
    public static AudioManager _audioManager { get; set; }
    public static BgManager _bgManager { get; set; }
    public static DataLoader _dataLoader { get; set; }
    public static EffectManager _effectManager { get; set; }
    public static InputManager _inputManager { get; set; }
    public static JudgeManager _judgeManager { get; set; }
    public static NoteManager _noteManager { get; set; }
    public static NoteSkinManager _noteSkinManager { get; set; }
    public static ObjectCounter _objectCounter { get; set; }
    public static PlayManager _playManager { get; set; }
    public static ScreenRecorder _screenRecorder { get; set; }
    public static TimeProvider _timeProvider { get; set; }
    public static WsServer _wsServer { get; set; }


    public const float FRAME_LENGTH_SEC = 1f / 60;
    public const float FRAME_LENGTH_MSEC = FRAME_LENGTH_SEC * 1000;

    public const string HanteiLine_LAYER = "HanteiLine";
    public const string TapLines_LAYER = "TapLines";
    public const string Slide_LAYER = "Slide";
    public const string Notes_LAYER = "Notes";
    public const string Touch_LAYER = "Touch";
}