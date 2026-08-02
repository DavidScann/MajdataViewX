using Unity.Jobs;
using Unity.IL2CPP.CompilerServices;

[assembly: Il2CppSetOption(Option.NullChecks, false)]
[assembly: Il2CppSetOption(Option.ArrayBoundsChecks, false)]
[assembly: Il2CppSetOption(Option.DivideByZeroChecks, false)]


[assembly: RegisterGenericJobType(typeof(RadixSort.RadixSortJob<LineRenderData>))]
[assembly: RegisterGenericJobType(typeof(RadixSort.RadixSortJob<NotesRenderData>))]
[assembly: RegisterGenericJobType(typeof(RadixSort.RadixSortJob<MaskRenderData>))]
[assembly: RegisterGenericJobType(typeof(RadixSort.RadixSortJob<SimpleRenderData>))]
[assembly: RegisterGenericJobType(typeof(RadixSort.RadixSortJob<HitRenderData>))]