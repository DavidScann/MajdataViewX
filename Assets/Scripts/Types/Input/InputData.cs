public readonly ref struct InputData
{
    public SensorType Type { get; init; }
    public bool OldStatus { get; init; }
    public bool NowStatus { get; init; }
    public bool IsButton { get; init; }
    public bool IsClick => OldStatus == false && NowStatus == true;

}