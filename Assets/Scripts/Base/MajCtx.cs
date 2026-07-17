#pragma warning disable IDE1006 // 命名样式
using Unity.Burst;
using Unity.IL2CPP.CompilerServices;

[Il2CppEagerStaticClassConstruction]
public static class MajCtx
{
    public static AllPerfectManager _allPerfectManager { get; set; }
    public static AudioManager _audioManager { get; set; }
    public static BgManager _bgManager { get; set; }
    public static DataLoader _dataLoader { get; set; }
    public static EffectManager _effectManager { get; set; }
    public static InputManager _inputManager { get; set; }
    public static NoteManager _noteManager { get; set; }
    public static SkinManager _noteSkinManager { get; set; }
    public static ObjectCounter _objectCounter { get; set; }
    public static PlayManager _playManager { get; set; }
    public static ScreenRecorder _screenRecorder { get; set; }
    public static TimeProvider _timeProvider { get; set; }
    public static WsServer _wsServer { get; set; }


    public const float FRAME_LENGTH_SEC = 1f / 60;
    public const float FRAME_LENGTH_MSEC = FRAME_LENGTH_SEC * 1000;

    public const int BUTTON_COUNT = 8;
    public const int SENSOR_COUNT = 33;

    public const float DJAUTO_HAND_RADIUS = 0.45f;
    public const float DJAUTO_WIFI_RADIUS = 1.00f;
    public const float BUTTON_HIT_RENDER_RADIUS = 0.4f;

    [BurstCompile]
    public static SensorType GetSensor(char areaPos, int startPos)
    {
        return areaPos switch
        {
            'A' => (SensorType)(startPos - 1),
            'B' => (SensorType)(startPos + 7),
            'C' => SensorType.C,
            'D' => (SensorType)(startPos + 16),
            'E' => (SensorType)(startPos + 24),
            _ => SensorType.A1,
        };
    }
}