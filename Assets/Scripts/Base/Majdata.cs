#nullable enable

internal static class Majdata<T>
{
    private static T? _instance = default;
    
    /// <summary>
    /// Get or set a globally unique instance
    /// </summary>
    public static ref T? Instance => ref _instance;

    /// <summary>
    /// Release the instance
    /// </summary>
    public static void Free() => _instance = default;
}
