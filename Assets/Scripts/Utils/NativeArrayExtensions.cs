
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

public static class NativeArrayExtensions
{
    /// <summary>
    /// 安全地将一个托管的数组指针临时包装为 NativeArray
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnsafeNativeArrayScope<T> AsUnsafeNativeArrayScope<T>(this T[] managedArray)
        where T : unmanaged
    {
        return new UnsafeNativeArrayScope<T>(managedArray);
    }

    /// <summary>
    /// 获取引用便于直接修改结构体数组中的内容
    /// </summary>
    /// <example>array.ElementRef(index).Data = new();</example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe static ref T ElementRef<T>(this NativeArray<T> array, int index)
        where T : unmanaged
    {
        return ref ((T*)array.GetUnsafePtr())[index];
    }
}

/// <summary>
/// 利用 C# 的 IDisposable 模式（using）来自动管理临时安全牌照的生命周期
/// </summary>
public unsafe ref struct UnsafeNativeArrayScope<T>
    where T : unmanaged
{
    public NativeArray<T> Array;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
    private AtomicSafetyHandle _safetyHandle;
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UnsafeNativeArrayScope(T[] managedArray)
    {
        fixed (void* ptr = managedArray)
        {
            Array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(ptr, managedArray.Length, Allocator.None);
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        _safetyHandle = AtomicSafetyHandle.Create();
        AtomicSafetyHandle.SetAllowSecondaryVersionWriting(_safetyHandle, false);
        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref Array, _safetyHandle);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Dispose()
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.Release(_safetyHandle);
#endif
    }
}