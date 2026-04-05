public static class SensorAreaExtensions
{
    /// <summary>
    /// Gets a touch panel area with a specified difference from the current touch panel area
    /// </summary>
    /// <param name="source">current touch panel area</param>
    /// <param name="diff">specified difference</param>
    public static SensorArea Diff(this SensorArea source, int diff)
    {
        diff = diff % 8;
        if (diff == 0)
        {
            return source;
        }
        if (diff < 0)
        {
            diff = 8 + diff;
        }
        
        var result = (source.GetIndex() - 1 + diff) % 8;
        var group = source.GetGroup();
        switch (group)
        {
            case SensorGroup.A:
                return (SensorArea)result;
            case SensorGroup.B:
                result += 8;
                return (SensorArea)result;
            case SensorGroup.C:
                return source;
            case SensorGroup.D:
                result += 17;
                return (SensorArea)result;
            // SensorGroup.E
            default:
                result += 25;
                return (SensorArea)result;
        }
    }
    /// <summary>
    /// Get the group where the touch panel area is located
    /// </summary>
    /// <param name="source"></param>
    public static SensorGroup GetGroup(this SensorArea source)
    {
        var i = (int)source;
        if (i <= 7)
        {
            return SensorGroup.A;
        }
        else if (i <= 15)
        {
            return SensorGroup.B;
        }
        else if (i <= 16)
        {
            return SensorGroup.C;
        }
        else if (i <= 24)
        {
            return SensorGroup.D;
        }
        else
        {
            return SensorGroup.E;
        }
    }
    /// <summary>
    /// Get the index of the touch panel area within the group
    /// </summary>
    /// <param name="source"></param>
    public static int GetIndex(this SensorArea source)
    {
        var group = source.GetGroup();
        return group switch
        {
            SensorGroup.A => (int)source + 1,
            SensorGroup.B => (int)source - 7,
            SensorGroup.C => 1,
            SensorGroup.D => (int)source - 16,
            SensorGroup.E => (int)source - 24,
        };
    }
}