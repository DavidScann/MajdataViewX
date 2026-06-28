using Unity.Collections;

// Burst-compatible unmanaged slide data types.
// SlideAreaData is the flat equivalent of the managed SlideArea class.
// ArrowPose/OKPose mirror the auto-generated nested structs
// in SlideArrowTable / SlideOKTable for NativeArray storage.
public struct SlideAreaData
{
    public SensorType Area0;
    public SensorType Area1;
    public byte AreaCount;
    public byte ArrowProgressWhenOn;
    public byte ArrowProgressWhenFinished;
    public bool IsSkippable;
    public bool IsLast;
    public bool On;
    public bool Off;

    public readonly bool IsFinished => IsLast ? On : (On && Off);

    public void Judge(bool status)
    {
        if (status) On = true;
        else if (On) Off = true;
    }
}

public struct SlideTableData
{
    public int Offset;
    public byte Count;
    public float Const;
}

public struct WifiTableData
{
    public int LeftOffset;
    public byte LeftCount;
    public int CenterOffset;
    public byte CenterCount;
    public int RightOffset;
    public byte RightCount;
    public float Const;
}

public struct ArrowPose
{
    public float X;
    public float Y;
    public float RotZ;
}

public struct OKPose
{
    public float X;
    public float Y;
    public float RotZ;
}

public struct ShapeInfo
{
    public int ArrowOffset;
    public byte ArrowCount;
    public int AreaOffset;
    public byte AreaCount;
    public OKPose OK;
    public float Const;
}

public struct SlideTableStore
{
    [NativeDisableParallelForRestriction]
    public NativeArray<ShapeInfo> Shapes;
    [NativeDisableParallelForRestriction]
    public NativeArray<ArrowPose> ArrowPoses;
    [NativeDisableParallelForRestriction]
    public NativeArray<SlideAreaData> Areas;
    public WifiTableData Wifi;
}
