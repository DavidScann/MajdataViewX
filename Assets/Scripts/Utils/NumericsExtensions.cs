using System;

public static class NumericsExtensions
{
    public static T Clamp<T>(this T source, in T min, in T max) where T : IComparable<T>
    {
        if (source.CompareTo(min) < 0)
            return min;
        else if (source.CompareTo(max) > 0)
            return max;
        else
            return source;
    }
}