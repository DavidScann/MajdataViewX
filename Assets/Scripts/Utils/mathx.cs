using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Unity.Mathematics;

namespace MajdataViewX.Utils
{
    // TODO: extensions...
    [Unity.IL2CPP.CompilerServices.Il2CppEagerStaticClassConstruction]
    public static class mathx
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float cross(float2 x, float2 y)
        {
            return x.x * y.y - x.y * y.x;
        }
    }
}
