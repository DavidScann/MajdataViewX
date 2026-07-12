using MajSimai;
using UnityEngine;
using static MajCtx;

public partial class NoteManager
{
    public float NoteSpeed = 7f;
    public float TouchSpeed = 7.5f;



    public void Load(SimaiChart chart)
    {
        foreach (var timing in chart.NoteTimings)
            LoadTiming(timing);
    }

    private void LoadTiming(SimaiTimingPoint timing)
    {
        foreach (var note in timing.Notes)
        {
            switch (note.Type)
            {
                case SimaiNoteType.Tap:
                    break;
            }
        }
    }
}