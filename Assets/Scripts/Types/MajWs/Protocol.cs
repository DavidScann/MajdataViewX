namespace MajdataViewX.Types.MajWs
{
    /// <summary>
    /// 请求类型（线格式枚举）。数值必须与 Edit 端 MajWsRequestType 完全一致。
    /// 0=Setting, 1=Load, 2=Update, 3=Play, 4=Pause, 5=Stop, 6=State。
    /// </summary>
    public enum MajWsRequestType
    {
        Setting = 0,
        Load = 1,
        Update = 2,
        Play = 3,
        Pause = 4,
        Stop = 5,
        State = 6,
    }

    /// <summary>
    /// 响应类型（线格式枚举）。数值必须与 Edit 端 MajWsResponseType 完全一致。
    /// </summary>
    public enum MajWsResponseType
    {
        Error = 400,
        Ok = 200,
        PlayStarted = 201,
        PlayResumed = 202,
        Heartbeat = 203,
        PlayPaused = 204,
        PlayStopped = 205,
        LoadOk = 206
    }
}
