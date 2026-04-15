using System;
#nullable enable

internal readonly struct MajWsRequestBase
{
    public MajWsRequestType requestType { get; init; }
    public object? requestData { get; init; }
}
public enum MajWsRequestType 
{
    Setting,
    Load,
    Play,
    Pause,
    Resume,
    Stop,
    State
}