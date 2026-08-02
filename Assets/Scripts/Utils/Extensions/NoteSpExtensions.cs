using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using static SkinManager;

public static class NoteSpExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NoteSp Offset(this NoteSp value, int offset)
    {
        return (NoteSp)((uint)value + offset);
    }
}
