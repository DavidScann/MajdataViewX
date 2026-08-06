using System.Runtime.CompilerServices;
using static MajdataViewX.Managers.SkinManager;

namespace MajdataViewX.Utils.Extensions
{
    public static class NoteSpExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NoteSp Offset(this NoteSp value, int offset)
        {
            return (NoteSp)((uint)value + offset);
        }
    }
}