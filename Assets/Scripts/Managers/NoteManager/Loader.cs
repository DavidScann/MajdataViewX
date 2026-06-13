using MajSimai;
using UnityEngine;
using static MajCtx;

public partial class NoteManager
{
    public float NoteSpeed = 7f;
    public float TouchSpeed = 7.5f;

    private int _noteSortOrder;


    private static int GetNoteLayerCount(SimaiNoteType type) => type switch
    {
        SimaiNoteType.Tap => 2,
        SimaiNoteType.Hold => 3,
        SimaiNoteType.Slide => 2,
        SimaiNoteType.Touch => 7,
        SimaiNoteType.TouchHold => 6,
        _ => 0
    };




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

                            sort = _noteSortOrder,

                            tapLine = tapLine,
                            tapEx = tapEx,

                            isStar = note.IsForceStar,
                            rotateSpeed = note.IsFakeRotate ? -440f : 0,

                            isEach = timing.Notes.Length > 1,
                            isEx = note.IsEx,
                            isBreak = note.IsBreak,
                            isMine = note.IsMine,
                            usingSV = note.UsingSV
                        };
                        tap.Init();

                        taps.Add(tap);
                        _noteSortOrder -= GetNoteLayerCount(note.Type);
                    }
                    break;
            }
        }
    }
}