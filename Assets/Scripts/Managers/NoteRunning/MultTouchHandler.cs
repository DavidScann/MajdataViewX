using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.ComTypes;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using static SkinManager;
#nullable enable

[BurstCompile]
public struct MultTouchHandler
{
    private NativeArray<NoteRegisterSpan> _spans;
    private NativeArray<NoteRegister> _registers;
    private NativeArray<int> _activeCounts;

    public void Init()
    {
        _spans = new(MajCtx.SENSOR_COUNT, Allocator.Persistent);
        _activeCounts = new(MajCtx.SENSOR_COUNT, Allocator.Persistent);
    }

    public void Load(List<NoteRegister>[] registers)
    {
        if (_registers.IsCreated) _registers.Dispose();

        var count = 0;
        for (var s = 0; s < MajCtx.SENSOR_COUNT; s++)
        {
            var newCount = count + registers[s].Count;
            _spans[s] = new()
            {
                Current = count,
                Count = newCount
            };
            count = newCount;
        }
        _registers = new(count, Allocator.Persistent);

        var i = 0;
        foreach (var list in registers)
            foreach (var r in list)
            {
                _registers[i] = r;
                i++;
            }
    }

    public void Clear()
    {
        for (var i = 0; i < MajCtx.SENSOR_COUNT; i++)
        {
            _spans[i] = default;
            _activeCounts[i] = 0;
        }
        if (_registers.IsCreated) _registers.Dispose();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RegisterActive(SensorType area)
    {
        System.Threading.Interlocked.Increment(ref _activeCounts.ElementRef((int)area));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UnregisterActive(SensorType area)
    {
        System.Threading.Interlocked.Decrement(ref _activeCounts.ElementRef((int)area));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Unregister(SensorType area)
    {
        System.Threading.Interlocked.Increment(ref _spans.ElementRef((int)area).Current);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool CanShowBorder(SensorType area, out bool isThree, out NoteSp sprite)
    {
        ref readonly var span = ref _spans.ElementRef((int)area);
        var diff = _activeCounts[(int)area];
        if (diff <= 1)
        {
            isThree = false;
            sprite = default;
            return false;
        }
        else if (diff == 2)
        {
            isThree = false;
            sprite = GetSpriteId(_registers[span.Current + 1], false);
            return true;
        }
        else
        {
            isThree = true;
            sprite = GetSpriteId(_registers[span.Current + 2], true);
            return true;
        }

        static NoteSp GetSpriteId(in NoteRegister reg, bool isThree)
        {
            if (reg.IsMine)
            {
                if (reg.IsBreak)
                {
                    return !isThree ? NoteSp.TOUCH_BORDER_BREAK_MINE_0 : NoteSp.TOUCH_BORDER_BREAK_MINE_1;
                }
                else
                {
                    return !isThree ? NoteSp.TOUCH_BORDER_MINE_0 : NoteSp.TOUCH_BORDER_MINE_1;
                }
            }
            if (reg.IsBreak)
            {
                return !isThree ? NoteSp.TOUCH_BORDER_BREAK_0 : NoteSp.TOUCH_BORDER_BREAK_1;
            }
            if (reg.IsEach)
            {
                return !isThree ? NoteSp.TOUCH_BORDER_EACH_0 : NoteSp.TOUCH_BORDER_EACH_1;
            }
            return !isThree ? NoteSp.TOUCH_BORDER_0 : NoteSp.TOUCH_BORDER_1;
        }
    }

    public void Dispose()
    {
        if (_registers.IsCreated) _registers.Dispose();
        if (_spans.IsCreated) _spans.Dispose();
        if (_activeCounts.IsCreated) _activeCounts.Dispose();
    }



    struct NoteRegisterSpan
    {
        public int Current;
        public int Count;
    }
}
public struct NoteRegister
{
    public bool IsEach { get; set; }
    public bool IsBreak { get; set; }
    public bool IsMine { get; set; }
}