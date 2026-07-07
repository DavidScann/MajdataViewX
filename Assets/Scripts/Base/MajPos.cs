using Unity.Mathematics;

public static class MajPos
{
    public static float2 GetBtnPos(SensorType sensor)
    {
        int i = (int)sensor;
        if (i >= 0 && i <= 7)
            return RingPos(4.8f, i + 1, false);
        return float2.zero;
    }

    public static float2 GetAreaPos(SensorType sensor)
    {
        int i = (int)sensor;
        if (i >= 0 && i <= 7)
            return RingPos(4.1f, i + 1, false);
        if (i >= 8 && i <= 15)
            return RingPos(2.3f, i - 7, false);
        if (i == 16)
            return float2.zero;
        if (i >= 17 && i <= 24)
            return RingPos(4.1f, i - 16, true);
        if (i >= 25 && i <= 32)
            return RingPos(3.0f, i - 24, true);
        return float2.zero;
    }

    private static float2 RingPos(float radius, int index1, bool altAngle)
    {
        var a = altAngle
            ? (index1 * -2f + 6f) * 0.125f * math.PI
            : (index1 * -2f + 5f) * 0.125f * math.PI;
        return new float2(radius * math.cos(a), radius * math.sin(a));
    }
}