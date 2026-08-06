using MajdataViewX.Types.Enums;

namespace MajdataViewX.Types.MajWs
{
    public readonly struct ViewSummary
    {
        public ViewStatus State { get; init; }
        public string ErrMsg { get; init; }
        public float Timeline { get; init; }
    }
}