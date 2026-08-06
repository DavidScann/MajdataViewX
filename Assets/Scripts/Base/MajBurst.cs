using MajdataViewX.Managers;
using Unity.Burst;
using Unity.IL2CPP.CompilerServices;
using Unity.Mathematics;

namespace MajdataViewX.Base
{
    public struct MajBurstKey { }

    [Il2CppEagerStaticClassConstruction]
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


        public static ref Random GlobalRandom =>
            ref __DataSS.Data.GlobalRandom;
    }

    public struct MajBurstData
    {
        public TimeDataB TimeData;
        public InputDataB InputData;
        public MultTouchHandler MultTouchHandler;
        public Random GlobalRandom;
    }
}