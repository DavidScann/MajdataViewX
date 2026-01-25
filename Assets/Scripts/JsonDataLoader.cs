using Assets.Scripts.Notes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class JsonDataLoader : MonoBehaviour
{
    public float noteSpeed = 7f;
    public float touchSpeed = 7.5f;
    public bool smoothSlideAnime = false;
    public Sprite starEach;
    public GameObject tapPrefab;
    public GameObject holdPrefab;
    public GameObject starPrefab;
    public GameObject touchHoldPrefab;
    public GameObject touchPrefab;
    public GameObject cmdPrefab;
    public GameObject eachLine;
    public GameObject starLine;
    public GameObject notes;
    public GameObject star_slidePrefab;
    public GameObject[] slidePrefab;
    public GameObject subSVPrefab;
    public Material breakMaterial;
    public RuntimeAnimatorController BreakShine;
    public RuntimeAnimatorController JudgeBreakShine;
    public RuntimeAnimatorController HoldShine;

    public Text diffText;
    public Text levelText;
    public Text titleText;
    public Text artistText;
    public Text designText;
    public RawImage cardImage;

    public Color[] diffColors = new Color[7];

    public TextMeshProUGUI levelTextM;
    public Text titleTextM;
    public Text artistTextM;
    public Text designTextM;
    public Text bpmTextM;
    public SpriteRenderer cardImageM;
    public SpriteRenderer LvBackgroundM;
    public SpriteRenderer[] TabM = new SpriteRenderer[2];
    public GameObject[] Modes = new GameObject[2];

    public Sprite[] cardImagesM = new Sprite[8];
    public Sprite[] LvBackgroundsM = new Sprite[8];
    public Sprite[] TabsM = new Sprite[8];
    public Texture2D[] MLevelsM = new Texture2D[8];
    public GameObject QuestionM;
    public GameObject TabUTGM;
    public Text UTGTextM;
    public GameObject TabUTG2pM;

    private Dictionary<int, SubSV> SubSVList;

    private CustomSkin customSkin;
    private AudioTimeProvider timeProvider;

    private ObjectCounter ObjectCounter;

    private int slideLayer = -10000;
    private int noteSortOrder = 0;

    string kPath = new DirectoryInfo(Application.dataPath).Parent.FullName + "/Skin/";

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

    static Dictionary<string, float> SLIDE_AREA_CONST = new()
    {
        { "line3", 0.1919f},
        { "line4", 0.1793f},
        { "line5", 0.1629f},
        { "line6", 0.1793f},
        { "line7", 0.1919f},
        { "circle1", 0.7892f},
        { "circle2", 0.2326f},
        { "circle3", 0.1550f},
        { "circle4", 0.1163f},
        { "circle5", 0.0930f},
        { "circle6", 0.0775f},
        { "circle7", 0.0664f},
        { "circle8", 0.0490f},
        { "v1", 0.1629f},
        { "v2", 0.1629f},
        { "v3", 0.1629f},
        { "v4", 0.1629f},
        { "v5", 0.1629f},
        { "v6", 0.1629f},
        { "v7", 0.1629f},
        { "v8", 0.1629f},
        { "ppqq1", 0.1014f},
        { "ppqq2", 0.1204f},
        { "ppqq3", 0.1434f},
        { "ppqq4", 0.0697f},
        { "ppqq5", 0.0867f},
        { "ppqq6", 0.1026f},
        { "ppqq7", 0.1266f},
        { "ppqq8", 0.1413f},
        { "pq1", 0.1021f},
        { "pq2", 0.1144f},
        { "pq3", 0.1247f},
        { "pq4", 0.1436f},
        { "pq5", 0.1627f},
        { "pq6", 0.0752f},
        { "pq7", 0.0984f},
        { "pq8", 0.1126f},
        { "s", 0.1054f},
        { "wifi", 0.1829f},
        { "L2", 0.0948f},
        { "L3", 0.0711f},
        { "L4", 0.0948f},
        { "L5", 0.1186f},
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
        {"wifi", new List<int>(){ 0, 3, 6, 7, 9, 11 } },
        {"L2", new List<int>(){ 0, 2, 7, 15, 21, 26, 32 } },
        {"L3", new List<int>(){ 0, 2, 8, 17, 20, 26, 29, 34 } },
        {"L4", new List<int>(){ 0, 2, 8, 17, 22, 26, 32 } },
        {"L5", new List<int>(){ 0, 2, 8, 16, 22, 28 } },
    };

    // Start is called before the first frame update
    private void Start()
    {
        Application.targetFrameRate = 120;
        ObjectCounter = GameObject.Find("ObjectCounter").GetComponent<ObjectCounter>();
        timeProvider = GameObject.Find("AudioTimeProvider").GetComponent<AudioTimeProvider>();
        customSkin = GameObject.Find("Outline").GetComponent<CustomSkin>();
        SubSVList = timeProvider.SubSVList;
    }

    // Update is called once per frame
    private void Update()
    {
    }

    public void LoadJson(string json, float ignoreOffset)
    {
        var loadedData = JsonConvert.DeserializeObject<Majson>(json);

        //旧
        diffText.text = loadedData.difficulty;
        levelText.text = loadedData.level;
        titleText.text = loadedData.title;
        artistText.text = loadedData.artist;
        designText.text = loadedData.designer;
        cardImage.color = diffColors[loadedData.diffNum];

        //新：乌蒙的UI
        levelTextM.spriteAsset.spriteSheet = MLevelsM[loadedData.diffNum];
        levelTextM.spriteAsset.material.SetTexture("_MainTex", MLevelsM[loadedData.diffNum]);

        UTGTextM.text = "";
        TabUTG2pM.SetActive(false); //保证初始状态

        StringBuilder sb = new();
        if (loadedData.level.StartsWith('%'))
        {
            var last = loadedData.level.LastIndexOf('%');

            if (last != 0)
            {
                UTGTextM.text = loadedData.level[1..last];
                loadedData.level = loadedData.level[(last + 1)..];
            }

            if (loadedData.level[0] == '!') //shit!!!此时必定是截掉%%段还有!的情况
            {
                TabUTG2pM.SetActive(true);
                loadedData.level = loadedData.level[1..];
            }
        }
        if (loadedData.level.Length == 1)
        {
            sb.Append("<space=1>");
        }
        foreach (var item in loadedData.level)
        {
            if (int.TryParse(item.ToString(), out int lv))
                sb.Append($"<sprite={lv}>");
            else
            {
                switch (item)
                {
                    case '+':
                        sb.Append("<sprite=10>");
                        break;
                    case '-':
                        sb.Append("<sprite=11>");
                        break;
                    case ',':
                        sb.Append("<sprite=12>");
                        break;
                    case '.':
                        sb.Append("<sprite=13>");
                        break;
                }
            }
        }
        levelTextM.text = sb.ToString();
        titleTextM.text = loadedData.title;
        artistTextM.text = loadedData.artist;
        designTextM.text = loadedData.designer;
        bpmTextM.text = "BPM " + loadedData.wholebpm;
        cardImageM.sprite = cardImagesM[loadedData.diffNum];
        LvBackgroundM.sprite = LvBackgroundsM[loadedData.diffNum];

        QuestionM.SetActive(loadedData.level.EndsWith('?'));
        loadedData.level = loadedData.level.Replace("?", "");

        if (loadedData.diffNum != 6)
        {
            TabUTGM.SetActive(false);
            if (loadedData.mode == ChartMode.Standard)
            {
                Modes[0].SetActive(true);
                Modes[1].SetActive(false);
                TabM[0].sprite = TabsM[loadedData.diffNum];
            }
            else
            {
                Modes[0].SetActive(false);
                Modes[1].SetActive(true);
                TabM[1].sprite = TabsM[loadedData.diffNum];
            }
        }
        else
        {
            Modes[0].SetActive(false);
            Modes[1].SetActive(false);
            TabM[0].gameObject.SetActive(false);
            TabM[1].gameObject.SetActive(false);
            TabUTGM.SetActive(true);
        }

        CountNoteSum(loadedData);

        var lastNoteTime = loadedData.timingList.Last().time;
        float lastBPM = 0;

        List<float> bpmlist = new();

        foreach (var timing in loadedData.timingList)
            try
            {
                if (timeProvider.SVList.Count == 0 || timeProvider.SVList[^1] != timing.SVeloc)
                {
                    timeProvider.SVList.Add(timing.SVeloc);
                    Debug.Log(timing.SVeloc);
                    timeProvider.SVTime.Add((float)timing.time);
                }
                if (timing.time < ignoreOffset)
                {
                    CountNoteCount(timing.noteList);
                    continue;
                }
                if (timing.currentBpm != lastBPM)
                {
                    var GOnote = Instantiate(cmdPrefab);
                    var NDCompo = GOnote.GetComponent<CmdDrop>();
                    NDCompo.time = (float)timing.time;
                    NDCompo.times = 1;
                    NDCompo.Handler = () =>
                    {
                        GameObject.Find("objBPM").GetComponent<Text>().text = Math.Truncate(timing.currentBpm).ToString();
                    };
                    lastBPM = timing.currentBpm;

                    bpmlist.Add(timing.currentBpm);
                }

                for (var i = 0; i < timing.noteList.Count; i++)
                {
                    var note = timing.noteList[i];
                    if (note.noteType == SimaiNoteType.Tap)
                    {
                        var GOnote = Instantiate(tapPrefab, notes.transform);
                        var NDCompo = GOnote.GetComponent<TapDrop>();

                        // note的图层顺序
                        NDCompo.noteSortOrder = noteSortOrder;
                        noteSortOrder -= NOTE_LAYER_COUNT[note.noteType];

                        if (note.isForceStar)
                        {
                            if (string.IsNullOrEmpty(note.kSkin))
                            {
                                NDCompo.normalSpr = customSkin.Star;
                                NDCompo.eachSpr = customSkin.Star_Each;
                                NDCompo.breakSpr = customSkin.Star_Break;
                                NDCompo.exSpr = customSkin.Star_Ex;
                            }
                            else
                            {
                                NDCompo.normalSpr = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin));
                                NDCompo.eachSpr = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin.Insert(note.kSkin.Length - 4, "_each")));
                                NDCompo.breakSpr = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin.Insert(note.kSkin.Length - 4, "_break")));
                                NDCompo.exSpr = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin.Insert(note.kSkin.Length - 4, "_ex")));
                            }
                            NDCompo.tapLine = starLine;
                            NDCompo.isFakeStarRotate = note.isFakeRotate;
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(note.kSkin))
                            {
                                NDCompo.normalSpr = customSkin.Tap;
                                NDCompo.breakSpr = customSkin.Tap_Break;
                                NDCompo.eachSpr = customSkin.Tap_Each;
                                NDCompo.exSpr = customSkin.Tap_Ex;
                            }
                            else
                            {
                                NDCompo.normalSpr = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin));
                                NDCompo.eachSpr = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin.Insert(note.kSkin.Length - 4, "_each")));
                                NDCompo.breakSpr = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin.Insert(note.kSkin.Length - 4, "_break")));
                                NDCompo.exSpr = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin.Insert(note.kSkin.Length - 4, "_ex")));
                            }
                        }

                        NDCompo.BreakShine = BreakShine;

                        if (timing.noteList.Count > 1) NDCompo.isEach = true;
                        NDCompo.isBreak = note.isBreak;
                        NDCompo.isEX = note.isEx;
                        NDCompo.isUnplayable = note.isUnplayable;
                        NDCompo.canSVAffect = note.canSVAffect;
                        NDCompo.time = (float)timing.time;
                        NDCompo.startPosition = note.startPosition;
                        NDCompo.speed = noteSpeed * timing.HSpeed;
                    }
                    else if (note.noteType == SimaiNoteType.Hold)
                    {
                        var GOnote = Instantiate(holdPrefab, notes.transform);
                        var NDCompo = GOnote.GetComponent<HoldDrop>();

                        // note的图层顺序
                        NDCompo.noteSortOrder = noteSortOrder;
                        noteSortOrder -= NOTE_LAYER_COUNT[note.noteType];

                        if (string.IsNullOrEmpty(note.kSkin))
                        {
                            NDCompo.tapSpr = customSkin.Hold;
                            NDCompo.holdOnSpr = customSkin.Hold_On;
                            NDCompo.eachSpr = customSkin.Hold_Each;
                            NDCompo.eachHoldOnSpr = customSkin.Hold_Each_On;
                            NDCompo.exSpr = customSkin.Hold_Ex;
                            NDCompo.breakSpr = customSkin.Hold_Break;
                            NDCompo.breakHoldOnSpr = customSkin.Hold_Break_On;
                            NDCompo.holdOffSpr = customSkin.Hold_Off;
                        }
                        else
                        {
                            NDCompo.tapSpr = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin));
                            NDCompo.holdOnSpr = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin.Insert(note.kSkin.Length - 4, "_on")));
                            NDCompo.eachSpr = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin.Insert(note.kSkin.Length - 4, "_each")));
                            NDCompo.eachHoldOnSpr = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin.Insert(note.kSkin.Length - 4, "_each_on")));
                            NDCompo.exSpr = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin.Insert(note.kSkin.Length - 4, "_ex")));
                            NDCompo.breakSpr = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin.Insert(note.kSkin.Length - 4, "_break")));
                            NDCompo.breakHoldOnSpr = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin.Insert(note.kSkin.Length - 4, "_break_on")));
                            NDCompo.holdOffSpr = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin.Insert(note.kSkin.Length - 4, "_off")));
                        }

                        NDCompo.HoldShine = HoldShine;
                        NDCompo.BreakShine = BreakShine;

                        if (timing.noteList.Count > 1) NDCompo.isEach = true;
                        NDCompo.time = (float)timing.time;
                        NDCompo.LastFor = (float)note.holdTime;
                        NDCompo.startPosition = note.startPosition;
                        NDCompo.speed = noteSpeed * timing.HSpeed;
                        NDCompo.isEX = note.isEx;
                        NDCompo.isBreak = note.isBreak;
                        NDCompo.isUnplayable = note.isUnplayable;
                        NDCompo.canSVAffect = note.canSVAffect;
                    }
                    else if (note.noteType == SimaiNoteType.TouchHold)
                    {
                        var GOnote = Instantiate(touchHoldPrefab, notes.transform);
                        var NDCompo = GOnote.GetComponent<TouchHoldDrop>();

                        // note的图层顺序
                        NDCompo.noteSortOrder = noteSortOrder;
                        noteSortOrder -= NOTE_LAYER_COUNT[note.noteType];

                        if (timing.noteList.Count > 1) NDCompo.isEach = true;
                        NDCompo.time = (float)timing.time;
                        NDCompo.LastFor = (float)note.holdTime;
                        NDCompo.speed = touchSpeed * timing.HSpeed;
                        NDCompo.isFirework = note.isHanabi;
                        NDCompo.areaPosition = note.touchArea;
                        NDCompo.startPosition = note.startPosition;
                        NDCompo.TouchPointEachSprite = customSkin.TouchPoint_Each;

                        if (timing.noteList.Count > 1) NDCompo.isEach = true;


                        if (string.IsNullOrEmpty(note.kSkin))
                        {
                            Array.Copy(customSkin.TouchHold, NDCompo.TouchHoldSprite, 5);
                            NDCompo.TouchPointSprite = customSkin.TouchPoint;
                            NDCompo.TouchPointEachSprite = customSkin.TouchPoint_Each;
                            NDCompo.TouchHoldBorderMiss = customSkin.TouchHoldBorderMiss;
                        }
                        else
                        {
                            Sprite[] touchHold = new Sprite[5];
                            touchHold[0] = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin.Insert(note.kSkin.Length - 4, "_0")));
                            touchHold[1] = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin.Insert(note.kSkin.Length - 4, "_1")));
                            touchHold[2] = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin.Insert(note.kSkin.Length - 4, "_2")));
                            touchHold[3] = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin.Insert(note.kSkin.Length - 4, "_3")));
                            touchHold[4] = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin.Insert(note.kSkin.Length - 4, "_border")));

                            Array.Copy(touchHold, NDCompo.TouchHoldSprite, 5);
                            NDCompo.TouchPointSprite = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin.Insert(note.kSkin.Length - 4, "_point")));
                            NDCompo.TouchPointEachSprite = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin.Insert(note.kSkin.Length - 4, "_point_each")));
                            NDCompo.TouchHoldBorderMiss = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin.Insert(note.kSkin.Length - 4, "_border_miss")));
                        }
                        NDCompo.isUnplayable = note.isUnplayable;
                        NDCompo.canSVAffect = note.canSVAffect;
                    }
                    else if (note.noteType == SimaiNoteType.Touch)
                    {
                        var GOnote = Instantiate(touchPrefab, notes.transform);
                        var NDCompo = GOnote.GetComponent<TouchDrop>();

                        // note的图层顺序
                        NDCompo.noteSortOrder = noteSortOrder;
                        noteSortOrder -= NOTE_LAYER_COUNT[note.noteType];

                        NDCompo.time = (float)timing.time;
                        NDCompo.areaPosition = note.touchArea;
                        NDCompo.startPosition = note.startPosition;

                        if (string.IsNullOrEmpty(note.kSkin))
                        {
                            NDCompo.fanNormalSprite = customSkin.Touch;
                            NDCompo.fanEachSprite = customSkin.Touch_Each;
                            NDCompo.pointNormalSprite = customSkin.TouchPoint;
                            NDCompo.pointEachSprite = customSkin.TouchPoint_Each;
                            NDCompo.justSprite = customSkin.TouchJust;
                            Array.Copy(customSkin.TouchBorder, NDCompo.multTouchNormalSprite, 2);
                            Array.Copy(customSkin.TouchBorder_Each, NDCompo.multTouchEachSprite, 2);
                        }
                        else
                        {
                            NDCompo.fanNormalSprite = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin));
                            NDCompo.fanEachSprite = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin.Insert(note.kSkin.Length - 4, "_each")));
                            NDCompo.pointNormalSprite = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin.Insert(note.kSkin.Length - 4, "_point")));
                            NDCompo.pointEachSprite = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin.Insert(note.kSkin.Length - 4, "_point_each")));
                            NDCompo.justSprite = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin.Insert(note.kSkin.Length - 4, "_just")));
                            NDCompo.multTouchNormalSprite[0] = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin.Insert(note.kSkin.Length - 4, "_border_2")));
                            NDCompo.multTouchNormalSprite[1] = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin.Insert(note.kSkin.Length - 4, "_border_3")));
                            NDCompo.multTouchEachSprite[0] = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin.Insert(note.kSkin.Length - 4, "_border_2_each")));
                            NDCompo.multTouchEachSprite[1] = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.kSkin.Insert(note.kSkin.Length - 4, "_border_3_each")));
                        }

                        if (timing.noteList.Count > 1)
                            NDCompo.isEach = true;
                        NDCompo.speed = touchSpeed * timing.HSpeed;
                        NDCompo.isFirework = note.isHanabi;
                        NDCompo.isUnplayable = note.isUnplayable;
                        NDCompo.canSVAffect = note.canSVAffect;
                    }
                    else if (note.noteType == SimaiNoteType.Slide)
                    {
                        string kPattern = @"k""([^""]+\.png)""(?:'([^']+\.wav)')?|k'([^']+\.wav)'"; // k"*.png" or k'*.wav' or k"*.png"'*.wav'
                        note.noteContent = Regex.Replace(note.noteContent, kPattern, "");
                        InstantiateStarGroup(timing, note, i, lastNoteTime); // 星星组
                    }
                    else if (note.noteType == SimaiNoteType.NoneOrCmd)
                    {
                        string[] cmd = note.noteContent[2..].Split('.');
                        if (cmd[0] == "data")
                        {
                            var GOnote = Instantiate(cmdPrefab);
                            var NDCompo = GOnote.GetComponent<CmdDrop>();
                            NDCompo.time = (float)timing.time;
                            if (cmd.Length > 2 && int.TryParse(cmd[2], out int result))
                                NDCompo.times = result;
                            else NDCompo.times = 1;

                            ObjectCounter oc = GameObject.Find("ObjectCounter").GetComponent<ObjectCounter>();
                            switch (cmd[1])
                            {
                                case "tap":
                                    NDCompo.Handler = () => { oc.tapCount++; };
                                    break;
                                case "hod":
                                case "hold":
                                    NDCompo.Handler = () => { oc.holdCount++; };
                                    break;
                                case "sld":
                                case "slide":
                                    NDCompo.Handler = () => { oc.slideCount++; };
                                    break;
                                case "toh":
                                case "touch":
                                    NDCompo.Handler = () => { oc.touchCount++; };
                                    break;
                                case "brk":
                                case "break":
                                    NDCompo.Handler = () => { oc.breakCount++; };
                                    break;
                                default:
                                    break;
                            }
                        }
                        else if (cmd[0] == "ui")
                        {
                            var GOnote = Instantiate(cmdPrefab);
                            var NDCompo = GOnote.GetComponent<CmdDrop>();
                            NDCompo.time = (float)timing.time;

                            if (cmd[1] == "bpmrange")
                            {
                                NDCompo.times = 1;
                                NDCompo.Handler = () =>
                                {
                                    GameObject.Find("objBPMRange").GetComponent<Text>().text = $"{cmd[2]} - {cmd[3]}";
                                };
                            }
                            else if (cmd[1] == "meter")
                            {
                                NDCompo.times = 1;
                                NDCompo.Handler = () =>
                                {
                                    GameObject.Find("objMeter").GetComponent<TextMeshProUGUI>().text = $"{cmd[2]}\n{cmd[3]}";
                                };
                            }
                        }
                        else if (cmd[0] == "sv")
                        {
                            float speed = float.Parse(cmd[2] + (cmd.Length == 4 ? '.' + cmd[3] : ""));

                            if (int.TryParse(cmd[1], out int count) && count >= 2)
                            {
                                if (SubSVList.ContainsKey(count))
                                {
                                    var subSV = SubSVList[count];
                                    if (subSV.SVList.Count == 0 || subSV.SVList[^1] != speed)
                                    {
                                        subSV.SVList.Add(speed);
                                        subSV.SVTime.Add((float)timing.time);
                                    }
                                }
                                else
                                {
                                    var subSV = Instantiate(subSVPrefab).GetComponent<SubSV>();
                                    SubSVList.Add(count, subSV);
                                    if (subSV.SVList.Count == 0 || subSV.SVList[^1] != speed)
                                    {
                                        subSV.SVList.Add(speed);
                                        subSV.SVTime.Add((float)timing.time);
                                    }
                                }
                            }
                        }
                    }
                }

                var eachNotes = timing.noteList.FindAll(o =>
                    o.noteType != SimaiNoteType.Touch && o.noteType != SimaiNoteType.TouchHold && !o.isSlideNoHead);
                if (eachNotes.Count > 1) //有多个非touchnote
                {
                    var startPos = eachNotes[0].startPosition;
                    var endPos = eachNotes[1].startPosition;
                    endPos = endPos - startPos;
                    if (endPos == 0) continue;

                    var line = Instantiate(eachLine, notes.transform);
                    var lineDrop = line.GetComponent<EachLineDrop>();

                    lineDrop.time = (float)timing.time;
                    lineDrop.speed = noteSpeed * timing.HSpeed;
                    lineDrop.canSVAffect = 1;
                    foreach (var eachNote in eachNotes)
                    {
                        if (eachNote.canSVAffect != 1)
                        {
                            lineDrop.canSVAffect = eachNote.canSVAffect;
                        }
                    }

                    endPos = endPos < 0 ? endPos + 8 : endPos;
                    endPos = endPos > 8 ? endPos - 8 : endPos;
                    endPos++;

                    if (endPos > 4)
                    {
                        startPos = eachNotes[1].startPosition;
                        endPos = eachNotes[0].startPosition;
                        endPos = endPos - startPos;
                        endPos = endPos < 0 ? endPos + 8 : endPos;
                        endPos = endPos > 8 ? endPos - 8 : endPos;
                        endPos++;
                    }

                    lineDrop.startPosition = startPos;
                    lineDrop.curvLength = endPos - 1;
                }

                GameObject.Find("objBPMRange").GetComponent<Text>().text = bpmlist.Min() + " - " + bpmlist.Max();
            }
            catch (Exception e)
            {
                GameObject.Find("ErrText").GetComponent<Text>().text =
                    "在第" + (timing.rawTextPositionY + 1) + "行发现问题：\n" + e.Message;
            }
    }


    private void CountNoteSum(Majson json)
    {
        foreach (var timing in json.timingList)
            foreach (var note in timing.noteList)
            {

                if (!note.isBreak)
                {
                    if (note.noteType == SimaiNoteType.Tap) ObjectCounter.tapSum++;
                    if (note.noteType == SimaiNoteType.Hold) ObjectCounter.holdSum++;
                    if (note.noteType == SimaiNoteType.TouchHold) ObjectCounter.holdSum++;
                    if (note.noteType == SimaiNoteType.Touch) ObjectCounter.touchSum++;
                    if (note.noteType == SimaiNoteType.Slide)
                    {
                        if (!note.isSlideNoHead) ObjectCounter.tapSum++;
                        if (note.isSlideBreak)
                            ObjectCounter.breakSum++;
                        else
                            ObjectCounter.slideSum++;
                    }
                }
                else if (note.isBreak)
                {
                    if (note.noteType == SimaiNoteType.Slide)
                    {
                        if (!note.isSlideNoHead) ObjectCounter.breakSum++;
                        if (note.isSlideBreak)
                            ObjectCounter.breakSum++;
                        else
                            ObjectCounter.slideSum++;
                    }
                    else
                    {
                        ObjectCounter.breakSum++;
                    }
                }
                else
                {
                    if (note.noteType == SimaiNoteType.Slide)
                    {
                        ObjectCounter.slideSum++;
                    }
                }
            }
    }

    private void CountNoteCount(List<SimaiNote> timing)
    {
        foreach (var note in timing)
        {
            if (!note.isBreak)
            {
                if (note.noteType == SimaiNoteType.Tap) ObjectCounter.tapCount++;
                if (note.noteType == SimaiNoteType.Hold) ObjectCounter.holdCount++;
                if (note.noteType == SimaiNoteType.TouchHold) ObjectCounter.holdCount++;
                if (note.noteType == SimaiNoteType.Touch) ObjectCounter.touchCount++;
                if (note.noteType == SimaiNoteType.Slide)
                {
                    if (!note.isSlideNoHead) ObjectCounter.tapCount++;
                    if (note.isSlideBreak)
                        ObjectCounter.breakCount++;
                    else
                        ObjectCounter.slideCount++;
                }
            }
            else if (note.isBreak)
            {
                if (note.noteType == SimaiNoteType.Slide)
                {
                    if (!note.isSlideNoHead) ObjectCounter.breakCount++;
                    if (note.isSlideBreak)
                        ObjectCounter.breakCount++;
                    else
                        ObjectCounter.slideCount++;
                }
                else
                {
                    ObjectCounter.breakCount++;
                }
            }
            else
            {
                if (note.noteType == SimaiNoteType.Slide)
                {
                    ObjectCounter.slideCount++;
                }
            }
        }
    }

    private void InstantiateStarGroup(SimaiTimingPoint timing, SimaiNote note, int sort, double lastNoteTime)
    {
        int charIntParse(char c)
        {
            return c - '0';
        }

        var subSlide = new List<SimaiNote>();
        var subBarCount = new List<int>();
        var sumBarCount = 0;

        var noteContent = note.noteContent;
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
                slidePart.noteType = SimaiNoteType.Slide;
                slidePart.startPosition = latestStartIndex;
                if (slideTypeChar == "V")
                {
                    // 转折星星
                    var middlePos = noteContent[ptr++];
                    var endPos = noteContent[ptr++];

                    slidePart.noteContent = latestStartIndex + slideTypeChar + middlePos + endPos;
                    latestStartIndex = charIntParse(endPos);
                }
                else
                {
                    // 其他普通星星
                    // 额外检查pp和qq
                    if (noteContent[ptr] == slideTypeChar[0]) slideTypeChar += noteContent[ptr++];
                    var endPos = noteContent[ptr++];

                    slidePart.noteContent = latestStartIndex + slideTypeChar + endPos;
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
                        slidePart.noteContent += noteContent[ptr++];
                    slidePart.noteContent += noteContent[ptr++];
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

                string slideShape = detectShapeFromText(slidePart.noteContent);
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
            o.isBreak = note.isBreak;
            o.isEx = note.isEx;
            o.isSlideBreak = note.isSlideBreak;
            o.isSlideNoHead = true;
            o.canSVAffect = note.canSVAffect;
            o.kSkin = note.kSkin;
            o.isUnplayable = note.isUnplayable;
        });
        subSlide[0].isSlideNoHead = note.isSlideNoHead;
        //double wholetime = 0;

        if (specTimeFlag == 1 || specTimeFlag == 0)
            // 如果到结束还是1 那说明没有一个指定了时长 报错
            throw new Exception("组合星星有错误\nwSLIDE CHAIN ERROR");
        // 此时 flag为2表示每条指定语法 为3表示整体指定语法

        if (specTimeFlag == 3)
        {
            // 整体指定语法 使用slideTime来计算
            var tempBarCount = 0;
            for (var i = 0; i < subSlide.Count; i++)
            {
                subSlide[i].slideStartTime = note.slideStartTime + (double)tempBarCount / sumBarCount * note.slideTime;
                subSlide[i].slideTime = (double)subBarCount[i] / sumBarCount * note.slideTime;
                tempBarCount += subBarCount[i];
            }
            //wholetime = note.slideTime;
        }
        else
        {
            // 每条指定语法

            // 获取时长的子函数
            double getTimeFromBeats(string noteText, float currentBpm)
            {
                var startIndex = noteText.IndexOf('[');
                var overIndex = noteText.IndexOf(']');
                var innerString = noteText.Substring(startIndex + 1, overIndex - startIndex - 1);
                var timeOneBeat = 1d / (currentBpm / 60d);
                if (innerString.Count(o => o == '#') == 1)
                {
                    var times = innerString.Split('#');
                    if (times[1].Contains(':'))
                    {
                        innerString = times[1];
                        timeOneBeat = 1d / (double.Parse(times[0]) / 60d);
                    }
                    else
                    {
                        return double.Parse(times[1]);
                    }
                }

                if (innerString.Count(o => o == '#') == 2)
                {
                    var times = innerString.Split('#');
                    return double.Parse(times[2]);
                }

                var numbers = innerString.Split(':');
                var divide = int.Parse(numbers[0]);
                var count = int.Parse(numbers[1]);


                return timeOneBeat * 4d / divide * count;
            }

            double tempSlideTime = 0;
            for (var i = 0; i < subSlide.Count; i++)
            {
                subSlide[i].slideStartTime = note.slideStartTime + tempSlideTime;
                subSlide[i].slideTime = getTimeFromBeats(subSlide[i].noteContent, timing.currentBpm);
                tempSlideTime += subSlide[i].slideTime;
            }
            //wholetime = tempSlideTime;
        }
        /*for (int i = 0; i < subSlide.Count; i++)
        {
            subSlide[i].lastSlideTime = wholetime;
        }*/

        for (var i = subSlide.Count - 1; i >= 0; i--)
            if (note.noteContent.Contains('w')) //wifi
                InstantiateWifi(timing, subSlide[i], i != 0, i == subSlide.Count - 1);
            else
            {
                GameObject lastSlide = null;
                InstantiateStar(timing, subSlide[i], i != 0, i == subSlide.Count - 1, ref lastSlide);
            }
    }

    private void InstantiateWifi(SimaiTimingPoint timing, SimaiNote note, bool isGroupPart, bool isGroupPartEnd)
    {
        var str = note.noteContent.Substring(0, 3);
        var digits = str.Split('w');
        var startPos = int.Parse(digits[0]);
        var endPos = int.Parse(digits[1]);
        endPos = endPos - startPos;
        endPos = endPos < 0 ? endPos + 8 : endPos;
        endPos = endPos > 8 ? endPos - 8 : endPos;
        endPos++;

        var GOnote = Instantiate(starPrefab, notes.transform);
        var NDCompo = GOnote.GetComponent<StarDrop>();

        var skins = note.kSkin.Split(';');

        var starSkin = skins.Length > 2 ? skins[1] : skins[0];
        string slideSkin;
        if (skins.Length == 2) slideSkin = skins[1];
        else if (skins.Length >= 3) slideSkin = skins[2];
        else slideSkin = skins[0];

        // note的图层顺序
        NDCompo.noteSortOrder = noteSortOrder;
        noteSortOrder -= NOTE_LAYER_COUNT[note.noteType];

        if (string.IsNullOrEmpty(note.kSkin))
        {
            NDCompo.tapSpr = customSkin.Star;
            NDCompo.eachSpr = customSkin.Star_Each;
            NDCompo.breakSpr = customSkin.Star_Break;
            NDCompo.exSpr = customSkin.Star_Ex;

            NDCompo.tapSpr_Double = customSkin.Star_Double;
            NDCompo.eachSpr_Double = customSkin.Star_Each_Double;
            NDCompo.breakSpr_Double = customSkin.Star_Break_Double;
            NDCompo.exSpr_Double = customSkin.Star_Ex_Double;
        }
        else
        {
            NDCompo.tapSpr = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, skins[0]));
            NDCompo.eachSpr = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, skins[0].Insert(skins[0].Length - 4, "_each")));
            NDCompo.breakSpr = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, skins[0].Insert(skins[0].Length - 4, "_break")));
            NDCompo.exSpr = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, skins[0].Insert(skins[0].Length - 4, "_ex")));

            NDCompo.tapSpr_Double = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, skins[0].Insert(skins[0].Length - 4, "_double")));
            NDCompo.eachSpr_Double = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, skins[0].Insert(skins[0].Length - 4, "_each_double")));
            NDCompo.breakSpr_Double = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, skins[0].Insert(skins[0].Length - 4, "_break_double")));
            NDCompo.exSpr_Double = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, skins[0].Insert(skins[0].Length - 4, "_ex_double")));
        }

        NDCompo.BreakShine = BreakShine;

        NDCompo.rotateSpeed = (float)note.slideTime;
        NDCompo.isEX = note.isEx;
        NDCompo.isBreak = note.isBreak;
        NDCompo.isUnplayable = note.isUnplayable;

        var slideWifi = Instantiate(slidePrefab[SLIDE_PREFAB_MAP["wifi"]], notes.transform);
        slideWifi.SetActive(false);
        NDCompo.slide = slideWifi;
        var WifiCompo = slideWifi.GetComponent<WifiDrop>();

        if (string.IsNullOrEmpty(note.kSkin))
        {
            WifiCompo.normalStar = customSkin.Star;
            WifiCompo.eachStar = customSkin.Star_Each;
            WifiCompo.breakStar = customSkin.Star_Break;
        }
        else
        {
            WifiCompo.normalStar = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, starSkin));
            WifiCompo.eachStar = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, starSkin.Insert(starSkin.Length - 4, "_each")));
            WifiCompo.breakStar = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, starSkin.Insert(starSkin.Length - 4, "_break")));
        }
        WifiCompo.judgeBreakShine = JudgeBreakShine;
        WifiCompo.breakMaterial = breakMaterial;
        WifiCompo.slideShine = BreakShine;
        WifiCompo.areaStep = new List<int>(SLIDE_AREA_STEP_MAP["wifi"]);
        WifiCompo.slideConst = SLIDE_AREA_CONST["wifi"];
        WifiCompo.smoothSlideAnime = smoothSlideAnime;

        if (string.IsNullOrEmpty(note.kSkin))
        {
            Array.Copy(customSkin.Wifi, WifiCompo.normalSlide, 11);
            Array.Copy(customSkin.Wifi_Each, WifiCompo.eachSlide, 11);
            Array.Copy(customSkin.Wifi_Break, WifiCompo.breakSlide, 11);
        }
        else
        {
            Sprite[] wifi = new Sprite[11];
            Sprite[] wifi_each = new Sprite[11];
            Sprite[] wifi_break = new Sprite[11];
            for (var j = 0; j < 11; j++)
            {
                wifi[j] = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, slideSkin.Insert(slideSkin.Length - 4, "_" + j)));
                wifi_each[j] = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, slideSkin.Insert(slideSkin.Length - 4, "_each_" + j)));
                wifi_break[j] = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, slideSkin.Insert(slideSkin.Length - 4, "_break_" + j)));
            }

            Array.Copy(wifi, WifiCompo.normalSlide, 11);
            Array.Copy(wifi_each, WifiCompo.eachSlide, 11);
            Array.Copy(wifi_break, WifiCompo.breakSlide, 11);
        }

        if (timing.noteList.Count > 1)
        {
            if (timing.noteList.FindAll(o => !o.isSlideNoHead).Count > 1)
            {
                NDCompo.isEach = true;
                NDCompo.isDouble = false;
            }
            if (timing.noteList.FindAll(//当前时间星星找双押
                    o => o.noteType == SimaiNoteType.Slide).Count
                > 1)
            {
                WifiCompo.isEach = true;
            }
            var count = timing.noteList.FindAll(
                o => o.noteType == SimaiNoteType.Slide &&
                     o.startPosition == note.startPosition).Count;
            if (count > 1) //有同起点
            {
                NDCompo.isDouble = true;
                if (count == timing.noteList.Count)
                    NDCompo.isEach = false;
                else
                    NDCompo.isEach = true;
            }
        }

        WifiCompo.isBreak = note.isSlideBreak;
        WifiCompo.canSVAffect = note.canSVAffect;
        WifiCompo.isGroupPart = isGroupPart;
        WifiCompo.isGroupPartEnd = isGroupPartEnd;
        WifiCompo.isUnplayable = note.isUnplayable;
        //WifiCompo.lastSlideTime = note.lastSlideTime;

        NDCompo.isNoHead = note.isSlideNoHead;
        NDCompo.time = (float)timing.time;
        NDCompo.startPosition = note.startPosition;
        NDCompo.speed = noteSpeed * timing.HSpeed;
        NDCompo.canSVAffect = note.canSVAffect;

        WifiCompo.isJustR = detectJustType(note.noteContent,out endPos);
        WifiCompo.endPosition = endPos;
        WifiCompo.speed = noteSpeed * timing.HSpeed;
        WifiCompo.timeStart = (float)timing.time;
        WifiCompo.startPosition = note.startPosition;
        WifiCompo.time = (float)note.slideStartTime;
        WifiCompo.LastFor = (float)note.slideTime;
        WifiCompo.sortIndex = slideLayer;
        slideLayer += 5;
    }

    private void InstantiateStar(SimaiTimingPoint timing, SimaiNote note, bool isGroupPart, bool isGroupPartEnd, ref GameObject lastSlide)
    {
        var GOnote = Instantiate(starPrefab, notes.transform);
        var NDCompo = GOnote.GetComponent<StarDrop>();

        // note的图层顺序
        NDCompo.noteSortOrder = noteSortOrder;
        noteSortOrder -= NOTE_LAYER_COUNT[note.noteType];

        var skins = note.kSkin.Split(';');

        var starSkin = skins.Length > 2 ? skins[1] : skins[0];
        string slideSkin;
        if (skins.Length == 2) slideSkin = skins[1];
        else if (skins.Length >= 3) slideSkin = skins[2];
        else slideSkin = skins[0];

        if (string.IsNullOrEmpty(note.kSkin))
        {
            NDCompo.tapSpr = customSkin.Star;
            NDCompo.eachSpr = customSkin.Star_Each;
            NDCompo.breakSpr = customSkin.Star_Break;
            NDCompo.exSpr = customSkin.Star_Ex;

            NDCompo.tapSpr_Double = customSkin.Star_Double;
            NDCompo.eachSpr_Double = customSkin.Star_Each_Double;
            NDCompo.breakSpr_Double = customSkin.Star_Break_Double;
            NDCompo.exSpr_Double = customSkin.Star_Ex_Double;
        }
        else
        {
            NDCompo.tapSpr = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, skins[0]));
            NDCompo.eachSpr = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, skins[0].Insert(skins[0].Length - 4, "_each")));
            NDCompo.breakSpr = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, skins[0].Insert(skins[0].Length - 4, "_break")));
            NDCompo.exSpr = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, skins[0].Insert(skins[0].Length - 4, "_ex")));

            NDCompo.tapSpr_Double = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, skins[0].Insert(skins[0].Length - 4, "_double")));
            NDCompo.eachSpr_Double = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, skins[0].Insert(skins[0].Length - 4, "_each_double")));
            NDCompo.breakSpr_Double = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, skins[0].Insert(skins[0].Length - 4, "_break_double")));
            NDCompo.exSpr_Double = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, skins[0].Insert(skins[0].Length - 4, "_ex_double")));
        }

        NDCompo.BreakShine = BreakShine;

        NDCompo.rotateSpeed = (float)note.slideTime;
        NDCompo.isEX = note.isEx;
        NDCompo.isBreak = note.isBreak;
        NDCompo.isUnplayable = note.isUnplayable;
        NDCompo.canSVAffect = note.canSVAffect;

        string slideShape = detectShapeFromText(note.noteContent);
        var isMirror = false;
        if (slideShape.StartsWith("-"))
        {
            isMirror = true;
            slideShape = slideShape.Substring(1);
        }
        int slideIndex = SLIDE_PREFAB_MAP[slideShape];

        var slide = Instantiate(slidePrefab[slideIndex], notes.transform);
        var slide_star = Instantiate(star_slidePrefab, notes.transform);

        slide_star.GetComponent<SpriteRenderer>().sprite = string.IsNullOrEmpty(note.kSkin) ? 
            customSkin.Star : SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, starSkin));
        slide_star.SetActive(false);
        slide.SetActive(false);
        NDCompo.slide = slide;
        var SliCompo = slide.AddComponent<SlideDrop>();

        if (string.IsNullOrEmpty(note.kSkin))
        {
            SliCompo.spriteNormal = customSkin.Slide;
            SliCompo.spriteEach = customSkin.Slide_Each;
            SliCompo.spriteBreak = customSkin.Slide_Break;
        }
        else
        {
            SliCompo.spriteNormal = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, slideSkin));
            SliCompo.spriteEach = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, slideSkin.Insert(slideSkin.Length - 4, "_each")));
            SliCompo.spriteBreak = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, slideSkin.Insert(slideSkin.Length - 4, "_break")));
        }
        SliCompo.slideShine = BreakShine;
        SliCompo.breakMaterial = breakMaterial;
        SliCompo.judgeBreakShine = JudgeBreakShine;
        SliCompo.areaStep = new List<int>(SLIDE_AREA_STEP_MAP[slideShape]);
        SliCompo.slideConst = SLIDE_AREA_CONST[slideShape]; 
        SliCompo.smoothSlideAnime = smoothSlideAnime;

        if (timing.noteList.Count > 1)//当前时间找双押
        {
            if (timing.noteList.FindAll(o => !o.isSlideNoHead).Count > 1) NDCompo.isEach = true;
            if (timing.noteList.FindAll(//当前时间星星找双押
                    o => o.noteType == SimaiNoteType.Slide).Count
                > 1)
            {
                SliCompo.isEach = true;
                slide_star.GetComponent<SpriteRenderer>().sprite = string.IsNullOrEmpty(note.kSkin) ?
            customSkin.Star_Each : SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, starSkin.Insert(starSkin.Length - 4, "_each")));
            }

            var count = timing.noteList.FindAll(//找同头的
                o => o.noteType == SimaiNoteType.Slide &&
                     o.startPosition == note.startPosition).Count;
            if (count > 1)
            {
                NDCompo.isDouble = true;
                if (count == timing.noteList.Count)//只有这俩
                    NDCompo.isEach = false;
                else
                    NDCompo.isEach = true;
            }
        }

        SliCompo.isBreak = note.isSlideBreak;
        SliCompo.isUnplayable = note.isUnplayable;
        SliCompo.canSVAffect = note.canSVAffect;
        SliCompo.isGroupPart = isGroupPart;
        SliCompo.isGroupPartEnd = isGroupPartEnd;
        //SliCompo.lastSlideTime = note.lastSlideTime;
        if (note.isSlideBreak) slide_star.GetComponent<SpriteRenderer>().sprite = string.IsNullOrEmpty(note.kSkin) ?
            customSkin.Star_Break : SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, starSkin.Insert(starSkin.Length - 4, "_break")));

        NDCompo.isNoHead = note.isSlideNoHead;
        NDCompo.time = (float)timing.time;
        NDCompo.startPosition = note.startPosition;
        NDCompo.speed = noteSpeed * timing.HSpeed;


        SliCompo.isMirror = isMirror;
        SliCompo.isJustR = detectJustType(note.noteContent,out int endPos);
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
        SliCompo.timeStar = (float)timing.time;
        SliCompo.startPosition = note.startPosition;
        SliCompo.star_slide = slide_star;
        SliCompo.time = (float)note.slideStartTime;
        SliCompo.LastFor = (float)note.slideTime;
        //SliCompo.sortIndex = -7000 + (int)((lastNoteTime - timing.time) * -100) + sort * 5;
        SliCompo.sortIndex = slideLayer++;
        slideLayer += 5;

        if (lastSlide != null)
            lastSlide.GetComponent<SlideDrop>().parentSlide = slide;
        lastSlide = slide;
    }

    private bool detectJustType(string content,out int endPos)
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
}