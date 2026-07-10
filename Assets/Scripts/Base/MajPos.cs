using Unity.Burst;
using Unity.Mathematics;

public static class MajPos
{
    public const float A_RADIUS = 0.987f;
    public const float B_RADIUS = 0.673f;
    public const float C_RADIUS = 1.215f;
    public const float D_RADIUS = 0.974f;
    public const float E_RADIUS = 0.515f;

    [BurstCompile]
    public static float GetSensorRadius(SensorType sensor)
    {
        int i = (int)sensor;
        if (i >= 0 && i <= 7)
            return A_RADIUS;
        if (i >= 8 && i <= 15)
            return B_RADIUS;
        if (i == 16)
            return C_RADIUS;
        if (i >= 17 && i <= 24)
            return D_RADIUS;
        if (i >= 25 && i <= 32)
            return E_RADIUS;
        return MajCtx.DJAUTO_HAND_RADIUS;
    }

    /// <summary>
    /// 获取按键的世界坐标
    /// </summary>
    /// <param name="key">按键索引，注意是在0~7之间</param>
    /// <returns></returns>
    [BurstCompile]
    public static float2 GetBtnPos(int key)
    {
        if (key >= 0 && key <= 7)
            return RingPos(4.8f, key + 1, false);
        return float2.zero;
    }

    /// <summary>
    /// 获取判定区的世界坐标
    /// </summary>
    /// <param name="sensor">判定区</param>
    /// <returns></returns>
    [BurstCompile]
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
    [BurstCompile]
    private static float2 RingPos(float radius, int index1, bool altAngle)
    {
        var a = altAngle
            ? (index1 * -2f + 6f) * 0.125f * math.PI
            : (index1 * -2f + 5f) * 0.125f * math.PI;
        return new float2(radius * math.cos(a), radius * math.sin(a));
    }
}