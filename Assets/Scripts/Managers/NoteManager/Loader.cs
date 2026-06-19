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
                    {
                        var tapLine = new TapLineData();
                        var tapEx = new TapExData();
                        var tap = new TapData
                        {
                            time = (float)timing.Timing,
                            key = (SensorType)(note.StartPosition - 1),
                            speed = NoteSpeed * timing.HSpeed,

                            tapLine = tapLine,
                            tapEx = tapEx,

                            isStar = note.IsForceStar,
                            isDouble = false,
                            rotateSpeed = note.IsFakeRotate ? -440f : 0,

                            isEach = timing.Notes.Length > 1,
                            isEx = note.IsEx,
                            isBreak = note.IsBreak,
                            isMine = note.IsMine,
                            usingSV = note.UsingSV
                        };
                        tap.Init();

                        taps.Add(tap);
                    }
                    break;
            }
        }
    }
}