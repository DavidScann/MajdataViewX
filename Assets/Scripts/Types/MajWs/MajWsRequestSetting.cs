using MajdataViewX.Types.MajSetting;

namespace MajdataViewX.Types.MajWs
{
    internal readonly struct MajWsRequestSetting
    {
        public MajViewSetting ViewSetting { get; init; }
        public MajVolumeSetting VolumeSetting { get; init; }
    }
}