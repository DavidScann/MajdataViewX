namespace MajdataViewX.Types.MajWs
{
    /// <summary>
    /// 响应类型（线格式枚举）。成员数值必须与 Edit 端一致。
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
