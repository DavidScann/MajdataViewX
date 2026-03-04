using Assets.Scripts.Notes;
using MajSimai;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
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

    // 专门为了处理神秘ReadOnlySpan入参。。。虽然在Play+Neo没毛，但向下就有点搞
    public class MajsonConverter : JsonConverter<SimaiTimingPoint>
    {
        public override SimaiTimingPoint ReadJson(JsonReader reader, Type objectType, SimaiTimingPoint existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            double timing = 0; float bpm = 0, hspeed = 1f, sveloc = 1f;
            int textPosX = 0, textPosY = 0, rawPos = 0;
            SimaiNote[] notes = null;
            string rawString = null;

            // 手动流式读取，不生成 JObject
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject) break;
                if (reader.TokenType != JsonToken.PropertyName) continue;

                string propName = reader.Value.ToString();
                reader.Read();

                switch (propName)
                {
                    case "Timing": timing = Convert.ToDouble(reader.Value); break;
                    case "Bpm": bpm = Convert.ToSingle(reader.Value); break;
                    case "HSpeed": hspeed = Convert.ToSingle(reader.Value); break;
                    case "SVeloc": sveloc = Convert.ToSingle(reader.Value); break;
                    case "RawTextPositionX": textPosX = Convert.ToInt32(reader.Value); break;
                    case "RawTextPositionY": textPosY = Convert.ToInt32(reader.Value); break;
                    case "RawTextPosition": rawPos = Convert.ToInt32(reader.Value); break;
                    case "Notes": notes = serializer.Deserialize<SimaiNote[]>(reader); break;
                    case "RawContent":
                        rawString = (string)reader.Value;
                        break;
                }
            }

            return new SimaiTimingPoint(
                timing, notes, rawString.AsSpan(),
                textPosX, textPosY, bpm, hspeed, sveloc, rawPos
            );
        }

        public override void WriteJson(JsonWriter writer, SimaiTimingPoint value, JsonSerializer serializer) => throw new NotImplementedException();
    }

    public void LoadJson(string json, float ignoreOffset)
    {
        var settings = new JsonSerializerSettings();
        settings.Converters.Add(new MajsonConverter());
        var loadedData = JsonConvert.DeserializeObject<Majson>(json, settings);

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

        var lastNoteTime = loadedData.timingList.Last().Timing;
        float lastBPM = 0;

        List<float> bpmlist = new();

        foreach (var timing in loadedData.timingList)
            try
            {
                if (timeProvider.SVList.Count == 0 || timeProvider.SVList[^1] != timing.SVeloc)
                {
                    timeProvider.SVList.Add(timing.SVeloc);
                    Debug.Log(timing.SVeloc);
                    timeProvider.SVTime.Add((float)timing.Timing);
                }
                if (timing.Timing < ignoreOffset)
                {
                    CountNoteCount(timing.Notes.ToList());
                    continue;
                }
                if (timing.Bpm != lastBPM)
                {
                    var GOnote = Instantiate(cmdPrefab);
                    var NDCompo = GOnote.GetComponent<CmdDrop>();
                    NDCompo.time = (float)timing.Timing;
                    NDCompo.times = 1;
                    NDCompo.Handler = () =>
                    {
                        GameObject.Find("objBPM").GetComponent<Text>().text = Math.Truncate(timing.Bpm).ToString();
                    };
                    lastBPM = timing.Bpm;

                    bpmlist.Add(timing.Bpm);
                }

                for (var i = 0; i < timing.Notes.Length; i++)
                {
                    var note = timing.Notes[i];
                    if (note.Type == SimaiNoteType.Tap)
                    {
                        var GOnote = Instantiate(tapPrefab, notes.transform);
                        var NDCompo = GOnote.GetComponent<TapDrop>();

                        // note的图层顺序
                        NDCompo.noteSortOrder = noteSortOrder;
                        noteSortOrder -= NOTE_LAYER_COUNT[note.Type];

                        if (note.IsForceStar)
                        {
                            if (note.KustomSkin == null) //没有带双引号，用原来的
                            {
                                NDCompo.normalSpr = customSkin.Star;
                                NDCompo.eachSpr = customSkin.Star_Each;
                                NDCompo.breakSpr = customSkin.Star_Break;
                                NDCompo.exSpr = customSkin.Star_Ex;
                            }
                            else
                            {
                                var kSkin = note.KustomSkin.Split(':');
                                var notePath = Path.Combine(kPath, kSkin[0]);
                                var noteSprite = SpriteLoader.LoadSpriteFromFile(notePath);
                                NDCompo.normalSpr = noteSprite;
                                NDCompo.eachSpr = noteSprite;
                                NDCompo.breakSpr = noteSprite;
                                //ex情况下读取_ex作为ex框，直接在路径进行插入省时间
                                NDCompo.exSpr = SpriteLoader.LoadSpriteFromFile(InsertNameSuffix(notePath, "_ex"));

                                if (kSkin.Length > 1)
                                {
                                    NDCompo.lineSpriteRender.sprite = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, kSkin[1]));
                                }
                                else
                                {
                                    NDCompo.tapLine = starLine;
                                }
                            }

                            NDCompo.isFakeStarRotate = note.IsFakeRotate;
                        }
                        else
                        {
                            if (note.KustomSkin == null)
                            {
                                NDCompo.normalSpr = customSkin.Tap;
                                NDCompo.breakSpr = customSkin.Tap_Break;
                                NDCompo.eachSpr = customSkin.Tap_Each;
                                NDCompo.exSpr = customSkin.Tap_Ex;
                            }
                            else
                            {
                                var kSkin = note.KustomSkin.Split(':');
                                var notePath = Path.Combine(kPath, kSkin[0]);
                                var noteSprite = SpriteLoader.LoadSpriteFromFile(notePath);
                                NDCompo.normalSpr = noteSprite;
                                NDCompo.eachSpr = noteSprite;
                                NDCompo.breakSpr = noteSprite;
                                NDCompo.exSpr = SpriteLoader.LoadSpriteFromFile(InsertNameSuffix(notePath, "_ex"));

                                if (kSkin.Length > 1)
                                {
                                    NDCompo.lineSpriteRender.GetComponent<SpriteRenderer>().sprite = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, kSkin[1]));
                                }
                            }

                            //else //默认就是原来的tapline
                        }

                        NDCompo.BreakShine = BreakShine;

                        if (timing.Notes.Length > 1) NDCompo.isEach = true;
                        NDCompo.isBreak = note.IsBreak;
                        NDCompo.isEX = note.IsEx;
                        NDCompo.isUnplayable = note.IsUnplayable;
                        NDCompo.canSVAffect = note.UsingSV;
                        NDCompo.time = (float)timing.Timing;
                        NDCompo.startPosition = note.StartPosition;
                        NDCompo.speed = noteSpeed * timing.HSpeed;
                    }
                    else if (note.Type == SimaiNoteType.Hold)
                    {
                        var GOnote = Instantiate(holdPrefab, notes.transform);
                        var NDCompo = GOnote.GetComponent<HoldDrop>();

                        // note的图层顺序
                        NDCompo.noteSortOrder = noteSortOrder;
                        noteSortOrder -= NOTE_LAYER_COUNT[note.Type];

                        if (note.KustomSkin == null)
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
                            var kSkin = note.KustomSkin.Split(':');
                            var notePath = Path.Combine(kPath, kSkin[0]);
                            var noteSprite = SpriteLoader.LoadSpriteFromFile(notePath);
                            var onPath = Path.Combine(kPath, InsertNameSuffix(kSkin[0], "_on"));
                            var offPath = Path.Combine(kPath, InsertNameSuffix(kSkin[0], "_off"));
                            if (File.Exists(onPath))
                            {
                                var onSprite = SpriteLoader.LoadSpriteFromFile(onPath);
                                NDCompo.holdOnSpr = onSprite;
                                NDCompo.eachHoldOnSpr = onSprite;
                                NDCompo.breakHoldOnSpr = onSprite;
                            }
                            else
                            {
                                NDCompo.holdOnSpr = noteSprite;
                                NDCompo.eachHoldOnSpr = noteSprite;
                                NDCompo.breakHoldOnSpr = noteSprite;

                            }
                            if (File.Exists(offPath))
                                NDCompo.holdOffSpr = SpriteLoader.LoadSpriteFromFile(offPath);
                            else
                                NDCompo.holdOffSpr = noteSprite;


                            NDCompo.tapSpr = noteSprite;
                            NDCompo.eachSpr = noteSprite;
                            NDCompo.breakSpr = noteSprite;
                            NDCompo.exSpr = SpriteLoader.LoadSpriteFromFile(InsertNameSuffix(notePath, "_ex"));

                            if (kSkin.Length > 1)
                            {
                                var guideNames = kSkin[1].Split(';');
                                NDCompo.lineSpriteRender.GetComponent<SpriteRenderer>().sprite = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, guideNames[0]));
                                if (guideNames.Length > 1)
                                {
                                    var endSprite = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, guideNames[1]));
                                    NDCompo.holdEachEnd = endSprite;
                                    NDCompo.holdBreakEnd = endSprite;
                                }
                            }
                        }

                        NDCompo.HoldShine = HoldShine;
                        NDCompo.BreakShine = BreakShine;

                        if (timing.Notes.Length > 1) NDCompo.isEach = true;
                        NDCompo.time = (float)timing.Timing;
                        NDCompo.LastFor = (float)note.HoldTime;
                        NDCompo.startPosition = note.StartPosition;
                        NDCompo.speed = noteSpeed * timing.HSpeed;
                        NDCompo.isEX = note.IsEx;
                        NDCompo.isBreak = note.IsBreak;
                        NDCompo.isUnplayable = note.IsUnplayable;
                        NDCompo.canSVAffect = note.UsingSV;
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
                        NDCompo.areaPosition = note.TouchArea;
                        NDCompo.startPosition = note.StartPosition;
                        NDCompo.TouchPointEachSprite = customSkin.TouchPoint_Each;

                        if (timing.Notes.Length > 1) NDCompo.isEach = true;


                        if (note.KustomSkin == null)
                        {
                            Array.Copy(customSkin.TouchHold, NDCompo.TouchHoldSprite, 5);
                            NDCompo.TouchPointSprite = customSkin.TouchPoint;
                            NDCompo.TouchPointEachSprite = customSkin.TouchPoint_Each;
                            NDCompo.TouchHoldBorderMiss = customSkin.TouchHoldBorderMiss;
                        }
                        else
                        {
                            Sprite[] touchHold = new Sprite[5];
                            touchHold[0] = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, InsertNameSuffix(note.KustomSkin, "_0")));
                            touchHold[1] = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, InsertNameSuffix(note.KustomSkin, "_1")));
                            touchHold[2] = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, InsertNameSuffix(note.KustomSkin, "_2")));
                            touchHold[3] = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, InsertNameSuffix(note.KustomSkin, "_3")));
                            touchHold[4] = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, note.KustomSkin));

                            Array.Copy(touchHold, NDCompo.TouchHoldSprite, 5);
                            var pointSprite = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, InsertNameSuffix(note.KustomSkin, "_point")));
                            NDCompo.TouchPointSprite = pointSprite;
                            NDCompo.TouchPointEachSprite = pointSprite;
                            NDCompo.TouchHoldBorderMiss = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, InsertNameSuffix(note.KustomSkin, "_off")));
                        }
                        NDCompo.isUnplayable = note.IsUnplayable;
                        NDCompo.canSVAffect = note.UsingSV;
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

                        if (note.KustomSkin == null)
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
                            var kSkin = note.KustomSkin.Split(':');
                            var notePath = Path.Combine(kPath, kSkin[0]);
                            if (kSkin.Length > 1)
                            {
                                var borderPath = Path.Combine(kPath, kSkin[1]);
                                var border2Sprite = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, InsertNameSuffix(borderPath, "_border_2")));
                                var border3Sprite = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, InsertNameSuffix(borderPath, "_border_3")));
                                NDCompo.multTouchNormalSprite[0] = border2Sprite;
                                NDCompo.multTouchNormalSprite[1] = border3Sprite;
                                NDCompo.multTouchEachSprite[0] = border2Sprite;
                                NDCompo.multTouchEachSprite[1] = border3Sprite;
                            }
                            else
                            {
                                Array.Copy(customSkin.TouchBorder, NDCompo.multTouchNormalSprite, 2);
                                Array.Copy(customSkin.TouchBorder_Each, NDCompo.multTouchEachSprite, 2);
                            }

                            var fanSprite = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, notePath));
                            NDCompo.fanNormalSprite = fanSprite;
                            NDCompo.fanEachSprite = fanSprite;

                            var pointSprite = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, InsertNameSuffix(notePath, "_point")));
                            NDCompo.pointNormalSprite = pointSprite;
                            NDCompo.pointEachSprite = pointSprite;

                            NDCompo.justSprite = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, InsertNameSuffix(notePath, "_just")));
                        }

                        if (timing.Notes.Length > 1)
                            NDCompo.isEach = true;
                        NDCompo.speed = touchSpeed * timing.HSpeed;
                        NDCompo.isFirework = note.IsHanabi;
                        NDCompo.isUnplayable = note.IsUnplayable;
                        NDCompo.canSVAffect = note.UsingSV;
                    }
                    else if (note.Type == SimaiNoteType.Slide)
                    {
                        string kPattern = @"k""([^""]+\.png)""(?:'([^']+\.wav)')?|k'([^']+\.wav)'"; // k"*.png" or k'*.wav' or k"*.png"'*.wav'
                        note.RawContent = Regex.Replace(note.RawContent, kPattern, "");
                        InstantiateStarGroup(timing, note, i, lastNoteTime); // 星星组
                    }
                    else if (note.Type == SimaiNoteType.Command)
                    {
                        string[] cmd = note.RawContent[1..^1].Split('.');
                        if (cmd[0] == "data")
                        {
                            var GOnote = Instantiate(cmdPrefab);
                            var NDCompo = GOnote.GetComponent<CmdDrop>();
                            NDCompo.time = (float)timing.Timing;
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
                            NDCompo.time = (float)timing.Timing;

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
                        else if (cmd[0] == "scene")
                        {
                            var GOnote = Instantiate(cmdPrefab);
                            var NDCompo = GOnote.GetComponent<CmdDrop>();
                            NDCompo.time = (float)timing.Timing;

                            if (cmd[1] == "border")
                            {
                                if (cmd[2] == "hide")
                                {
                                    NDCompo.times = 1;
                                    NDCompo.Handler = () =>
                                    {
                                        GameObject.Find("RawImageR").SetActive(false);
                                        GameObject.Find("RawImageL").SetActive(false);
                                        GameObject.Find("RawImageT").SetActive(false);
                                        GameObject.Find("RawImageB").SetActive(false);
                                        GameObject.Find("1080Circle_Rev").SetActive(false);
                                    };
                                }
                                else if (cmd[2] == "show")
                                {
                                    NDCompo.times = 1;
                                    NDCompo.Handler = () =>
                                    {
                                        GameObject.Find("RawImageR").SetActive(true);
                                        GameObject.Find("RawImageL").SetActive(true);
                                        GameObject.Find("RawImageT").SetActive(true);
                                        GameObject.Find("RawImageB").SetActive(true);
                                        GameObject.Find("1080Circle_Rev").SetActive(true);
                                    };
                                }
                            }
                            else if (cmd[1] == "circle")
                            {
                                if (cmd[2] == "reset")
                                {
                                    NDCompo.times = 1;
                                    NDCompo.Handler = () =>
                                    {
                                        Camera.main.transform.SetPositionAndRotation(new Vector3(0, 0, -10), Quaternion.identity);
                                        Camera.main.orthographicSize = 5;
                                    };
                                }
                                else if (float.TryParse(cmd[3] + (cmd.Length == 5 ? '.' + cmd[4] : ""), out var value))
                                {
                                    var pos = Camera.main.transform.position;
                                    if (cmd[2] == "x")
                                    {
                                        NDCompo.times = 1;
                                        NDCompo.Handler = () =>
                                            Camera.main.transform.position = new Vector3(-value, pos.y, pos.z);
                                    }
                                    else if (cmd[2] == "y")
                                    {
                                        NDCompo.times = 1;
                                        NDCompo.Handler = () =>
                                            Camera.main.transform.position = new Vector3(pos.x, -value, pos.z);
                                    }
                                    else if (cmd[2] == "rot")
                                    {
                                        NDCompo.times = 1;
                                        NDCompo.Handler = () =>
                                            Camera.main.transform.Rotate(new Vector3(0, 0, value));
                                    }
                                    else if (cmd[2] == "scale")
                                    {
                                        NDCompo.times = 1;
                                        NDCompo.Handler = () =>
                                            Camera.main.orthographicSize = 5 / value;
                                    }
                                }
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
                                        subSV.SVTime.Add((float)timing.Timing);
                                    }
                                }
                                else
                                {
                                    var subSV = Instantiate(subSVPrefab).GetComponent<SubSV>();
                                    SubSVList.Add(count, subSV);
                                    if (subSV.SVList.Count == 0 || subSV.SVList[^1] != speed)
                                    {
                                        subSV.SVList.Add(speed);
                                        subSV.SVTime.Add((float)timing.Timing);
                                    }
                                }
                            }
                        }
                    }
                }

                var eachNotes = Array.FindAll(timing.Notes, o =>
                    o.Type != SimaiNoteType.Touch && o.Type != SimaiNoteType.TouchHold && !o.IsSlideNoHead);
                if (eachNotes.Length > 1) //有多个非touchnote
                {
                    var startPos = eachNotes[0].StartPosition;
                    var endPos = eachNotes[1].StartPosition;
                    endPos = endPos - startPos;
                    if (endPos == 0) continue;

                    var line = Instantiate(eachLine, notes.transform);
                    var lineDrop = line.GetComponent<EachLineDrop>();

                    lineDrop.time = (float)timing.Timing;
                    lineDrop.speed = noteSpeed * timing.HSpeed;
                    lineDrop.canSVAffect = 1;
                    foreach (var eachNote in eachNotes)
                    {
                        if (eachNote.UsingSV != 1)
                        {
                            lineDrop.canSVAffect = eachNote.UsingSV;
                        }
                    }

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

                GameObject.Find("objBPMRange").GetComponent<Text>().text = bpmlist.Min() + " - " + bpmlist.Max();
            }
            catch (Exception e)
            {
                GameObject.Find("ErrText").GetComponent<Text>().text =
                    "在第" + (timing.RawTextPositionY + 1) + "行发现问题：\n" + e.Message;
                Debug.LogError(e);
            }
    }


    private void CountNoteSum(Majson json)
    {
        foreach (var timing in json.timingList)
            foreach (var note in timing.Notes)
            {
                if (!note.IsBreak)
                {
                    if (note.Type == SimaiNoteType.Tap) ObjectCounter.tapSum++;
                    if (note.Type == SimaiNoteType.Hold) ObjectCounter.holdSum++;
                    if (note.Type == SimaiNoteType.TouchHold) ObjectCounter.holdSum++;
                    if (note.Type == SimaiNoteType.Touch) ObjectCounter.touchSum++;
                    if (note.Type == SimaiNoteType.Slide)
                    {
                        if (!note.IsSlideNoHead) ObjectCounter.tapSum++;
                        if (note.IsSlideBreak)
                            ObjectCounter.breakSum++;
                        else
                            ObjectCounter.slideSum++;
                    }
                }
                else if (note.IsBreak)
                {
                    if (note.Type == SimaiNoteType.Slide)
                    {
                        if (!note.IsSlideNoHead) ObjectCounter.breakSum++;
                        if (note.IsSlideBreak)
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
                    if (note.Type == SimaiNoteType.Slide)
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
            if (!note.IsBreak)
            {
                if (note.Type == SimaiNoteType.Tap) ObjectCounter.tapCount++;
                if (note.Type == SimaiNoteType.Hold) ObjectCounter.holdCount++;
                if (note.Type == SimaiNoteType.TouchHold) ObjectCounter.holdCount++;
                if (note.Type == SimaiNoteType.Touch) ObjectCounter.touchCount++;
                if (note.Type == SimaiNoteType.Slide)
                {
                    if (!note.IsSlideNoHead) ObjectCounter.tapCount++;
                    if (note.IsSlideBreak)
                        ObjectCounter.breakCount++;
                    else
                        ObjectCounter.slideCount++;
                }
            }
            else if (note.IsBreak)
            {
                if (note.Type == SimaiNoteType.Slide)
                {
                    if (!note.IsSlideNoHead) ObjectCounter.breakCount++;
                    if (note.IsSlideBreak)
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
                if (note.Type == SimaiNoteType.Slide)
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
            o.IsSlideNoHead = true;
            o.UsingSV = note.UsingSV;
            o.KustomSkin = note.KustomSkin;
            o.IsUnplayable = note.IsUnplayable;
        });
        subSlide[0].IsSlideNoHead = note.IsSlideNoHead;
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
                subSlide[i].SlideStartTime = note.SlideStartTime + (double)tempBarCount / sumBarCount * note.SlideTime;
                subSlide[i].SlideTime = (double)subBarCount[i] / sumBarCount * note.SlideTime;
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
                subSlide[i].SlideStartTime = note.SlideStartTime + tempSlideTime;
                subSlide[i].SlideTime = getTimeFromBeats(subSlide[i].RawContent, timing.Bpm);
                tempSlideTime += subSlide[i].SlideTime;
            }
            //wholetime = tempSlideTime;
        }
        /*for (int i = 0; i < subSlide.Count; i++)
        {
            subSlide[i].lastSlideTime = wholetime;
        }*/

        for (var i = subSlide.Count - 1; i >= 0; i--)
            if (note.RawContent.Contains('w')) //wifi
                InstantiateWifi(timing, subSlide[i], i != 0, i == subSlide.Count - 1);
            else
            {
                GameObject lastSlide = null;
                InstantiateStar(timing, subSlide[i], i != 0, i == subSlide.Count - 1, ref lastSlide);
            }
    }

    private void InstantiateWifi(SimaiTimingPoint timing, SimaiNote note, bool isGroupPart, bool isGroupPartEnd)
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
        var NDCompo = GOnote.GetComponent<StarDrop>();

        // note的图层顺序
        NDCompo.noteSortOrder = noteSortOrder;
        noteSortOrder -= NOTE_LAYER_COUNT[note.Type];

        NDCompo.BreakShine = BreakShine;

        NDCompo.rotateSpeed = (float)note.SlideTime;
        NDCompo.isEX = note.IsEx;
        NDCompo.isBreak = note.IsBreak;
        NDCompo.isUnplayable = note.IsUnplayable;

        var slideWifi = Instantiate(slidePrefab[SLIDE_PREFAB_MAP["wifi"]], notes.transform);
        slideWifi.SetActive(false);
        NDCompo.slide = slideWifi;
        var WifiCompo = slideWifi.GetComponent<WifiDrop>();

        WifiCompo.judgeBreakShine = JudgeBreakShine;
        WifiCompo.breakMaterial = breakMaterial;
        WifiCompo.slideShine = BreakShine;
        WifiCompo.areaStep = new List<int>(SLIDE_AREA_STEP_MAP["wifi"]);
        WifiCompo.slideConst = SLIDE_AREA_CONST["wifi"];
        WifiCompo.smoothSlideAnime = smoothSlideAnime;

        if (note.KustomSkin == null)
        {
            NDCompo.tapSpr = customSkin.Star;
            NDCompo.eachSpr = customSkin.Star_Each;
            NDCompo.breakSpr = customSkin.Star_Break;
            NDCompo.exSpr = customSkin.Star_Ex;

            NDCompo.tapSpr_Double = customSkin.Star_Double;
            NDCompo.eachSpr_Double = customSkin.Star_Each_Double;
            NDCompo.breakSpr_Double = customSkin.Star_Break_Double;
            NDCompo.exSpr_Double = customSkin.Star_Ex_Double;

            WifiCompo.normalStar = customSkin.Star;
            WifiCompo.eachStar = customSkin.Star_Each;
            WifiCompo.breakStar = customSkin.Star_Break;

            Array.Copy(customSkin.Wifi, WifiCompo.normalSlide, 11);
            Array.Copy(customSkin.Wifi_Each, WifiCompo.eachSlide, 11);
            Array.Copy(customSkin.Wifi_Break, WifiCompo.breakSlide, 11);
        }
        else
        {
            var kSkin = note.KustomSkin.Split(':');
            var skins = kSkin[0].Split(';');
            string tapName;
            string starName;
            string slideName;
            if (skins.Length == 1)
            {
                tapName = starName = slideName = skins[0];
            }
            else if (skins.Length == 2)
            {
                tapName = starName = skins[0];
                slideName = skins[1];
            }
            else
            {
                tapName = skins[0];
                starName = skins[1];
                slideName = skins[2];
            }

            var tapSprite = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, tapName));

            NDCompo.tapSpr = tapSprite;
            NDCompo.eachSpr = tapSprite;
            NDCompo.breakSpr = tapSprite;

            NDCompo.exSpr = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, InsertNameSuffix(tapName, "_ex")));

            NDCompo.tapSpr_Double = tapSprite;
            NDCompo.eachSpr_Double = tapSprite;
            NDCompo.breakSpr_Double = tapSprite;

            NDCompo.exSpr_Double = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, InsertNameSuffix(tapName, "_ex")));
            if (kSkin.Length > 1)
            {
                NDCompo.lineSpriteRender.GetComponent<SpriteRenderer>().sprite = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, kSkin[1]));
            }

            var starSprite = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, starName));
            WifiCompo.normalStar = starSprite;
            WifiCompo.eachStar = starSprite;
            WifiCompo.breakStar = starSprite;

            Sprite[] wifi = new Sprite[11];
            for (var j = 0; j < 11; j++)
            {
                wifi[j] = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, InsertNameSuffix(slideName, "_" + j)));
            }

            Array.Copy(wifi, WifiCompo.normalSlide, 11);
            Array.Copy(wifi, WifiCompo.eachSlide, 11);
            Array.Copy(wifi, WifiCompo.breakSlide, 11);
        }

        if (timing.Notes.Length > 1)
        {
            if (Array.FindAll(timing.Notes, o => !o.IsSlideNoHead).Length > 1)
            {
                NDCompo.isEach = true;
                NDCompo.isDouble = false;
            }
            if (Array.FindAll(timing.Notes, //当前时间星星找双押
                o => o.Type == SimaiNoteType.Slide).Length > 1)
            {
                WifiCompo.isEach = true;
            }
            var count = Array.FindAll(timing.Notes, 
                o => o.Type == SimaiNoteType.Slide &&
                     o.StartPosition == note.StartPosition).Length;
            if (count > 1) //有同起点
            {
                NDCompo.isDouble = true;
                if (count == timing.Notes.Length)
                    NDCompo.isEach = false;
                else
                    NDCompo.isEach = true;
            }
        }

        WifiCompo.isBreak = note.IsSlideBreak;
        WifiCompo.canSVAffect = note.UsingSV;
        WifiCompo.isGroupPart = isGroupPart;
        WifiCompo.isGroupPartEnd = isGroupPartEnd;
        WifiCompo.isUnplayable = note.IsUnplayable;
        //WifiCompo.lastSlideTime = note.lastSlideTime;

        NDCompo.isNoHead = note.IsSlideNoHead;
        NDCompo.time = (float)timing.Timing;
        NDCompo.startPosition = note.StartPosition;
        NDCompo.speed = noteSpeed * timing.HSpeed;
        NDCompo.canSVAffect = note.UsingSV;

        WifiCompo.isJustR = detectJustType(note.RawContent,out endPos);
        WifiCompo.endPosition = endPos;
        WifiCompo.speed = noteSpeed * timing.HSpeed;
        WifiCompo.timeStart = (float)timing.Timing;
        WifiCompo.startPosition = note.StartPosition;
        WifiCompo.time = (float)note.SlideStartTime;
        WifiCompo.LastFor = (float)note.SlideTime;
        WifiCompo.sortIndex = slideLayer;
        slideLayer += 5;
    }

    private void InstantiateStar(SimaiTimingPoint timing, SimaiNote note, bool isGroupPart, bool isGroupPartEnd, ref GameObject lastSlide)
    {
        var GOnote = Instantiate(starPrefab, notes.transform);
        var NDCompo = GOnote.GetComponent<StarDrop>();

        // note的图层顺序
        NDCompo.noteSortOrder = noteSortOrder;
        noteSortOrder -= NOTE_LAYER_COUNT[note.Type];

        NDCompo.BreakShine = BreakShine;
        NDCompo.rotateSpeed = (float)note.SlideTime;
        NDCompo.isEX = note.IsEx;
        NDCompo.isBreak = note.IsBreak;
        NDCompo.isUnplayable = note.IsUnplayable;
        NDCompo.canSVAffect = note.UsingSV;

        string slideShape = detectShapeFromText(note.RawContent);
        var isMirror = false;
        if (slideShape.StartsWith("-"))
        {
            isMirror = true;
            slideShape = slideShape.Substring(1);
        }
        int slideIndex = SLIDE_PREFAB_MAP[slideShape];

        var slide = Instantiate(slidePrefab[slideIndex], notes.transform);
        var slide_star = Instantiate(star_slidePrefab, notes.transform);

        slide_star.SetActive(false);
        slide.SetActive(false);
        NDCompo.slide = slide;
        var SliCompo = slide.AddComponent<SlideDrop>();

        if (note.KustomSkin == null)
        {
            NDCompo.tapSpr = customSkin.Star;
            NDCompo.eachSpr = customSkin.Star_Each;
            NDCompo.breakSpr = customSkin.Star_Break;
            NDCompo.exSpr = customSkin.Star_Ex;

            NDCompo.tapSpr_Double = customSkin.Star_Double;
            NDCompo.eachSpr_Double = customSkin.Star_Each_Double;
            NDCompo.breakSpr_Double = customSkin.Star_Break_Double;
            NDCompo.exSpr_Double = customSkin.Star_Ex_Double;

            SliCompo.spriteNormal = customSkin.Slide;
            SliCompo.spriteEach = customSkin.Slide_Each;
            SliCompo.spriteBreak = customSkin.Slide_Break;
            slide_star.GetComponent<SpriteRenderer>().sprite = customSkin.Star;
        }
        else
        {
            var kSkin = note.KustomSkin.Split(':');
            var skins = kSkin[0].Split(';');
            string tapName;
            string starName;
            string slideName;
            if (skins.Length == 1)
            {
                tapName = starName = slideName = skins[0];
            }
            else if (skins.Length == 2)
            {
                tapName = starName = skins[0];
                slideName = skins[1];
            }
            else
            {
                tapName = skins[0];
                starName = skins[1];
                slideName = skins[2];
            }

            var tapSprite = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, tapName));

            NDCompo.tapSpr = tapSprite;
            NDCompo.eachSpr = tapSprite;
            NDCompo.breakSpr = tapSprite;

            NDCompo.exSpr = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, InsertNameSuffix(tapName, "_ex")));

            NDCompo.tapSpr_Double = tapSprite;
            NDCompo.eachSpr_Double = tapSprite;
            NDCompo.breakSpr_Double = tapSprite;

            NDCompo.exSpr_Double = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, InsertNameSuffix(tapName, "_ex")));

            if (kSkin.Length > 1)
            {
                NDCompo.lineSpriteRender.GetComponent<SpriteRenderer>().sprite = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, kSkin[1]));
            }

            var slideSprite = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, slideName));

            SliCompo.spriteNormal = slideSprite;
            SliCompo.spriteEach = slideSprite;
            SliCompo.spriteBreak = slideSprite;
            slide_star.GetComponent<SpriteRenderer>().sprite = SpriteLoader.LoadSpriteFromFile(Path.Combine(kPath, starName));
        }
        SliCompo.slideShine = BreakShine;
        SliCompo.breakMaterial = breakMaterial;
        SliCompo.judgeBreakShine = JudgeBreakShine;
        SliCompo.areaStep = new List<int>(SLIDE_AREA_STEP_MAP[slideShape]);
        SliCompo.slideConst = SLIDE_AREA_CONST[slideShape]; 
        SliCompo.smoothSlideAnime = smoothSlideAnime;

        if (timing.Notes.Length > 1)//当前时间找双押
        {
            if (Array.FindAll(timing.Notes, o => !o.IsSlideNoHead).Length > 1) NDCompo.isEach = true;
            if (Array.FindAll(timing.Notes, //当前时间星星找双押
                    o => o.Type == SimaiNoteType.Slide).Length > 1)
            {
                SliCompo.isEach = true;
            }

            var count = Array.FindAll(timing.Notes, //找同头的
                o => o.Type == SimaiNoteType.Slide &&
                     o.StartPosition == note.StartPosition).Length;
            if (count > 1)
            {
                NDCompo.isDouble = true;
                if (count == timing.Notes.Length)//只有这俩
                    NDCompo.isEach = false;
                else
                    NDCompo.isEach = true;
            }
        }

        SliCompo.isBreak = note.IsSlideBreak;
        SliCompo.isUnplayable = note.IsUnplayable;
        SliCompo.canSVAffect = note.UsingSV;
        SliCompo.isGroupPart = isGroupPart;
        SliCompo.isGroupPartEnd = isGroupPartEnd;
        //SliCompo.lastSlideTime = note.lastSlideTime;

        NDCompo.isNoHead = note.IsSlideNoHead;
        NDCompo.time = (float)timing.Timing;
        NDCompo.startPosition = note.StartPosition;
        NDCompo.speed = noteSpeed * timing.HSpeed;


        SliCompo.isMirror = isMirror;
        SliCompo.isJustR = detectJustType(note.RawContent,out int endPos);
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
        SliCompo.timeStar = (float)timing.Timing;
        SliCompo.startPosition = note.StartPosition;
        SliCompo.star_slide = slide_star;
        SliCompo.time = (float)note.SlideStartTime;
        SliCompo.LastFor = (float)note.SlideTime;
        //SliCompo.sortIndex = -7000 + (int)((lastNoteTime - timing.Timing) * -100) + sort * 5;
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

    private string InsertNameSuffix(string name, string suffix) 
    {
        if (name.Length >= 4)
            return name.Insert(name.Length - 4, suffix);
        else
            return "";
    }
}