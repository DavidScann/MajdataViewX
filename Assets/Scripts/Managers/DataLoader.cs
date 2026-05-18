#region

using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using MajSimai;
using UnityEngine;
using UnityEngine.UI;

#endregion

public class DataLoader : MonoBehaviour
{
    private SkinManager skinManager;
    private ObjectCounter objectCounter;
    NoteManager noteManager;

    public float noteSpeed = 7f;
    public float touchSpeed = 7.5f;
    public bool smoothSlideAnime = false;


    //SerializeField
    public GameObject tapPrefab;
    public GameObject holdPrefab;
    public GameObject starPrefab;
    public GameObject touchHoldPrefab;
    public GameObject touchPrefab;
    public GameObject eachLine;
    public GameObject tapLine;
    // public GameObject starLine;
    // public GameObject mineLine;
    public GameObject notes;
    public GameObject star_slidePrefab;

    public GameObject[] slidePrefab;

    Majson loadedData = null;
    Dictionary<int, int> noteIndex = new();
    Dictionary<SensorType, int> touchIndex = new();
    private bool streamingRunning;
    List<TouchDrop> touchMembers = new();

    public Text diffText;
    public Text levelText;
    public Text titleText;
    public Text artistText;
    public Text designText;
    public RawImage cardImage;
    public Color[] diffColors = new Color[7];

    private const double StreamingCreatePreloadTime = 10;
    private const double StreamingActivePreloadTime = 5;
    private const double StreamingFrameBudgetMs = 4;

    private int slideLayer = -1;
    private int noteSortOrder = 0;

    private static readonly Dictionary<SimaiNoteType, int> NOTE_LAYER_COUNT = new Dictionary<SimaiNoteType, int>()
    {
        {SimaiNoteType.Tap, 2 },
        {SimaiNoteType.Hold, 3 },
        {SimaiNoteType.Slide, 2 },
        {SimaiNoteType.Touch, 7 },
        {SimaiNoteType.TouchHold, 6 },
    };
    private static readonly Dictionary<string, int> SLIDE_PREFAB_MAP = new Dictionary<string, int>()
    {
        {"line3", 0 },
        {"line4", 1 },
        {"line5", 2 },
        {"line6", 3 },
        {"line7", 4 },
        {"circle1", 5 },
        {"circle2", 6 },
        {"circle3", 7 },
        {"circle4", 8 },
        {"circle5", 9 },
        {"circle6", 10 },
        {"circle7", 11 },
        {"circle8", 12 },
        {"v1", 41 },
        {"v2", 13 },
        {"v3", 14 },
        {"v4", 15 },
        {"v6", 16 },
        {"v7", 17 },
        {"v8", 18 },
        {"ppqq1", 19 },
        {"ppqq2", 20 },
        {"ppqq3", 21 },
        {"ppqq4", 22 },
        {"ppqq5", 23 },
        {"ppqq6", 24 },
        {"ppqq7", 25 },
        {"ppqq8", 26 },
        {"pq1", 27 },
        {"pq2", 28 },
        {"pq3", 29 },
        {"pq4", 30 },
        {"pq5", 31 },
        {"pq6", 32 },
        {"pq7", 33 },
        {"pq8", 34 },
        {"s", 35 },
        {"wifi", 36 },
        {"L2", 37 },
        {"L3", 38 },
        {"L4", 39 },
        {"L5", 40 },
    };

    static readonly Dictionary<SensorType, SensorType[]> TOUCH_GROUPS = new()
    {
        { SensorType.A1, new SensorType[]{ SensorType.D1, SensorType.D2, SensorType.E1, SensorType.E2 } },
        { SensorType.A2, new SensorType[]{ SensorType.D2, SensorType.D3, SensorType.E2, SensorType.E3 } },
        { SensorType.A3, new SensorType[]{ SensorType.D3, SensorType.D4, SensorType.E3, SensorType.E4 } },
        { SensorType.A4, new SensorType[]{ SensorType.D4, SensorType.D5, SensorType.E4, SensorType.E5 } },
        { SensorType.A5, new SensorType[]{ SensorType.D5, SensorType.D6, SensorType.E5, SensorType.E6 } },
        { SensorType.A6, new SensorType[]{ SensorType.D6, SensorType.D7, SensorType.E6, SensorType.E7 } },
        { SensorType.A7, new SensorType[]{ SensorType.D7, SensorType.D8, SensorType.E7, SensorType.E8 } },
        { SensorType.A8, new SensorType[]{ SensorType.D8, SensorType.D1, SensorType.E8, SensorType.E1 } },

        { SensorType.D1, new SensorType[]{ SensorType.A1, SensorType.A8, SensorType.E1 } },
        { SensorType.D2, new SensorType[]{ SensorType.A2, SensorType.A1, SensorType.E2 } },
        { SensorType.D3, new SensorType[]{ SensorType.A3, SensorType.A2, SensorType.E3 } },
        { SensorType.D4, new SensorType[]{ SensorType.A4, SensorType.A3, SensorType.E4 } },
        { SensorType.D5, new SensorType[]{ SensorType.A5, SensorType.A4, SensorType.E5 } },
        { SensorType.D6, new SensorType[]{ SensorType.A6, SensorType.A5, SensorType.E6 } },
        { SensorType.D7, new SensorType[]{ SensorType.A7, SensorType.A6, SensorType.E7 } },
        { SensorType.D8, new SensorType[]{ SensorType.A8, SensorType.A7, SensorType.E8 } },

        { SensorType.E1, new SensorType[]{ SensorType.D1, SensorType.A1, SensorType.A8, SensorType.B1, SensorType.B8 } },
        { SensorType.E2, new SensorType[]{ SensorType.D2, SensorType.A2, SensorType.A1, SensorType.B2, SensorType.B1 } },
        { SensorType.E3, new SensorType[]{ SensorType.D3, SensorType.A3, SensorType.A2, SensorType.B3, SensorType.B2 } },
        { SensorType.E4, new SensorType[]{ SensorType.D4, SensorType.A4, SensorType.A3, SensorType.B4, SensorType.B3 } },
        { SensorType.E5, new SensorType[]{ SensorType.D5, SensorType.A5, SensorType.A4, SensorType.B5, SensorType.B4 } },
        { SensorType.E6, new SensorType[]{ SensorType.D6, SensorType.A6, SensorType.A5, SensorType.B6, SensorType.B5 } },
        { SensorType.E7, new SensorType[]{ SensorType.D7, SensorType.A7, SensorType.A6, SensorType.B7, SensorType.B6 } },
        { SensorType.E8, new SensorType[]{ SensorType.D8, SensorType.A8, SensorType.A7, SensorType.B8, SensorType.B7 } },

        { SensorType.B1, new SensorType[]{ SensorType.E1, SensorType.E2, SensorType.B8, SensorType.B2, SensorType.A1, SensorType.C } },
        { SensorType.B2, new SensorType[]{ SensorType.E2, SensorType.E3, SensorType.B1, SensorType.B3, SensorType.A2, SensorType.C } },
        { SensorType.B3, new SensorType[]{ SensorType.E3, SensorType.E4, SensorType.B2, SensorType.B4, SensorType.A3, SensorType.C } },
        { SensorType.B4, new SensorType[]{ SensorType.E4, SensorType.E5, SensorType.B3, SensorType.B5, SensorType.A4, SensorType.C } },
        { SensorType.B5, new SensorType[]{ SensorType.E5, SensorType.E6, SensorType.B4, SensorType.B6, SensorType.A5, SensorType.C } },
        { SensorType.B6, new SensorType[]{ SensorType.E6, SensorType.E7, SensorType.B5, SensorType.B7, SensorType.A6, SensorType.C } },
        { SensorType.B7, new SensorType[]{ SensorType.E7, SensorType.E8, SensorType.B6, SensorType.B8, SensorType.A7, SensorType.C } },
        { SensorType.B8, new SensorType[]{ SensorType.E8, SensorType.E1, SensorType.B7, SensorType.B1, SensorType.A8, SensorType.C } },

        { SensorType.C, new SensorType[]{ SensorType.B1, SensorType.B2, SensorType.B3, SensorType.B4, SensorType.B5, SensorType.B6, SensorType.B7, SensorType.B8} },
    };
    private static readonly Dictionary<string, List<int>> SLIDE_AREA_STEP_MAP = new Dictionary<string, List<int>>()
    {
        {"line3", new List<int>(){ 0, 2, 8, 13 } },
        {"line4", new List<int>(){ 0, 3, 8, 12, 18 } },
        {"line5", new List<int>(){ 0, 3, 6, 11, 15, 19 } },
        {"line6", new List<int>(){ 0, 3, 8, 12, 18 } },
        {"line7", new List<int>(){ 0, 2, 8, 13 } },
        {"circle1", new List<int>(){ 0, 3, 11, 19, 27, 35, 43, 50, 58, 63 } },
        {"circle2", new List<int>(){ 0, 3, 7 } },
        {"circle3", new List<int>(){ 0, 3, 11, 15 } },
        {"circle4", new List<int>(){ 0, 3, 11, 19, 23 } },
        {"circle5", new List<int>(){ 0, 3, 11, 19, 27, 31 } },
        {"circle6", new List<int>(){ 0, 3, 11, 19, 27, 35, 39 } },
        {"circle7", new List<int>(){ 0, 3, 11, 19, 27, 35, 43, 47 } },
        {"circle8", new List<int>(){ 0, 3, 11, 19, 27, 35, 43, 50, 55 } },
        {"v1", new List<int>(){ 0, 3, 6, 11, 15, 19 } },
        {"v2", new List<int>(){ 0, 3, 6, 11, 15, 19 } },
        {"v3", new List<int>(){ 0, 3, 6, 11, 15, 19 } },
        {"v4", new List<int>(){ 0, 3, 6, 11, 15, 19 } },
        {"v6", new List<int>(){ 0, 3, 6, 11, 15, 19 } },
        {"v7", new List<int>(){ 0, 3, 6, 11, 15, 19 } },
        {"v8", new List<int>(){ 0, 3, 6, 11, 15, 19 } },
        {"ppqq1", new List<int>(){ 0, 3, 7, 13, 17, 26, 32, 35 } },
        {"ppqq2", new List<int>(){ 0, 3, 7, 12, 16, 25, 28 } },
        {"ppqq3", new List<int>(){ 0, 3, 6, 12, 15, 22 } },
        {"ppqq4", new List<int>(){ 0, 3, 7, 12, 16, 25, 29, 35, 40, 44, 49 } },
        {"ppqq5", new List<int>(){ 0, 3, 7, 12, 16, 25, 29, 35, 40, 44, 49 } },
        {"ppqq6", new List<int>(){ 0, 3, 7, 12, 16, 25, 28, 34, 38, 41, 48 } },
        {"ppqq7", new List<int>(){ 0, 3, 7, 13, 17, 27, 31, 37, 41, 46 } },
        {"ppqq8", new List<int>(){ 0, 3, 7, 12, 16, 25, 29, 35, 41 } },
        {"pq1", new List<int>(){ 0, 3, 8, 11, 14, 17, 21, 24, 27, 33 } },
        {"pq2", new List<int>(){ 0, 3, 8, 11, 14, 18, 21, 24, 30 } },
        {"pq3", new List<int>(){ 0, 3, 9, 12, 16, 19, 23, 27 } },
        {"pq4", new List<int>(){ 0, 3, 9, 13, 16, 20, 24 } },
        {"pq5", new List<int>(){ 0, 3, 9, 13, 17, 21 } },
        {"pq6", new List<int>(){ 0, 3, 8, 11, 15, 18, 21, 25, 28, 31, 35, 38, 42 } },
        {"pq7", new List<int>(){ 0, 3, 8, 12, 15, 18, 22, 25, 28, 32, 35, 39 } },
        {"pq8", new List<int>(){ 0, 3, 8, 11, 14, 17, 21, 24, 27, 30, 36 } },
        {"s", new List<int>(){ 0, 3, 8, 11, 17, 21, 24, 30 } },
        {"wifi", new List<int>(){ 0, 1, 4, 6, 11 } },
        {"L2", new List<int>(){ 0, 2, 7, 15, 21, 26, 32 } },
        {"L3", new List<int>(){ 0, 2, 8, 17, 20, 26, 29, 34 } },
        {"L4", new List<int>(){ 0, 2, 8, 17, 22, 26, 32 } },
        {"L5", new List<int>(){ 0, 2, 8, 16, 22, 28 } },
    };

    private void Awake()
    {
        Majdata<DataLoader>.Instance = this;
    }

    private void Start()
    {
        objectCounter = Majdata<ObjectCounter>.Instance!;
        skinManager = Majdata<SkinManager>.Instance!;
        noteManager = Majdata<NoteManager>.Instance!;
        for (var i = 1; i < 9; i++)
            noteIndex.Add(i, 0);
        for (var i = 0; i < 33; i++)
            touchIndex.Add((SensorType)i, 0);
    }

    public async UniTask Load(SimaiChart chart, double ignoreOffset, string title, string artist, int diff)
    {
        titleText.text = title;
        artistText.text = artist;
        diffText.text = GetDifficultyText(diff);
        cardImage.color = diffColors[diff];

        levelText.text = chart.Level;
        designText.text = chart.Designer;

        objectCounter.CountNoteSumAsync(chart).Forget();
        objectCounter.ReportMeterBpmAsync(chart).Forget();

        noteManager.ResetIndex();
        streamingRunning = true;
        var timings = chart.NoteTimings.ToArray();
        StreamingSetActive(ignoreOffset).Forget();
        await StreamingCreate(timings, ignoreOffset);
    }

    private async UniTask StreamingCreate(SimaiTimingPoint[] timings, double ignoreOffset)
    {
        var i = 0;

        while (i < timings.Length && timings[i].Timing < ignoreOffset)
        {
            objectCounter
                .CountIgnoreNoteCountAsync(timings[i].Notes)
                .Forget();

            i++;
        }

        i = await LoadStreamingWindow(timings, i, ignoreOffset, StreamingCreatePreloadTime);
        ContinueStreamingCreate(timings, i, ignoreOffset, StreamingCreatePreloadTime).Forget();
    }

    private async UniTask ContinueStreamingCreate(SimaiTimingPoint[] timings, int startIndex, double ignoreOffset, double preloadTime)
    {
        var i = startIndex;

        while (i < timings.Length && streamingRunning)
        {
            i = await LoadStreamingWindow(timings, i, ignoreOffset, preloadTime);
            await UniTask.Yield();
        }
    }

    private async UniTask<int> LoadStreamingWindow(SimaiTimingPoint[] timings, int startIndex, double fallbackTime, double preloadTime)
    {
        var i = startIndex;
        var now = GetStreamingTime(fallbackTime);
        var frameStart = GetTimestamp();

        while (i < timings.Length && streamingRunning)
        {
            var timing = timings[i];

            if (timing.Timing - now > preloadTime)
                break;

            await LoadTiming(timing);
            i++;

            if (!IsFrameBudgetExceeded(frameStart))
                continue;

            await UniTask.Yield();
            frameStart = GetTimestamp();
            now = GetStreamingTime(fallbackTime);
        }

        return i;
    }

    private async UniTask StreamingSetActive(double fallbackTime)
    {
        while (streamingRunning)
        {
            var now = GetStreamingTime(fallbackTime);
            var frameStart = GetTimestamp();

            for (var i = 0; i < noteManager.LoadedNotes.Count; i++)
            {
                var note = noteManager.LoadedNotes[i];
                if (note.time - now > StreamingActivePreloadTime)
                    break;

                if (!note.gameObject.activeSelf)
                    note.gameObject.SetActive(true);

                if (!IsFrameBudgetExceeded(frameStart))
                    continue;

                await UniTask.Yield();
                frameStart = GetTimestamp();
                now = GetStreamingTime(fallbackTime);
            }

            await UniTask.Yield();
        }
    }

    private static long GetTimestamp()
    {
        return System.Diagnostics.Stopwatch.GetTimestamp();
    }

    private static bool IsFrameBudgetExceeded(long frameStart)
    {
        var elapsedMs = (GetTimestamp() - frameStart) * 1000d / System.Diagnostics.Stopwatch.Frequency;
        return elapsedMs >= StreamingFrameBudgetMs;
    }

    private double GetStreamingTime(double fallbackTime)
    {
        var timeProvider = Majdata<TimeProvider>.Instance!;
        return timeProvider.IsStart ? timeProvider.NoteTime : fallbackTime;
    }

    private async UniTask LoadTiming(SimaiTimingPoint timing)
    {
        touchMembers.Clear();
        foreach (var note in timing.Notes)
        {
            if (note.Type == SimaiNoteType.Tap)
            {
                GameObject GOnote;
                TapBase NDCompo;

                if (note.IsForceStar)
                {
                    GOnote = Instantiate(starPrefab, notes.transform);
                    var _NDCompo = GOnote.GetComponent<StarDrop>();

                    _NDCompo.isFakeStarRotate = note.IsFakeRotate;
                    _NDCompo.isFakeStar = true;
                    NDCompo = _NDCompo;
                }
                else
                {
                    GOnote = Instantiate(tapPrefab, notes.transform);
                    NDCompo = GOnote.GetComponent<TapDrop>();
                }

                // note的图层顺序
                NDCompo.noteSortOrder = noteSortOrder;
                noteSortOrder -= NOTE_LAYER_COUNT[note.Type];

                if (timing.Notes.Length > 1) NDCompo.isEach = true;
                NDCompo.isBreak = note.IsBreak;
                NDCompo.isEx = note.IsEx;
                NDCompo.isMine = note.IsMine;
                NDCompo.tapLine = tapLine;
                NDCompo.time = (float)timing.Timing;
                NDCompo.startPosition = note.StartPosition;
                NDCompo.speed = noteSpeed * timing.HSpeed;

                noteManager.AddLoadedNote(NDCompo);
                noteManager.AddNote(NDCompo, noteIndex[note.StartPosition]++);
                GOnote.SetActive(false);
            }
            else if (note.Type == SimaiNoteType.Hold)
            {
                var GOnote = Instantiate(holdPrefab, notes.transform);
                var NDCompo = GOnote.GetComponent<HoldDrop>();

                // note的图层顺序
                NDCompo.noteSortOrder = noteSortOrder;
                noteSortOrder -= NOTE_LAYER_COUNT[note.Type];

                if (timing.Notes.Length > 1) NDCompo.isEach = true;
                NDCompo.time = (float)timing.Timing;
                NDCompo.LastFor = (float)note.HoldTime;
                NDCompo.startPosition = note.StartPosition;
                NDCompo.speed = noteSpeed * timing.HSpeed;
                NDCompo.isEx = note.IsEx;
                NDCompo.isBreak = note.IsBreak;
                NDCompo.isMine = note.IsMine;
                NDCompo.tapLine = tapLine;

                noteManager.AddLoadedNote(NDCompo);
                noteManager.AddNote(NDCompo, noteIndex[note.StartPosition]++);
                GOnote.SetActive(false);
            }
            else if (note.Type == SimaiNoteType.TouchHold)
            {
                var GOnote = Instantiate(touchHoldPrefab, notes.transform);
                var NDCompo = GOnote.GetComponent<TouchHoldDrop>();

                // note的图层顺序
                NDCompo.noteSortOrder = noteSortOrder;
                noteSortOrder -= NOTE_LAYER_COUNT[note.Type];

                if (timing.Notes.Length > 1) NDCompo.isEach = true;
                NDCompo.time = (float)timing.Timing;
                NDCompo.LastFor = (float)note.HoldTime;
                NDCompo.speed = touchSpeed * timing.HSpeed;
                NDCompo.isFirework = note.IsHanabi;
                NDCompo.isBreak = note.IsBreak;
                NDCompo.isMine = note.IsMine;
                NDCompo.areaPosition = note.TouchArea;
                NDCompo.startPosition = note.StartPosition;

                noteManager.AddLoadedNote(NDCompo);
                noteManager.AddTouch(NDCompo, touchIndex[NDCompo.GetSensor()]++);
                GOnote.SetActive(false);
            }
            else if (note.Type == SimaiNoteType.Touch)
            {
                var GOnote = Instantiate(touchPrefab, notes.transform);
                var NDCompo = GOnote.GetComponent<TouchDrop>();

                // note的图层顺序
                NDCompo.noteSortOrder = noteSortOrder;
                noteSortOrder -= NOTE_LAYER_COUNT[note.Type];
                NDCompo.time = (float)timing.Timing;
                NDCompo.areaPosition = note.TouchArea;
                NDCompo.startPosition = note.StartPosition;

                if (timing.Notes.Length > 1)
                {
                    NDCompo.isEach = true;
                    touchMembers.Add(NDCompo);
                }

                NDCompo.speed = touchSpeed * timing.HSpeed;
                NDCompo.isFirework = note.IsHanabi;
                NDCompo.isBreak = note.IsBreak;
                NDCompo.isMine = note.IsMine;
                NDCompo.GroupInfo = null;

                noteManager.AddLoadedNote(NDCompo);
                noteManager.AddTouch(NDCompo, touchIndex[NDCompo.GetSensor()]++);
                GOnote.SetActive(false);
            }

            else if (note.Type == SimaiNoteType.Slide)
                InstantiateStarGroup(timing, note); // 星星组
        }

        //touch group handle
        if (touchMembers.Count != 0)
        {
            var sensorTypes = touchMembers.GroupBy(x => x.GetSensor())
                .Select(x => x.Key)
                .ToList();
            List<List<SensorType>> sensorGroups = new();

            while (sensorTypes.Count > 0)
            {
                var sensorType = sensorTypes[0];
                var existsGroup = sensorGroups.FindAll(x => x.Contains(sensorType));
                var groupMap = TOUCH_GROUPS[sensorType];
                existsGroup.AddRange(sensorGroups.FindAll(x => x.Any(y => groupMap.Contains(y))));

                var groupMembers = existsGroup.SelectMany(x => x)
                    .ToList();
                var newMembers = sensorTypes.FindAll(x => groupMap.Contains(x));

                groupMembers.AddRange(newMembers);
                groupMembers.Add(sensorType);
                var newGroup = groupMembers.GroupBy(x => x)
                    .Select(x => x.Key)
                    .ToList();

                foreach (var newMember in newGroup)
                    sensorTypes.Remove(newMember);
                foreach (var oldGroup in existsGroup)
                    sensorGroups.Remove(oldGroup);

                sensorGroups.Add(newGroup);
            }
            List<TouchGroup> touchGroups = new();
            var groupedMembers = touchMembers.GroupBy(x => x.GetSensor());
            foreach (var group in sensorGroups)
            {
                touchGroups.Add(new TouchGroup()
                {
                    Members = group.SelectMany(x => groupedMembers.Where(g => g.Key == x)
                        .SelectMany(g => g)).ToArray()
                });
            }
            foreach (var member in touchMembers)
                member.GroupInfo = touchGroups.Find(x => x.Members.Any(y => y == member));
        }

        //each handle
        var eachNotes = timing.Notes.ToList().FindAll(o =>
            o.Type != SimaiNoteType.Touch && o.Type != SimaiNoteType.TouchHold);
        if (eachNotes.Count > 1) //有多个非touch note
        {
            var startPos = eachNotes[0].StartPosition;
            var endPos = eachNotes[1].StartPosition;
            endPos = endPos - startPos;
            if (endPos == 0) return;

            var line = Instantiate(eachLine, notes.transform);
            var lineDrop = line.GetComponent<EachLineDrop>();

            lineDrop.time = (float)timing.Timing;
            lineDrop.speed = noteSpeed * timing.HSpeed;

            endPos = endPos < 0 ? endPos + 8 : endPos;
            endPos = endPos > 8 ? endPos - 8 : endPos;
            endPos++;

            if (endPos > 4)
            {
                startPos = eachNotes[1].StartPosition;
                endPos = eachNotes[0].StartPosition;
                endPos = endPos - startPos;
                endPos = endPos < 0 ? endPos + 8 : endPos;
                endPos = endPos > 8 ? endPos - 8 : endPos;
                endPos++;
            }

            lineDrop.startPosition = startPos;
            lineDrop.curvLength = endPos - 1;
        }
    }


    private void InstantiateStarGroup(SimaiTimingPoint timing, SimaiNote note)
    {
        int charIntParse(char c)
        {
            return c - '0';
        }

        var subSlide = new List<SimaiNote>();
        var subBarCount = new List<int>();
        var sumBarCount = 0;

        var noteContent = note.RawContent;
        var latestStartIndex = charIntParse(noteContent[0]); // 存储上一个Slide的结尾 也就是下一个Slide的起点
        var ptr = 1; // 指向目前处理的字符

        var specTimeFlag = 0; // 表示此组合slide是指定总时长 还是指定每一段的时长
        // 0-目前还没有读取 1-读取到了一个未指定时长的段落 2-读取到了一个指定时长的段落 3-（期望）读取到了最后一个时长指定

        while (ptr < noteContent.Length)
            if (!char.IsNumber(noteContent[ptr]))
            {
                // 读取到字符
                var slideTypeChar = noteContent[ptr++].ToString();

                var slidePart = new SimaiNote();
                slidePart.Type = SimaiNoteType.Slide;
                slidePart.StartPosition = latestStartIndex;
                if (slideTypeChar == "V")
                {
                    // 转折星星
                    var middlePos = noteContent[ptr++];
                    var endPos = noteContent[ptr++];

                    slidePart.RawContent = latestStartIndex + slideTypeChar + middlePos + endPos;
                    latestStartIndex = charIntParse(endPos);
                }
                else
                {
                    // 其他普通星星
                    // 额外检查pp和qq
                    if (noteContent[ptr] == slideTypeChar[0]) slideTypeChar += noteContent[ptr++];
                    var endPos = noteContent[ptr++];

                    slidePart.RawContent = latestStartIndex + slideTypeChar + endPos;
                    latestStartIndex = charIntParse(endPos);
                }

                if (noteContent[ptr] == '[')
                {
                    // 如果指定了速度
                    if (specTimeFlag == 0)
                        // 之前未读取过
                        specTimeFlag = 2;
                    else if (specTimeFlag == 1)
                        // 之前读取到的都是未指定时长的段落 那么将flag设为3 如果之后又读取到时长 则报错
                        specTimeFlag = 3;
                    else if (specTimeFlag == 3)
                        // 之前读取到了指定时长 并期待那个时长就是最终时长 但是又读取到一个新的时长 则报错
                        throw new Exception("组合星星有错误\nSLIDE CHAIN ERROR");

                    while (ptr < noteContent.Length && noteContent[ptr] != ']')
                        slidePart.RawContent += noteContent[ptr++];
                    slidePart.RawContent += noteContent[ptr++];
                }
                else
                {
                    // 没有指定速度
                    if (specTimeFlag == 0)
                        // 之前未读取过
                        specTimeFlag = 1;
                    else if (specTimeFlag == 2 || specTimeFlag == 3)
                        // 之前读取到指定时长的段落了 说明这一条组合星星有的指定时长 有的没指定 则需要报错
                        throw new Exception("组合星星有错误\nSLIDE CHAIN ERROR");
                }

                string slideShape = detectShapeFromText(slidePart.RawContent);
                if (slideShape.StartsWith("-"))
                {
                    slideShape = slideShape.Substring(1);
                }
                int slideIndex = SLIDE_PREFAB_MAP[slideShape];
                if (slideIndex < 0) slideIndex = -slideIndex;

                var barCount = slidePrefab[slideIndex].transform.childCount;
                subBarCount.Add(barCount);
                sumBarCount += barCount;

                subSlide.Add(slidePart);
            }
            else
            {
                // 理论上来说 不应该读取到数字 因此如果读取到了 说明有语法错误
                throw new Exception("组合星星有错误\nwSLIDE CHAIN ERROR");
            }

        subSlide.ForEach(o =>
        {
            o.IsBreak = note.IsBreak;
            o.IsEx = note.IsEx;
            o.IsSlideBreak = note.IsSlideBreak;
            o.IsMine = note.IsMine;
            o.IsMineSlide = note.IsMineSlide;
            o.IsSlideNoHead = true;
        });
        subSlide[0].IsSlideNoHead = note.IsSlideNoHead;

        if (specTimeFlag == 1 || specTimeFlag == 0)
            // 如果到结束还是1 那说明没有一个指定了时长 报错
            throw new Exception("组合星星有错误\nwSLIDE CHAIN ERROR");
        // 此时 flag为2表示每条指定语法 为3表示整体指定语法

        var tempBarCount = 0;
        for (var i = 0; i < subSlide.Count; i++)
        {
            subSlide[i].SlideStartTime = note.SlideStartTime + (double)tempBarCount / sumBarCount * note.SlideTime;
            subSlide[i].SlideTime = (double)subBarCount[i] / sumBarCount * note.SlideTime;
            tempBarCount += subBarCount[i];
        }

        GameObject parent = null;
        List<SlideDrop> subSlides = new();
        float totalLen = (float)subSlide.Select(x => x.SlideTime).Sum();
        for (var i = 0; i <= subSlide.Count - 1; i++)
        {
            bool isConn = subSlide.Count != 1;
            bool isGroupHead = i == 0;
            bool isGroupEnd = i == subSlide.Count - 1;
            if (note.RawContent.Contains('w')) //wifi
            {
                if (isConn)
                    throw new InvalidOperationException("不允许Wifi Slide作为Connection Slide的一部分");
                InstantiateWifi(timing, subSlide[i]);
            }
            else
            {
                ConnSlideInfo info = new ConnSlideInfo()
                {
                    TotalLength = totalLen,
                    IsGroupPart = isConn,
                    IsGroupPartHead = isGroupHead,
                    IsGroupPartEnd = isGroupEnd,
                    Parent = parent
                };
                parent = InstantiateSlide(timing, subSlide[i], info);
                subSlides.Add(parent.GetComponent<SlideDrop>());
            }
        }
        float totalSlideLen = 0;
        long judgeQueueLen = 0;
        var slideCount = subSlides.Count;
        for (var i = 0; i < slideCount; i++)
        {
            var isEnd = i == slideCount - 1;
            var table = SlideTables.FindTableByName(subSlides[i].slideType);

            totalSlideLen += subSlides[i].GetSlideLength();
            if (isEnd)
            {
                judgeQueueLen += table!.JudgeQueue.Length;
            }
            else
            {
                judgeQueueLen += table!.JudgeQueue.Length - 1;
            }
        }
        subSlides.ForEach(s =>
        {
            s.ConnectInfo.TotalSlideLen = totalSlideLen;
            s.ConnectInfo.TotalJudgeQueueLen = judgeQueueLen;
            s.Initialize();
        });
    }

    private GameObject InstantiateSlide(SimaiTimingPoint timing, SimaiNote note, ConnSlideInfo info)
    {
        var GOnote = Instantiate(starPrefab, notes.transform);
        GOnote.SetActive(false);
        var NDCompo = GOnote.GetComponent<StarDrop>();
        //无头星星的头只用来叫醒slide
        noteManager.AddLoadedNote(NDCompo);
        if (!note.IsSlideNoHead) noteManager.AddNote(NDCompo, noteIndex[note.StartPosition]++);

        // note的图层顺序
        NDCompo.noteSortOrder = noteSortOrder;
        noteSortOrder -= NOTE_LAYER_COUNT[note.Type];

        NDCompo.rotateSpeed = (float)note.SlideTime;
        NDCompo.isEx = note.IsEx;
        NDCompo.isBreak = note.IsBreak;
        NDCompo.isMine = note.IsMine;
        NDCompo.tapLine = tapLine;

        var slideShape = detectShapeFromText(note.RawContent);
        var isMirror = false;
        if (slideShape.StartsWith("-"))
        {
            isMirror = true;
            slideShape = slideShape.Substring(1);
        }
        var slideIndex = SLIDE_PREFAB_MAP[slideShape];

        var slide = Instantiate(slidePrefab[slideIndex], notes.transform);
        var slide_star = Instantiate(star_slidePrefab, notes.transform);
        slide_star.SetActive(false);
        slide.SetActive(false);
        NDCompo.slide = slide;
        var SliCompo = slide.AddComponent<SlideDrop>();

        SliCompo.slideType = slideShape;
        SliCompo.areaStep = new List<int>(SLIDE_AREA_STEP_MAP[slideShape]);
        SliCompo.smoothSlideAnime = smoothSlideAnime;

        if (timing.Notes.Length > 1)
        {
            var notes = timing.Notes.ToList();
            NDCompo.isEach = true;
            if (notes.FindAll(o => o.Type == SimaiNoteType.Slide).Count > 1)
            {
                SliCompo.isEach = true;
            }

            var count = notes.FindAll(
                o => o.Type == SimaiNoteType.Slide &&
                     o.StartPosition == note.StartPosition).Count;
            if (count > 1)
            {
                NDCompo.isDouble = true;
                if (count == notes.Count)
                    NDCompo.isEach = false;
                else
                    NDCompo.isEach = true;
            }
        }

        SliCompo.ConnectInfo = info;
        SliCompo.isBreak = note.IsSlideBreak;
        SliCompo.isMine = note.IsMineSlide;

        NDCompo.isNoHead = note.IsSlideNoHead;
        NDCompo.time = (float)timing.Timing;
        NDCompo.startPosition = note.StartPosition;
        NDCompo.speed = noteSpeed * timing.HSpeed;


        SliCompo.isMirror = isMirror;
        SliCompo.isJustR = detectJustType(note.RawContent, out int endPos);
        SliCompo.endPosition = endPos;
        if (slideIndex - 26 > 0 && slideIndex - 26 <= 8)
        {
            // known slide sprite issue
            //    1 2 3 4 5 6 7 8
            // p  X X X X X X O O
            // q  X O O X X X X X
            var pqEndPos = slideIndex - 26;
            SliCompo.isSpecialFlip = isMirror == (pqEndPos == 7 || pqEndPos == 8);
        }
        else
        {
            SliCompo.isSpecialFlip = isMirror;
        }
        SliCompo.speed = noteSpeed * timing.HSpeed;
        SliCompo.startTime = (float)timing.Timing;
        SliCompo.startPosition = note.StartPosition;
        SliCompo.star_slide = slide_star;
        SliCompo.time = (float)note.SlideStartTime;
        SliCompo.LastFor = (float)note.SlideTime;
        //SliCompo.sortIndex = -7000 + (int)((lastNoteTime - timing.time) * -100) + sort * 5;
        SliCompo.sortIndex = slideLayer;
        slideLayer -= SLIDE_AREA_STEP_MAP[slideShape].Last();
        //slideLayer += 5;
        return slide;
    }

    private void InstantiateWifi(SimaiTimingPoint timing, SimaiNote note)
    {
        var str = note.RawContent.Substring(0, 3);
        var digits = str.Split('w');
        var startPos = int.Parse(digits[0]);
        var endPos = int.Parse(digits[1]);
        endPos = endPos - startPos;
        endPos = endPos < 0 ? endPos + 8 : endPos;
        endPos = endPos > 8 ? endPos - 8 : endPos;
        endPos++;

        var GOnote = Instantiate(starPrefab, notes.transform);
        GOnote.SetActive(false);
        var NDCompo = GOnote.GetComponent<StarDrop>();
        //无头星星的头只用来叫醒slide
        noteManager.AddLoadedNote(NDCompo);
        if (!note.IsSlideNoHead) noteManager.AddNote(NDCompo, noteIndex[note.StartPosition]++);

        // note的图层顺序
        NDCompo.noteSortOrder = noteSortOrder;
        noteSortOrder -= NOTE_LAYER_COUNT[note.Type];

        NDCompo.rotateSpeed = (float)note.SlideTime;
        NDCompo.isEx = note.IsEx;
        NDCompo.isBreak = note.IsBreak;
        NDCompo.isMine = note.IsMine;
        NDCompo.tapLine = tapLine;

        var slideWifi = Instantiate(slidePrefab[SLIDE_PREFAB_MAP["wifi"]], notes.transform);
        slideWifi.SetActive(false);
        NDCompo.slide = slideWifi;
        var WifiCompo = slideWifi.GetComponent<WifiDrop>();

        WifiCompo.areaStep = new List<int>(SLIDE_AREA_STEP_MAP["wifi"]);
        WifiCompo.smoothSlideAnime = smoothSlideAnime;

        if (timing.Notes.Length > 1)
        {
            NDCompo.isEach = true;
            NDCompo.isDouble = false;
            var notes = timing.Notes.ToList();
            if (notes.FindAll(
                    o => o.Type == SimaiNoteType.Slide).Count
                > 1)
                WifiCompo.isEach = true;
            var count = notes.FindAll(
                o => o.Type == SimaiNoteType.Slide &&
                     o.StartPosition == note.StartPosition).Count;
            if (count > 1) //有同起点
            {
                NDCompo.isDouble = true;
                if (count == notes.Count)
                    NDCompo.isEach = false;
                else
                    NDCompo.isEach = true;
            }
        }

        WifiCompo.isBreak = note.IsSlideBreak;
        WifiCompo.isMine = note.IsMineSlide;

        NDCompo.isNoHead = note.IsSlideNoHead;
        NDCompo.time = (float)timing.Timing;
        NDCompo.startPosition = note.StartPosition;
        NDCompo.speed = noteSpeed * timing.HSpeed;

        WifiCompo.isJustR = detectJustType(note.RawContent, out endPos);
        WifiCompo.endPosition = endPos;
        WifiCompo.speed = noteSpeed * timing.HSpeed;
        WifiCompo.startTime = (float)timing.Timing;
        WifiCompo.startPosition = note.StartPosition;
        WifiCompo.time = (float)note.SlideStartTime;
        WifiCompo.LastFor = (float)note.SlideTime;
        WifiCompo.sortIndex = slideLayer;
        slideLayer -= SLIDE_AREA_STEP_MAP["wifi"].Last();
    }


    //helper

    private bool detectJustType(string content, out int endPos)
    {
        // > < ^ V w
        if (content.Contains('>'))
        {
            var str = content.Substring(0, 3);
            var digits = str.Split('>');
            var startPos = int.Parse(digits[0]);
            endPos = int.Parse(digits[1]);
            if (isUpperHalf(startPos))
                return true;
            return false;
        }

        if (content.Contains('<'))
        {
            var str = content.Substring(0, 3);
            var digits = str.Split('<');
            var startPos = int.Parse(digits[0]);
            endPos = int.Parse(digits[1]);
            if (!isUpperHalf(startPos))
                return true;
            return false;
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

            if (endPos < 4)
            {
                endPos = int.Parse(digits[1]);
                return true;
            }
            if (endPos > 4)
            {
                endPos = int.Parse(digits[1]);
                return false;
            }
        }
        else if (content.Contains('V'))
        {
            var str = content.Substring(0, 4);
            var digits = str.Split('V');
            endPos = int.Parse(digits[1][1].ToString());

            if (isRightHalf(endPos))
                return true;
            return false;
        }
        else if (content.Contains('w'))
        {
            var str = content.Substring(0, 3);
            endPos = int.Parse(str.Substring(2, 1));
            if (isUpperHalf(endPos))
                return true;
            return false;
        }
        else
        {
            //int endPos;
            if (content.Contains("qq") || content.Contains("pp"))
                endPos = int.Parse(content.Substring(3, 1));
            else
                endPos = int.Parse(content.Substring(2, 1));
            if (isRightHalf(endPos))
                return true;
            return false;
        }
        return true;
    }

    private string detectShapeFromText(string content)
    {
        int getRelativeEndPos(int startPos, int endPos)
        {
            endPos = endPos - startPos;
            endPos = endPos < 0 ? endPos + 8 : endPos;
            endPos = endPos > 8 ? endPos - 8 : endPos;
            return endPos + 1;
        }

        //print(content);
        if (content.Contains('-'))
        {
            // line
            var str = content.Substring(0, 3); //something like "8-6"
            var digits = str.Split('-');
            var startPos = int.Parse(digits[0]);
            var endPos = int.Parse(digits[1]);
            endPos = getRelativeEndPos(startPos, endPos);
            if (endPos < 3 || endPos > 7) throw new Exception("-星星至少隔开一键\n-スライドエラー");
            return "line" + endPos;
        }

        if (content.Contains('>'))
        {
            // circle 默认顺时针
            var str = content.Substring(0, 3);
            var digits = str.Split('>');
            var startPos = int.Parse(digits[0]);
            var endPos = int.Parse(digits[1]);
            endPos = getRelativeEndPos(startPos, endPos);
            if (isUpperHalf(startPos))
            {
                return "circle" + endPos;
            }

            endPos = MirrorKeys(endPos);
            return "-circle" + endPos; //Mirror
        }

        if (content.Contains('<'))
        {
            // circle 默认顺时针
            var str = content.Substring(0, 3);
            var digits = str.Split('<');
            var startPos = int.Parse(digits[0]);
            var endPos = int.Parse(digits[1]);
            endPos = getRelativeEndPos(startPos, endPos);
            if (!isUpperHalf(startPos))
            {
                return "circle" + endPos;
            }

            endPos = MirrorKeys(endPos);
            return "-circle" + endPos; //Mirror
        }

        if (content.Contains('^'))
        {
            var str = content.Substring(0, 3);
            var digits = str.Split('^');
            var startPos = int.Parse(digits[0]);
            var endPos = int.Parse(digits[1]);
            endPos = getRelativeEndPos(startPos, endPos);

            if (endPos == 1 || endPos == 5)
            {
                throw new Exception("^星星不合法\n^スライドエラー");
            }

            if (endPos < 5)
            {
                return "circle" + endPos;
            }
            if (endPos > 5)
            {
                return "-circle" + MirrorKeys(endPos);
            }
        }

        if (content.Contains('v'))
        {
            // v
            var str = content.Substring(0, 3);
            var digits = str.Split('v');
            var startPos = int.Parse(digits[0]);
            var endPos = int.Parse(digits[1]);
            endPos = getRelativeEndPos(startPos, endPos);
            if (endPos == 5) throw new Exception("v星星不合法\nvスライドエラー");
            return "v" + endPos;
        }

        if (content.Contains("pp"))
        {
            // ppqq 默认为pp
            var str = content.Substring(0, 4);
            var digits = str.Split('p');
            var startPos = int.Parse(digits[0]);
            var endPos = int.Parse(digits[2]);
            endPos = getRelativeEndPos(startPos, endPos);
            return "ppqq" + endPos;
        }

        if (content.Contains("qq"))
        {
            // ppqq 默认为pp
            var str = content.Substring(0, 4);
            var digits = str.Split('q');
            var startPos = int.Parse(digits[0]);
            var endPos = int.Parse(digits[2]);
            endPos = getRelativeEndPos(startPos, endPos);
            endPos = MirrorKeys(endPos);
            return "-ppqq" + endPos;
        }

        if (content.Contains('p'))
        {
            // pq 默认为p
            var str = content.Substring(0, 3);
            var digits = str.Split('p');
            var startPos = int.Parse(digits[0]);
            var endPos = int.Parse(digits[1]);
            endPos = getRelativeEndPos(startPos, endPos);
            return "pq" + endPos;
        }

        if (content.Contains('q'))
        {
            // pq 默认为p
            var str = content.Substring(0, 3);
            var digits = str.Split('q');
            var startPos = int.Parse(digits[0]);
            var endPos = int.Parse(digits[1]);
            endPos = getRelativeEndPos(startPos, endPos);
            endPos = MirrorKeys(endPos);
            return "-pq" + endPos;
        }

        if (content.Contains('s'))
        {
            // s
            var str = content.Substring(0, 3);
            var digits = str.Split('s');
            var startPos = int.Parse(digits[0]);
            var endPos = int.Parse(digits[1]);
            endPos = getRelativeEndPos(startPos, endPos);
            if (endPos != 5) throw new Exception("s星星尾部错误\nsスライドエラー");
            return "s";
        }

        if (content.Contains('z'))
        {
            // s镜像
            var str = content.Substring(0, 3);
            var digits = str.Split('z');
            var startPos = int.Parse(digits[0]);
            var endPos = int.Parse(digits[1]);
            endPos = getRelativeEndPos(startPos, endPos);
            if (endPos != 5) throw new Exception("z星星尾部错误\nzスライドエラー");
            return "-s";
        }

        if (content.Contains('V'))
        {
            // L
            var str = content.Substring(0, 4);
            var digits = str.Split('V');
            var startPos = int.Parse(digits[0]);
            var turnPos = int.Parse(digits[1][0].ToString());
            var endPos = int.Parse(digits[1][1].ToString());

            turnPos = getRelativeEndPos(startPos, turnPos);
            endPos = getRelativeEndPos(startPos, endPos);
            if (turnPos == 7)
            {
                if (endPos < 2 || endPos > 5) throw new Exception("V星星终点不合法\nVスライドエラー");
                return "L" + endPos;
            }

            if (turnPos == 3)
            {
                if (endPos < 5) throw new Exception("V星星终点不合法\nVスライドエラー");
                return "-L" + MirrorKeys(endPos);
            }

            throw new Exception("V星星拐点只能隔开一键\nVスライドエラー");
        }

        if (content.Contains('w'))
        {
            // wifi
            var str = content.Substring(0, 3);
            var digits = str.Split('w');
            var startPos = int.Parse(digits[0]);
            var endPos = int.Parse(digits[1]);
            endPos = getRelativeEndPos(startPos, endPos);
            if (endPos != 5) throw new Exception("w星星尾部错误\nwスライドエラー");
            return "wifi";
        }

        return "";
    }

    private bool isUpperHalf(int key)
    {
        if (key == 7) return true;
        if (key == 8) return true;
        if (key == 1) return true;
        if (key == 2) return true;

        return false;
    }

    private bool isRightHalf(int key)
    {
        if (key == 1) return true;
        if (key == 2) return true;
        if (key == 3) return true;
        if (key == 4) return true;

        return false;
    }

    private int MirrorKeys(int key)
    {
        if (key == 1) return 1;
        if (key == 2) return 8;
        if (key == 3) return 7;
        if (key == 4) return 6;

        if (key == 5) return 5;
        if (key == 6) return 4;
        if (key == 7) return 3;
        if (key == 8) return 2;
        throw new Exception("Keys out of range: " + key);
    }

    public static string GetDifficultyText(int index)
    {
        if (index == 0) return "EASY";
        if (index == 1) return "BASIC";
        if (index == 2) return "ADVANCED";
        if (index == 3) return "EXPERT";
        if (index == 4) return "MASTER";
        if (index == 5) return "Re:MASTER";
        if (index == 6) return "ORIGINAL";
        return "DEFAULT";
    }

    public void ResetState()
    {
        loadedData = null;
        streamingRunning = false;
        for (var i = 1; i < 9; i++)
            noteIndex[i] = 0;
        for (var i = 0; i < 33; i++)
            touchIndex[(SensorType)i] = 0;
        slideLayer = -1;
        noteSortOrder = 0;
    }
}