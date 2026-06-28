using System;
using System.Threading.Tasks;
using MajSimai;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using static MajCtx;

public partial class NoteManager
{
    public float NoteSpeed = 7f;
    public float TouchSpeed = 7.5f;
    public bool legacySlideLayer = false;
    public bool smoothSlideAnime = true;

    private int[] _noteOrderIndex = new int[34];

    public void Load(SimaiChart chart)
    {
        for (int i = 0; i < 34; i++) _noteOrderIndex[i] = 0;
        foreach (var timing in chart.NoteTimings)
            LoadTiming(timing);
    }

    private unsafe void LoadTiming(SimaiTimingPoint timing)
    {
        try
        {
            var nonMineCount = 0;
            var startPositions = stackalloc int[timing.Notes.Length];

            foreach (var note in timing.Notes)
            {
                switch (note.Type)
                {
                    case SimaiNoteType.Tap:
                        LoadTap(timing, note);
                        if (!note.IsMine) startPositions[nonMineCount++] = note.StartPosition;
                        break;
                    case SimaiNoteType.Hold:
                        LoadHold(timing, note);
                        if (!note.IsMine) startPositions[nonMineCount++] = note.StartPosition;
                        break;
                    case SimaiNoteType.Touch:
                        LoadTouch(timing, note);
                        break;
                    case SimaiNoteType.TouchHold:
                        LoadTouchHold(timing, note);
                        break;
                    case SimaiNoteType.Slide:
                        LoadSlideChain(timing, note);
                        break;
                }
            }

            if (nonMineCount > 1)
            {
                for (int i = 0; i < nonMineCount - 1; i++)
                {
                    var s = (float)timing.Timing;
                    var spd = NoteSpeed * timing.HSpeed;
                    CreateEachLine(s, startPositions[i], startPositions[i + 1], spd);
                }
            }
        }
        catch (Exception ex)
        {

        }
    }

    private void CreateEachLine(float time, int startPosA, int startPosB, float speed)
    {
        var startPos = startPosA;
        var endPos = startPosB;
        endPos -= startPos;
        if (endPos == 0) return;
        endPos = endPos < 0 ? endPos + 8 : endPos;
        endPos = endPos > 8 ? endPos - 8 : endPos;
        endPos++;

        if (endPos > 4)
        {
            startPos = startPosB;
            endPos = startPosA - startPosB;
            endPos = endPos < 0 ? endPos + 8 : endPos;
            endPos = endPos > 8 ? endPos - 8 : endPos;
            endPos++;
        }

        var el = new EachLineData
        {
            time = time,
            key = startPos - 1,
            curvLength = endPos - 1,
            speed = speed,
        };
        el.Init();
        eachLines.Add(el);
    }

    private void LoadTap(SimaiTimingPoint timing, SimaiNote note)
    {
        var key = (SensorType)(note.StartPosition - 1);
        var tap = new TapData
        {
            time = (float)timing.Timing,
            key = key,
            speed = NoteSpeed * timing.HSpeed,
            sensorOrderIndex = _noteOrderIndex[(int)key]++,

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

    private void LoadHold(SimaiTimingPoint timing, SimaiNote note)
    {
        var key = (SensorType)(note.StartPosition - 1);
        var hold = new HoldData
        {
            time = (float)timing.Timing,
            key = key,
            speed = NoteSpeed * timing.HSpeed,
            LastFor = (float)note.HoldTime,
            sensorOrderIndex = _noteOrderIndex[(int)key]++,

            isEach = timing.Notes.Length > 1,
            isEx = note.IsEx,
            isBreak = note.IsBreak,
            isMine = note.IsMine,
            usingSV = note.UsingSV
        };
        hold.Init();
        holds.Add(hold);
    }

    private void LoadTouch(SimaiTimingPoint timing, SimaiNote note)
    {
        var sensor = InputManager.GetSensor(note.TouchArea, note.StartPosition);
        var touch = new TouchData
        {
            time = (float)timing.Timing,
            sensor = sensor,
            speed = TouchSpeed * timing.HSpeed,
            sensorOrderIndex = _noteOrderIndex[(int)sensor]++,

            isHanabi = note.IsHanabi,
            isEach = timing.Notes.Length > 1,
            isEx = note.IsEx,
            isBreak = note.IsBreak,
            isMine = note.IsMine,
        };
        touch.Init();
        touches.Add(touch);
    }

    private void LoadTouchHold(SimaiTimingPoint timing, SimaiNote note)
    {
        var sensor = InputManager.GetSensor(note.TouchArea, note.StartPosition);
        var th = new TouchHoldData
        {
            time = (float)timing.Timing,
            sensor = sensor,
            speed = TouchSpeed * timing.HSpeed,
            sensorOrderIndex = _noteOrderIndex[(int)sensor]++,
            LastFor = (float)note.HoldTime,

            isHanabi = note.IsHanabi,
            isEach = timing.Notes.Length > 1,
            isEx = note.IsEx,
            isBreak = note.IsBreak,
            isMine = note.IsMine,
        };
        th.Init();
        touchHolds.Add(th);
    }

    private void LoadSlideChain(SimaiTimingPoint timing, SimaiNote note)
    {
        var noteContent = note.RawContent;
        if (string.IsNullOrEmpty(noteContent)) return;

        if (noteContent.Contains('w'))
        {
            var (metadata, judgeQueueL, judgeQueueC, judgeQueueR, slidePoses, okPose) =
                SlideTables.GetWifiTable();

            var isJustR = DetectJustType(noteContent, out var endPos);

            var starTap = new TapData
            {
                time = (float)timing.Timing,
                key = (SensorType)(note.StartPosition - 1),
                speed = NoteSpeed * timing.HSpeed,
                sensorOrderIndex = _noteOrderIndex[note.StartPosition]++,
                isStar = true,
                isDouble = false,
                rotateSpeed = 0,
                isEach = timing.Notes.Length > 1,
                isEx = note.IsEx,
                isBreak = note.IsSlideBreak,
                isMine = note.IsMineSlide,
                usingSV = note.UsingSV,
            };
            starTap.Init();
            taps.Add(starTap);

            unsafe
            {
                var slide = new SlideData
                {
                    tapTime = (float)timing.Timing,
                    time = (float)note.SlideStartTime,
                    LastFor = (float)note.SlideTime,
                    startPosition = note.StartPosition,
                    endPosition = endPos,
                    speed = NoteSpeed * timing.HSpeed,
                    sensorOrderIndex = _noteOrderIndex[note.StartPosition]++,

                    isWifi = true,
                    isJustR = isJustR,

                    metadata = metadata,
                    judgeQueue = (SlideArea*)judgeQueueL.AsUnsafeNativeArrayScope().Array.GetUnsafePtr(),
                    judgeQueueCount = judgeQueueL.Length,
                    judgeQueueC = (SlideArea*)judgeQueueC.AsUnsafeNativeArrayScope().Array.GetUnsafePtr(),
                    judgeQueueCCount = judgeQueueC.Length,
                    judgeQueueR = (SlideArea*)judgeQueueR.AsUnsafeNativeArrayScope().Array.GetUnsafePtr(),
                    judgeQueueRCount = judgeQueueR.Length,
                    slideArrows = (SlidePose*)slidePoses.AsUnsafeNativeArrayScope().Array.GetUnsafePtr(),
                    slideArrowsCount = slidePoses.Length,
                    okPose = okPose,

                    isEach = timing.Notes.Length > 1,
                    isEx = note.IsEx,
                    isBreak = note.IsSlideBreak,
                    isMine = note.IsMineSlide,
                    usingSV = note.UsingSV,
                    smoothSlideAnime = smoothSlideAnime,
                    legacySlideLayer = legacySlideLayer,
                };
                slide.Init();
                slides.Add(slide);
            }
        }
        else
        {
            var slideShape = DetectShapeFromText(noteContent);
            if (string.IsNullOrEmpty(slideShape)) return;
            var isMirror = false;
            if (slideShape.StartsWith("-"))
            {
                isMirror = true;
                slideShape = slideShape.Substring(1);
            }

            var (metadata, judgeQueue, slidePoses, okPose) =
                SlideTables.GetSlideTableByName(slideShape);

            var justR = DetectJustType(noteContent, out var ePos);

            // Per legacy InstantiateSlide(): a star Tap is always created at the slide head.
            var starTapD = new TapData
            {
                time = (float)timing.Timing,
                key = (SensorType)(note.StartPosition - 1),
                speed = NoteSpeed * timing.HSpeed,
                sensorOrderIndex = _noteOrderIndex[note.StartPosition]++,
                isStar = true,
                isDouble = false,
                rotateSpeed = 0,
                isEach = timing.Notes.Length > 1,
                isEx = note.IsEx,
                isBreak = note.IsSlideBreak,
                isMine = note.IsMineSlide,
                usingSV = note.UsingSV,
            };
            starTapD.Init();
            taps.Add(starTapD);

            unsafe
            {
                var slideData = new SlideData
                {
                    tapTime = (float)timing.Timing,
                    time = (float)note.SlideStartTime,
                    LastFor = (float)note.SlideTime,
                    startPosition = note.StartPosition,
                    endPosition = ePos,
                    speed = NoteSpeed * timing.HSpeed,
                    sensorOrderIndex = _noteOrderIndex[note.StartPosition]++,

                    isMirror = isMirror,
                    isJustR = justR,

                    metadata = metadata,
                    judgeQueue = (SlideArea*)judgeQueue.AsUnsafeNativeArrayScope().Array.GetUnsafePtr(),
                    judgeQueueCount = judgeQueue.Length,
                    slideArrows = (SlidePose*)slidePoses.AsUnsafeNativeArrayScope().Array.GetUnsafePtr(),
                    slideArrowsCount = slidePoses.Length,
                    okPose = okPose,

                    isEach = timing.Notes.Length > 1,
                    isEx = note.IsEx,
                    isBreak = note.IsSlideBreak,
                    isMine = note.IsMineSlide,
                    usingSV = note.UsingSV,
                    smoothSlideAnime = smoothSlideAnime,
                    legacySlideLayer = legacySlideLayer,
                };
                slideData.Init();
                slides.Add(slideData);
            }
        }
    }

    // ============== Slide shape detection ==============

    private static string DetectShapeFromText(string content)
    {
        int getRelativeEndPos(int startPos, int endPos)
        {
            endPos = endPos - startPos;
            endPos = endPos < 0 ? endPos + 8 : endPos;
            endPos = endPos > 8 ? endPos - 8 : endPos;
            return endPos + 1;
        }

        if (content.Contains('-'))
        {
            var str = content.Substring(0, 3);
            var digits = str.Split('-');
            if (digits.Length < 2) return "";
            var startPos = int.Parse(digits[0]);
            var endPos = int.Parse(digits[1]);
            endPos = getRelativeEndPos(startPos, endPos);
            if (endPos < 3 || endPos > 7) return "";
            return "line" + endPos;
        }
        if (content.Contains('>'))
        {
            var str = content.Substring(0, 3);
            var digits = str.Split('>');
            if (digits.Length < 2) return "";
            var startPos = int.Parse(digits[0]);
            var endPos = int.Parse(digits[1]);
            endPos = getRelativeEndPos(startPos, endPos);
            if (IsUpperHalf(startPos)) return "circle" + endPos;
            endPos = MirrorKeys(endPos);
            return "-circle" + endPos;
        }
        if (content.Contains('<'))
        {
            var str = content.Substring(0, 3);
            var digits = str.Split('<');
            if (digits.Length < 2) return "";
            var startPos = int.Parse(digits[0]);
            var endPos = int.Parse(digits[1]);
            endPos = getRelativeEndPos(startPos, endPos);
            if (!IsUpperHalf(startPos)) return "circle" + endPos;
            endPos = MirrorKeys(endPos);
            return "-circle" + endPos;
        }
        if (content.Contains('^'))
        {
            var str = content.Substring(0, 3);
            var digits = str.Split('^');
            if (digits.Length < 2) return "";
            var startPos = int.Parse(digits[0]);
            var endPos = int.Parse(digits[1]);
            endPos = getRelativeEndPos(startPos, endPos);
            if (endPos == 1 || endPos == 5) return "";
            if (endPos < 5) return "circle" + endPos;
            if (endPos > 5) return "-circle" + MirrorKeys(endPos);
        }
        if (content.Contains('v'))
        {
            var str = content.Substring(0, 3);
            var digits = str.Split('v');
            if (digits.Length < 2) return "";
            var startPos = int.Parse(digits[0]);
            var endPos = int.Parse(digits[1]);
            endPos = getRelativeEndPos(startPos, endPos);
            if (endPos == 5) return "";
            return "v" + endPos;
        }
        if (content.Contains("pp"))
        {
            var str = content.Substring(0, 4);
            var digits = str.Split('p');
            if (digits.Length < 3) return "";
            var startPos = int.Parse(digits[0]);
            var endPos = int.Parse(digits[2]);
            endPos = getRelativeEndPos(startPos, endPos);
            return "ppqq" + endPos;
        }
        if (content.Contains("qq"))
        {
            var str = content.Substring(0, 4);
            var digits = str.Split('q');
            if (digits.Length < 3) return "";
            var startPos = int.Parse(digits[0]);
            var endPos = int.Parse(digits[2]);
            endPos = getRelativeEndPos(startPos, endPos);
            endPos = MirrorKeys(endPos);
            return "-ppqq" + endPos;
        }
        if (content.Contains('p'))
        {
            var str = content.Substring(0, 3);
            var digits = str.Split('p');
            if (digits.Length < 2) return "";
            var startPos = int.Parse(digits[0]);
            var endPos = int.Parse(digits[1]);
            endPos = getRelativeEndPos(startPos, endPos);
            return "pq" + endPos;
        }
        if (content.Contains('q'))
        {
            var str = content.Substring(0, 3);
            var digits = str.Split('q');
            if (digits.Length < 2) return "";
            var startPos = int.Parse(digits[0]);
            var endPos = int.Parse(digits[1]);
            endPos = getRelativeEndPos(startPos, endPos);
            endPos = MirrorKeys(endPos);
            return "-pq" + endPos;
        }
        if (content.Contains('s'))
        {
            var str = content.Substring(0, 3);
            var digits = str.Split('s');
            if (digits.Length < 2) return "";
            var startPos = int.Parse(digits[0]);
            var endPos = int.Parse(digits[1]);
            endPos = getRelativeEndPos(startPos, endPos);
            if (endPos != 5) return "";
            return "s";
        }
        if (content.Contains('z'))
        {
            var str = content.Substring(0, 3);
            var digits = str.Split('z');
            if (digits.Length < 2) return "";
            var startPos = int.Parse(digits[0]);
            var endPos = int.Parse(digits[1]);
            endPos = getRelativeEndPos(startPos, endPos);
            if (endPos != 5) return "";
            return "-s";
        }
        if (content.Contains('V'))
        {
            var str = content.Substring(0, 4);
            var digits = str.Split('V');
            if (digits.Length < 2) return "";
            var startPos = int.Parse(digits[0]);
            var turnPos = int.Parse(digits[1][0].ToString());
            var endPos = int.Parse(digits[1][1].ToString());
            turnPos = getRelativeEndPos(startPos, turnPos);
            endPos = getRelativeEndPos(startPos, endPos);
            if (turnPos == 7)
            {
                if (endPos < 2 || endPos > 5) return "";
                return "L" + endPos;
            }
            if (turnPos == 3)
            {
                if (endPos < 5) return "";
                return "-L" + MirrorKeys(endPos);
            }
            return "";
        }
        return "";
    }

    private static bool DetectJustType(string content, out int endPos)
    {
        if (content.Contains('>'))
        {
            var str = content.Substring(0, 3);
            var digits = str.Split('>');
            var startPos = int.Parse(digits[0]);
            endPos = int.Parse(digits[1]);
            return IsUpperHalf(startPos);
        }
        if (content.Contains('<'))
        {
            var str = content.Substring(0, 3);
            var digits = str.Split('<');
            var startPos = int.Parse(digits[0]);
            endPos = int.Parse(digits[1]);
            return !IsUpperHalf(startPos);
        }
        if (content.Contains('^'))
        {
            var str = content.Substring(0, 3);
            var digits = str.Split('^');
            var startPos = int.Parse(digits[0]);
            endPos = int.Parse(digits[1]);
            endPos = endPos - startPos;
            endPos = endPos < 0 ? endPos + 8 : endPos;
            endPos = endPos > 8 ? endPos - 8 : endPos;
            if (endPos < 4) { endPos = int.Parse(digits[1]); return true; }
            if (endPos > 4) { endPos = int.Parse(digits[1]); return false; }
        }
        else if (content.Contains('V'))
        {
            var str = content.Substring(0, 4);
            var digits = str.Split('V');
            endPos = int.Parse(digits[1][1].ToString());
            return IsRightHalf(endPos);
        }
        else if (content.Contains('w'))
        {
            var str = content.Substring(0, 3);
            endPos = int.Parse(str.Substring(2, 1));
            return IsUpperHalf(endPos);
        }
        else
        {
            if (content.Contains("qq") || content.Contains("pp"))
                endPos = int.Parse(content.Substring(3, 1));
            else
                endPos = int.Parse(content.Substring(2, 1));
            return IsRightHalf(endPos);
        }
        endPos = 0;
        return true;
    }

    private static bool IsUpperHalf(int key) { return key == 7 || key == 8 || key == 1 || key == 2; }
    private static bool IsRightHalf(int key) { return key >= 1 && key <= 4; }
    private static int MirrorKeys(int key)
    {
        return key switch
        {
            1 => 1,
            2 => 8,
            3 => 7,
            4 => 6,
            5 => 5,
            6 => 4,
            7 => 3,
            8 => 2,
            _ => 1,
        };
    }
}
