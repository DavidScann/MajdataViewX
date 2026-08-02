
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

public static class NativeListExtensions
{
    /// <summary>
    /// 获取引用便于直接修改结构体list中的内容
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe static ref T ElementRef<T>(this NativeList<T> list, int index)
        where T : unmanaged
    {
        return ref list.GetUnsafePtr()[index];
    }
}