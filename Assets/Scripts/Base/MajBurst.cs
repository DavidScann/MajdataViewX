
using System;
using Unity.Burst;
using UnityEngine;

public struct MajBurstKey { }

public static class MajBurst
{
    public static readonly SharedStatic<MajBurstData> __DataSS =
        SharedStatic<MajBurstData>.GetOrCreate<MajBurstKey>();

    public static ref TimeDataB TimeData =>
        ref __DataSS.Data.TimeData;
    public static ref InputDataB InputData =>
        ref __DataSS.Data.InputData;
    public static ref MultTouchHandler MultTouchHandler =>
        ref __DataSS.Data.MultTouchHandler;
}

public struct MajBurstData
{
    public TimeDataB TimeData;
    public InputDataB InputData;
    public MultTouchHandler MultTouchHandler;
}