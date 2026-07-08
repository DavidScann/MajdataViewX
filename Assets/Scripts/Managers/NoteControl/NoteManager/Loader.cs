using Cysharp.Threading.Tasks;
using MajSimai;
using Notes.SlideUtils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using WebSocketSharp;
using static MajCtx;

public partial class NoteManager
{
    public float NoteSpeed = 7f;
    public float TouchSpeed = 7.5f;
    public bool LegacySlideLayer = false;
    public bool SmoothSlideAnime = true;

    public double Ignore = 0f;

    private readonly int[] _buttonOrderIndex = new int[BUTTON_COUNT];
    private readonly int[] _sensorOrderIndex = new int[SENSOR_COUNT];

    private unsafe SlideArea* slideAreaPool;
    private unsafe SlidePose* slidePosePool;
    private int areaPoolIndex = 0;
    private int posePoolIndex = 0;
    private readonly List<SlideArea[]> loadedSlideAreaArrays = new();
    private readonly List<SlidePose[]> loadedSlidePoseArrays = new();

    private readonly List<NoteRegister>[] loadedTouches = new List<NoteRegister>[SENSOR_COUNT];

    public unsafe void Load(SimaiChart chart)
    {
        areaPoolIndex = 0;
        posePoolIndex = 0;
        Array.Fill(_buttonOrderIndex, 0);
        Array.Fill(_sensorOrderIndex, 0);
        if (slideAreaPool != null)
            UnsafeUtility.Free(slideAreaPool, Allocator.Persistent);
        if (slidePosePool != null)
            UnsafeUtility.Free(slidePosePool, Allocator.Persistent);
        for (var i = 0; i < SENSOR_COUNT; i++)
            if (loadedTouches[i] != null)
                loadedTouches[i].Clear();
            else
                loadedTouches[i] = new();

        foreach (var timing in chart.NoteTimings)
        {
            if (timing.Timing < Ignore)
                LoadIgnore(timing);
            else
                LoadTiming(timing);
        }

        slideAreaPool = (SlideArea*)UnsafeUtility.Malloc(
            areaPoolIndex * sizeof(SlideArea),
            16, Allocator.Persistent);
        slidePosePool = (SlidePose*)UnsafeUtility.Malloc(
            posePoolIndex * sizeof(SlidePose),
            16, Allocator.Persistent);

        for (var i = 0; i < slides.Length; i++)
        {
            var slide = slides[i];
            slide.judgeQueue = slideAreaPool + slide.judgeQueueOffset;
            slide.judgeQueueL = slideAreaPool + slide.judgeQueueLOffset;
            slide.judgeQueueR = slideAreaPool + slide.judgeQueueROffset;
            slide.slideArrows = slidePosePool + slide.slideArrowsOffset;
            //init过了，无奈出此下策
            if (slide.judgeQueueCount <= 3)
            {
                slide.judgeQueue[1].IsSkippable = false;
            }
            slides[i] = slide;
        }

        var cur1 = 0;
        foreach (var areas in loadedSlideAreaArrays)
        {
            fixed (SlideArea* src = areas)
            {
                UnsafeUtility.MemCpy(
                    slideAreaPool + cur1,
                    src, areas.Length * sizeof(SlideArea));
            }

            cur1 += areas.Length;
        }

        var cur2 = 0;
        foreach (var poses in loadedSlidePoseArrays)
        {
            fixed (SlidePose* src = poses)
            {
                UnsafeUtility.MemCpy(
                    slidePosePool + cur2,
                    src, poses.Length * sizeof(SlidePose));
            }

            cur2 += poses.Length;
        }

        loadedSlideAreaArrays.Clear();
        loadedSlidePoseArrays.Clear();

        MajBurst.MultTouchHandler.Load(loadedTouches);
    }

    private void LoadIgnore(in SimaiTimingPoint timing)
    {
        var holdLength = 0d;
        foreach (var note in timing.Notes)
        {
            if (note.HoldTime > holdLength)
                holdLength = note.HoldTime;

            if (note.SlideStartTime + note.SlideTime > Ignore)
            {
                LoadTiming(timing);
                return;
            }
        }

        if (timing.Timing + holdLength > Ignore)
        {
            LoadTiming(timing);
            return;
        }

        _objectCounter.CountIgnoreNoteCountAsync(timing.Notes).Forget();
    }

    private void CalcEach(in SimaiTimingPoint timing, out bool isNoteEach, out bool isSlideEach)
    {
        var noteCount = 0;
        var slideCount = 0;

        foreach (var o in timing.Notes)
        {
            if (!o.IsMine)
            {
                if (o.Type == SimaiNoteType.Slide)
                {
                    if (!o.IsSlideNoHead)
                        noteCount++;
                }
                else
                {
                    noteCount++;
                }
            }

            if (o.Type == SimaiNoteType.Slide && !o.IsMineSlide)
            {
                slideCount++;
            }
        }

        isNoteEach = noteCount > 1;
        isSlideEach = slideCount > 1;
    }

    private unsafe void LoadTiming(in SimaiTimingPoint timing)
    {
        try
        {
            CalcEach(timing, out var isNoteEach, out var isSlideEach);

            var nonMineCount = 0;
            var startPositions = stackalloc int[timing.Notes.Length];
            foreach (var note in timing.Notes)
            {
                switch (note.Type)
                {
                    case SimaiNoteType.Tap:
                        LoadTap(timing, note, isNoteEach);
                        if (!note.IsMine)
                            startPositions[nonMineCount++] = note.StartPosition;
                        break;
                    case SimaiNoteType.Hold:
                        LoadHold(timing, note, isNoteEach);
                        if (!note.IsMine)
                            startPositions[nonMineCount++] = note.StartPosition;
                        break;
                    case SimaiNoteType.Touch:
                        LoadTouch(timing, note, isNoteEach);
                        break;
                    case SimaiNoteType.TouchHold:
                        LoadTouchHold(timing, note, isNoteEach);
                        break;
                    case SimaiNoteType.Slide:
                        LoadSlideChain(timing, note, isNoteEach, isSlideEach);
                        if (!note.IsMine && !note.IsSlideNoHead)
                            startPositions[nonMineCount++] = note.StartPosition;
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
        catch (Exception) { throw; }
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

    private void LoadTap(in SimaiTimingPoint timing, in SimaiNote note, bool isEach)
    {
        var key = (SensorType)(note.StartPosition - 1);
        var tap = new TapData
        {
            Time = (float)timing.Timing,
            Key = key,
            Speed = NoteSpeed * timing.HSpeed,
            ButtonOrderIndex = _buttonOrderIndex[(int)key]++,
            SensorOrderIndex = _sensorOrderIndex[(int)key]++,

            IsStar = note.IsForceStar,
            IsDouble = false,
            RotateSpeed = note.IsFakeRotate ? -440f : 0,

            IsEach = isEach,
            IsEx = note.IsEx,
            IsBreak = note.IsBreak,
            IsMine = note.IsMine,
            UsingSV = note.UsingSV
        };
        tap.Init();
        taps.Add(tap);
    }

    private void LoadHold(in SimaiTimingPoint timing, in SimaiNote note, bool isEach)
    {
        var key = (SensorType)(note.StartPosition - 1);
        var hold = new HoldData
        {
            time = (float)timing.Timing,
            Key = key,
            speed = NoteSpeed * timing.HSpeed,
            LastFor = (float)note.HoldTime,
            ButtonOrderIndex = _buttonOrderIndex[(int)key]++,
            SensorOrderIndex = _sensorOrderIndex[(int)key]++,

            isEach = isEach,
            isEx = note.IsEx,
            isBreak = note.IsBreak,
            isMine = note.IsMine,
            usingSV = note.UsingSV
        };
        hold.Init();
        holds.Add(hold);
    }

    private void LoadTouch(in SimaiTimingPoint timing, in SimaiNote note, bool isEach)
    {
        var sensor = InputManager.GetSensor(note.TouchArea, note.StartPosition);
        var touch = new TouchData
        {
            time = (float)timing.Timing,
            sensor = sensor,
            speed = TouchSpeed * timing.HSpeed,
            sensorOrderIndex = _sensorOrderIndex[(int)sensor]++,

            isHanabi = note.IsHanabi,
            isEach = isEach,
            isEx = note.IsEx,
            isBreak = note.IsBreak,
            isMine = note.IsMine,
            usingSV = note.UsingSV
        };
        touch.Init();
        touches.Add(touch);
        loadedTouches[(int)sensor].Add(new()
        {
            IsEach = isEach,
            IsBreak = note.IsBreak,
            IsMine = note.IsMine
        });
    }

    private void LoadTouchHold(in SimaiTimingPoint timing, in SimaiNote note, bool isEach)
    {
        var sensor = InputManager.GetSensor(note.TouchArea, note.StartPosition);
        var th = new TouchHoldData
        {
            time = (float)timing.Timing,
            sensor = sensor,
            speed = TouchSpeed * timing.HSpeed,
            sensorOrderIndex = _sensorOrderIndex[(int)sensor]++,
            LastFor = (float)note.HoldTime,

            isHanabi = note.IsHanabi,
            isEach = isEach,
            isEx = note.IsEx,
            isBreak = note.IsBreak,
            isMine = note.IsMine,
            usingSV = note.UsingSV
        };
        th.Init();
        touchHolds.Add(th);
    }

    private void LoadSlideChain(in SimaiTimingPoint timing, in SimaiNote note, bool isNoteEach, bool isSlideEach)
    {
        var noteContent = note.RawContent;

        if (noteContent.Contains('w'))
        {
            var metadata = SlideTableNeo.GetWifiSlide(noteContent[0..3]);

            var judgeQueueCount = metadata.JudgeAreaQueue.Length;
            loadedSlideAreaArrays.Add(metadata.JudgeAreaQueue);
            var judgeQueueLCount = metadata.JudgeAreaQueueL.Length;
            loadedSlideAreaArrays.Add(metadata.JudgeAreaQueueL);
            var judgeQueueRCount = metadata.JudgeAreaQueueR.Length;
            loadedSlideAreaArrays.Add(metadata.JudgeAreaQueueR);
            var slideArrowsCount = metadata.ArrowPoses.Length;
            loadedSlidePoseArrays.Add(metadata.ArrowPoses);

            if (!note.IsSlideNoHead)
            {
                var starTap = new TapData
                {
                    Time = (float)timing.Timing,
                    Key = (SensorType)(note.StartPosition - 1),
                    Speed = NoteSpeed * timing.HSpeed,
                    ButtonOrderIndex = _buttonOrderIndex[note.StartPosition - 1]++,
                    SensorOrderIndex = _sensorOrderIndex[note.StartPosition - 1]++,
                    IsStar = true,
                    IsDouble = false,
                    RotateSpeed = 0,
                    IsEach = isNoteEach,
                    IsEx = note.IsEx,
                    IsBreak = note.IsSlideBreak,
                    IsMine = note.IsMineSlide,
                    UsingSV = note.UsingSV,
                };
                starTap.Init();
                taps.Add(starTap);
            }

            var slide = new SlideData
            {
                tapTime = (float)timing.Timing,
                time = (float)note.SlideStartTime,
                startPos = noteContent[0] - '0',
                endPos = noteContent[2] - '0',
                LastFor = (float)note.SlideTime,
                speed = NoteSpeed * timing.HSpeed,
                sensorOrderIndex = _sensorOrderIndex[note.StartPosition - 1],

                isWifi = true,

                judgeQueueOffset = areaPoolIndex,
                judgeQueueCount = judgeQueueCount,
                judgeQueueLOffset = areaPoolIndex + judgeQueueCount,
                judgeQueueLCount = judgeQueueLCount,
                judgeQueueROffset = areaPoolIndex + judgeQueueCount + judgeQueueLCount,
                judgeQueueRCount = judgeQueueRCount,
                Const = metadata.SlideConst,
                slideArrowsOffset = posePoolIndex,
                slideArrowsCount = slideArrowsCount,
                okType = metadata.OkType,
                okPose = metadata.OkPose,

                isEach = isSlideEach,
                isEx = note.IsEx,
                isBreak = note.IsBreak,
                isMine = note.IsMine,
                usingSV = note.UsingSV,
                smoothSlideAnime = SmoothSlideAnime,
                legacySlideLayer = LegacySlideLayer,
            };
            slide.Init();
            slides.Add(slide);

            areaPoolIndex += judgeQueueCount + judgeQueueLCount + judgeQueueRCount;
            posePoolIndex += slideArrowsCount;
        }
        else
        {
            var slideMetaDatas = GetSlidesFromRawContent(noteContent);
            var metadata = SlideTableNeo.MakeConnSlide(slideMetaDatas);

            var judgeQueueCount = metadata.JudgeAreaQueue.Length;
            loadedSlideAreaArrays.Add(metadata.JudgeAreaQueue);
            var slideArrowsCount = metadata.ArrowPoses.Length - 2;
            loadedSlidePoseArrays.Add(metadata.ArrowPoses[1..^1]);

            if (!note.IsSlideNoHead)
            {
                var starTapD = new TapData
                {
                    Time = (float)timing.Timing,
                    Key = (SensorType)(note.StartPosition - 1),
                    Speed = NoteSpeed * timing.HSpeed,
                    ButtonOrderIndex = _buttonOrderIndex[note.StartPosition - 1]++,
                    SensorOrderIndex = _sensorOrderIndex[note.StartPosition - 1]++,
                    IsStar = true,
                    IsDouble = false,
                    RotateSpeed = 0,
                    IsEach = isNoteEach,
                    IsEx = note.IsEx,
                    IsBreak = note.IsBreak,
                    IsMine = note.IsMine,
                    UsingSV = note.UsingSV,
                };
                starTapD.Init();
                taps.Add(starTapD);
            }

            //ignore start/end pos
            var slideData = new SlideData
            {
                tapTime = (float)timing.Timing,
                time = (float)note.SlideStartTime,
                LastFor = (float)note.SlideTime,
                speed = NoteSpeed * timing.HSpeed,
                sensorOrderIndex = _sensorOrderIndex[note.StartPosition - 1],

                judgeQueueOffset = areaPoolIndex,
                judgeQueueCount = judgeQueueCount,
                Const = metadata.SlideConst,
                slideArrowsOffset = posePoolIndex,
                slideArrowsCount = slideArrowsCount,
                okType = metadata.OkType,
                okPose = metadata.OkPose,

                isEach = isSlideEach,
                isEx = note.IsEx,
                isBreak = note.IsSlideBreak,
                isMine = note.IsMineSlide,
                usingSV = note.UsingSV,
                smoothSlideAnime = SmoothSlideAnime,
                legacySlideLayer = LegacySlideLayer,
            };
            slideData.Init();
            slides.Add(slideData);

            areaPoolIndex += judgeQueueCount;
            posePoolIndex += slideArrowsCount;
        }
    }

    // ============== Slide shape detection ==============
    public static string RemoveBracketContent(string s)
    {
        var sb = new StringBuilder(s.Length);
        int depth = 0;

        foreach (var c in s)
        {
            if (c == '[')
            {
                depth++;
                continue;
            }

            if (c == ']')
            {
                if (depth > 0)
                    depth--;
                continue;
            }

            if (depth == 0)
                sb.Append(c);
        }

        return sb.ToString();
    }

    private static IList<SlideMetadata> GetSlidesFromRawContent(ReadOnlySpan<char> rawContent)
    {
        var slideMetadatas = new List<SlideMetadata>(rawContent.Length / 2);

        int lastKey = -1;
        ReadOnlySpan<char> lastShape = string.Empty;
        bool isSlideCode = false;
        for (var i = 0; i < rawContent.Length; i++)
        {
            var c = rawContent[i];

            if (c is '[')
            {
                var endIdx = rawContent[i..].IndexOf(']');
                if (endIdx == -1) return slideMetadatas;

                i += endIdx;
                continue;
            }

            if (c is >= '0' and <= '8')
            {
                if (isSlideCode)
                {
                    var curKey = c - '0';
                    if (lastKey != -1 && lastShape != string.Empty)
                    {
                        var shape = $"{lastKey}{lastShape.ToString()}{curKey}";
                        slideMetadatas.Add(SlideTableNeo.MakeCustomSlide(shape));
                        lastShape = string.Empty;
                    }
                    lastKey = curKey;
                    isSlideCode = false;
                }
                else if (lastShape.Length == 1 && lastShape[0] == 'V')
                {
                    if (i + 1 >= rawContent.Length)
                        return slideMetadatas;
                    var VKey = c - '0';
                    var curKey = rawContent[i + 1] - '0';
                    i++;
                    if (lastKey != -1 && lastShape != string.Empty)
                    {
                        var shape = $"{lastKey}{lastShape.ToString()}{VKey}{curKey}";
                        slideMetadatas.Add(SlideTableNeo.GetStandardSlide(shape));
                        lastShape = string.Empty;
                    }
                    lastKey = curKey;
                }
                else
                {
                    var curKey = c - '0';
                    if (lastKey != -1 && !lastShape.IsEmpty)
                    {
                        if (lastShape.Length == 1 && lastShape[0] == '^')
                            lastShape = TranslateAutoSlide(lastKey, curKey);
                        var shape = $"{lastKey}{lastShape.ToString()}{curKey}";
                        slideMetadatas.Add(SlideTableNeo.GetStandardSlide(shape));
                        lastShape = string.Empty;
                    }
                    lastKey = curKey;
                }
            }
            else if (c is '>' or '<' or '^' or 'v' or '-' or 'V' or 's' or 'z')
            {
                lastShape = c.ToString();
            }
            else if (c is 'p' or 'q')
            {
                if (i + 1 < rawContent.Length && rawContent[i + 1] == c)
                {
                    lastShape = new string(c, 2);
                    i++;
                }
                else
                {
                    lastShape = c.ToString();
                }
            }
            else if (SlideCodeParser.CommandChars.Contains(c))
            {
                var endIdx = rawContent[i..].IndexOf('K');
                if (endIdx == -1)
                    return slideMetadatas;

                endIdx += i;
                lastShape = rawContent[i..(endIdx + 1)];
                isSlideCode = true;
                i = endIdx;
            }
        }

        return slideMetadatas;


        static string TranslateAutoSlide(int from, int to)
        {
            int cw = (to - from + 8) % 8;   // 顺时针距离
            int ccw = (from - to + 8) % 8;  // 逆时针距离

            if (from is 1 or 2 or 7 or 8)
            {
                if (cw < ccw)
                    return ">";
                else if (ccw < cw)
                    return "<";
            }
            else if (from is 3 or 4 or 5 or 6)
            {
                if (cw < ccw)
                    return "<";
                else if (ccw < cw)
                    return ">";
            }

            throw new Exception("CNM");
        }
    }
}