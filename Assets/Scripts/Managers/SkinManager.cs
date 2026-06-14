#region

using System.IO;
using Unity.Mathematics;
using UnityEngine;

using static MajCtx;

#endregion

public class SkinManager : MonoBehaviour
{
    public Sprite Tap;
    public Sprite Tap_Each;
    public Sprite Tap_Break;
    public Sprite Tap_Ex;
    public Sprite Tap_Mine;
    public Sprite Tap_Break_Mine;

    public Sprite Slide;
    public Sprite Slide_Each;
    public Sprite Slide_Break;
    public Sprite Slide_Mine;
    public Sprite Slide_Break_Mine;
    public Sprite[] Wifi = new Sprite[11];
    public Sprite[] Wifi_Each = new Sprite[11];
    public Sprite[] Wifi_Break = new Sprite[11];
    public Sprite[] Wifi_Mine = new Sprite[11];
    public Sprite[] Wifi_Break_Mine = new Sprite[11];

    public Sprite Star;
    public Sprite Star_Double;
    public Sprite Star_Each;
    public Sprite Star_Each_Double;
    public Sprite Star_Break;
    public Sprite Star_Break_Double;
    public Sprite Star_Mine;
    public Sprite Star_Mine_Double;
    public Sprite Star_Ex;
    public Sprite Star_Ex_Double;
    public Sprite Star_Break_Mine;
    public Sprite Star_Break_Double_Mine;

    public Sprite Hold;
    public Sprite Hold_On;
    public Sprite Hold_Off;
    public Sprite Hold_Each;
    public Sprite Hold_Each_On;
    public Sprite Hold_Break;
    public Sprite Hold_Break_On;
    public Sprite Hold_Mine;
    public Sprite Hold_Mine_On;
    public Sprite Hold_Break_Mine;
    public Sprite Hold_Break_Mine_On;
    public Sprite Hold_Ex;

    public Sprite[] Just = new Sprite[36];
    public Sprite[] JudgeText = new Sprite[5];
    public Sprite JudgeText_Break;
    public Sprite FastText;
    public Sprite LateText;

    public Sprite Touch;
    public Sprite Touch_Each;
    public Sprite Touch_Break;
    public Sprite Touch_Mine;
    public Sprite Touch_Break_Mine;
    public Sprite TouchPoint;
    public Sprite TouchPoint_Each;
    public Sprite TouchPoint_Break;
    public Sprite TouchPoint_Mine;
    public Sprite TouchPoint_Break_Mine;
    public Sprite TouchJust;
    public Sprite[] TouchBorder = new Sprite[2];
    public Sprite[] TouchBorder_Each = new Sprite[2];
    public Sprite[] TouchBorder_Break = new Sprite[2];
    public Sprite[] TouchBorder_Mine = new Sprite[2];
    public Sprite[] TouchBorder_Break_Mine = new Sprite[2];

    public Sprite[] TouchHold = new Sprite[4];
    public Sprite[] TouchHold_Break = new Sprite[4];
    public Sprite[] TouchHold_Mine = new Sprite[4];
    public Sprite TouchHold_Border;
    public Sprite TouchHold_Border_Break;
    public Sprite TouchHold_Border_Mine;
    public Sprite TouchHold_Border_Break_Mine;
    public Sprite TouchHold_Border_Miss;

    public Color Ex;
    public Color Ex_Star;
    public Color Ex_Each;
    public Color Ex_Break;

    public float4 Ex_f4;
    public float4 Ex_Star_f4;
    public float4 Ex_Each_f4;
    public float4 Ex_Break_f4;

    public Sprite Line;
    public Sprite Line_Each;
    public Sprite Line_Mine;
    public Sprite Line_Break;
    public Sprite Line_Star;

    public Sprite[] EachLine = new Sprite[4];

    public Sprite HoldEnd;
    public Sprite HoldEnd_Each;
    public Sprite HoldEnd_Break;

    [SerializeField]
    public RuntimeAnimatorController Shine;
    [SerializeField]
    public RuntimeAnimatorController Shine_Break;
    [SerializeField]
    public RuntimeAnimatorController Shine_JudgeBreak;
    [SerializeField]
    public Material BreakMaterial;


    private SpriteRenderer Outline;

    private void Awake()
    {
        _skinManager = this;

        var path = MajEnv.GetPath("Skin");
        var tapPath = Path.Combine(path, "TapSkins");
        var slidePath = Path.Combine(path, "SlideSkins");
        var wifiPath = Path.Combine(path, "WifiSkins");
        var starPath = Path.Combine(path, "StarSkins");
        var holdPath = Path.Combine(path, "HoldSkins");
        var slideOkPath = Path.Combine(path, "SlideOKSkins");
        var judgeTextPath = Path.Combine(path, "JudgeTextSkins");
        var touchPath = Path.Combine(path, "TouchSkins");
        var touchHoldPath = Path.Combine(path, "TouchHoldSkins");
        var noteGuidePath = Path.Combine(path, "NoteGuideSkins");

        Outline = gameObject.GetComponent<SpriteRenderer>();

        Outline.sprite = SpriteLoader.Load(path + "/outline.png");

        Tap = SpriteLoader.Load(tapPath + "/tap.png");
        Tap_Each = SpriteLoader.Load(tapPath + "/tap_each.png");
        Tap_Break = SpriteLoader.Load(tapPath + "/tap_break.png");
        Tap_Ex = SpriteLoader.Load(tapPath + "/tap_ex.png");
        Tap_Mine = SpriteLoader.Load(tapPath + "/tap_mine.png");
        Tap_Break_Mine = SpriteLoader.Load(tapPath + "/tap_break_mine.png");

        Slide = SpriteLoader.Load(slidePath + "/slide.png");
        Slide_Each = SpriteLoader.Load(slidePath + "/slide_each.png");
        Slide_Break = SpriteLoader.Load(slidePath + "/slide_break.png");
        Slide_Mine = SpriteLoader.Load(slidePath + "/slide_mine.png");
        Slide_Break_Mine = SpriteLoader.Load(slidePath + "/slide_break_mine.png");
        for (var i = 0; i < 11; i++)
        {
            Wifi[i] = SpriteLoader.Load(wifiPath + "/wifi_" + i + ".png");
            Wifi_Each[i] = SpriteLoader.Load(wifiPath + "/wifi_each_" + i + ".png");
            Wifi_Break[i] = SpriteLoader.Load(wifiPath + "/wifi_break_" + i + ".png");
            Wifi_Mine[i] = SpriteLoader.Load(wifiPath + "/wifi_mine_" + i + ".png");
            Wifi_Break_Mine[i] = SpriteLoader.Load(wifiPath + "/wifi_break_mine_" + i + ".png");
        }

        Star = SpriteLoader.Load(starPath + "/star.png");
        Star_Double = SpriteLoader.Load(starPath + "/star_double.png");
        Star_Each = SpriteLoader.Load(starPath + "/star_each.png");
        Star_Each_Double = SpriteLoader.Load(starPath + "/star_each_double.png");
        Star_Break = SpriteLoader.Load(starPath + "/star_break.png");
        Star_Break_Double = SpriteLoader.Load(starPath + "/star_break_double.png");
        Star_Ex = SpriteLoader.Load(starPath + "/star_ex.png");
        Star_Ex_Double = SpriteLoader.Load(starPath + "/star_ex_double.png");
        Star_Mine = SpriteLoader.Load(starPath + "/star_mine.png");
        Star_Mine_Double = SpriteLoader.Load(starPath + "/star_double_mine.png");
        Star_Break_Mine = SpriteLoader.Load(starPath + "/star_break_mine.png");
        Star_Break_Double_Mine = SpriteLoader.Load(starPath + "/star_break_double_mine.png");

        var border = new Vector4(0, 58, 0, 58);
        Hold = SpriteLoader.Load(holdPath + "/hold.png", border);
        Hold_Each = SpriteLoader.Load(holdPath + "/hold_each.png", border);
        Hold_Break = SpriteLoader.Load(holdPath + "/hold_break.png", border);
        Hold_Mine = SpriteLoader.Load(holdPath + "/hold_mine.png", border);
        Hold_Break_Mine = SpriteLoader.Load(holdPath + "/hold_break_mine.png", border);
        Hold_Ex = SpriteLoader.Load(holdPath + "/hold_ex.png", border);
        Hold_Off = SpriteLoader.Load(holdPath + "/hold_off.png", border);

        if (File.Exists(Path.Combine(holdPath, "hold_on.png")))
            Hold_On = SpriteLoader.Load(holdPath + "/hold_on.png", border);
        else
            Hold_On = Hold;

        if (File.Exists(Path.Combine(holdPath, "hold_each_on.png")))
            Hold_Each_On = SpriteLoader.Load(holdPath + "/hold_each_on.png", border);
        else
            Hold_Each_On = Hold_Each;

        if (File.Exists(Path.Combine(holdPath, "hold_break_on.png")))
            Hold_Break_On = SpriteLoader.Load(holdPath + "/hold_break_on.png", border);
        else
            Hold_Break_On = Hold_Break;

        if (File.Exists(Path.Combine(holdPath, "hold_mine_on.png")))
            Hold_Mine_On = SpriteLoader.Load(holdPath + "/hold_mine_on.png", border);
        else
            Hold_Mine_On = Hold_Mine;

        if (File.Exists(Path.Combine(holdPath, "hold_break_mine_on.png")))
            Hold_Break_Mine_On = SpriteLoader.Load(holdPath + "/hold_break_mine_on.png", border);
        else
            Hold_Break_Mine_On = Hold_Break_Mine;

        Just[0] = SpriteLoader.Load(slideOkPath + "/just_curv_r.png");
        Just[1] = SpriteLoader.Load(slideOkPath + "/just_str_r.png");
        Just[2] = SpriteLoader.Load(slideOkPath + "/just_wifi_u.png");
        Just[3] = SpriteLoader.Load(slideOkPath + "/just_curv_l.png");
        Just[4] = SpriteLoader.Load(slideOkPath + "/just_str_l.png");
        Just[5] = SpriteLoader.Load(slideOkPath + "/just_wifi_d.png");

        Just[6] = SpriteLoader.Load(slideOkPath + "/just_curv_r_fast_gr.png");
        Just[7] = SpriteLoader.Load(slideOkPath + "/just_str_r_fast_gr.png");
        Just[8] = SpriteLoader.Load(slideOkPath + "/just_wifi_u_fast_gr.png");
        Just[9] = SpriteLoader.Load(slideOkPath + "/just_curv_l_fast_gr.png");
        Just[10] = SpriteLoader.Load(slideOkPath + "/just_str_l_fast_gr.png");
        Just[11] = SpriteLoader.Load(slideOkPath + "/just_wifi_d_fast_gr.png");

        Just[12] = SpriteLoader.Load(slideOkPath + "/just_curv_r_fast_gd.png");
        Just[13] = SpriteLoader.Load(slideOkPath + "/just_str_r_fast_gd.png");
        Just[14] = SpriteLoader.Load(slideOkPath + "/just_wifi_u_fast_gd.png");
        Just[15] = SpriteLoader.Load(slideOkPath + "/just_curv_l_fast_gd.png");
        Just[16] = SpriteLoader.Load(slideOkPath + "/just_str_l_fast_gd.png");
        Just[17] = SpriteLoader.Load(slideOkPath + "/just_wifi_d_fast_gd.png");

        Just[18] = SpriteLoader.Load(slideOkPath + "/just_curv_r_late_gr.png");
        Just[19] = SpriteLoader.Load(slideOkPath + "/just_str_r_late_gr.png");
        Just[20] = SpriteLoader.Load(slideOkPath + "/just_wifi_u_late_gr.png");
        Just[21] = SpriteLoader.Load(slideOkPath + "/just_curv_l_late_gr.png");
        Just[22] = SpriteLoader.Load(slideOkPath + "/just_str_l_late_gr.png");
        Just[23] = SpriteLoader.Load(slideOkPath + "/just_wifi_d_late_gr.png");

        Just[24] = SpriteLoader.Load(slideOkPath + "/just_curv_r_late_gd.png");
        Just[25] = SpriteLoader.Load(slideOkPath + "/just_str_r_late_gd.png");
        Just[26] = SpriteLoader.Load(slideOkPath + "/just_wifi_u_late_gd.png");
        Just[27] = SpriteLoader.Load(slideOkPath + "/just_curv_l_late_gd.png");
        Just[28] = SpriteLoader.Load(slideOkPath + "/just_str_l_late_gd.png");
        Just[29] = SpriteLoader.Load(slideOkPath + "/just_wifi_d_late_gd.png");

        Just[30] = SpriteLoader.Load(slideOkPath + "/miss_curv_r.png");
        Just[31] = SpriteLoader.Load(slideOkPath + "/miss_str_r.png");
        Just[32] = SpriteLoader.Load(slideOkPath + "/miss_wifi_u.png");
        Just[33] = SpriteLoader.Load(slideOkPath + "/miss_curv_l.png");
        Just[34] = SpriteLoader.Load(slideOkPath + "/miss_str_l.png");
        Just[35] = SpriteLoader.Load(slideOkPath + "/miss_wifi_d.png");

        JudgeText[0] = SpriteLoader.Load(judgeTextPath + "/judge_text_miss.png");
        JudgeText[1] = SpriteLoader.Load(judgeTextPath + "/judge_text_good.png");
        JudgeText[2] = SpriteLoader.Load(judgeTextPath + "/judge_text_great.png");
        JudgeText[3] = SpriteLoader.Load(judgeTextPath + "/judge_text_perfect.png");
        JudgeText[4] = SpriteLoader.Load(judgeTextPath + "/judge_text_cPerfect.png");
        JudgeText_Break = SpriteLoader.Load(judgeTextPath + "/judge_text_break.png");

        FastText = SpriteLoader.Load(judgeTextPath + "/fast.png");
        LateText = SpriteLoader.Load(judgeTextPath + "/late.png");

        Touch = SpriteLoader.Load(touchPath + "/touch.png");
        Touch_Each = SpriteLoader.Load(touchPath + "/touch_each.png");
        Touch_Break = SpriteLoader.Load(touchPath + "/touch_break.png");
        Touch_Mine = SpriteLoader.Load(touchPath + "/touch_mine.png");
        Touch_Break_Mine = SpriteLoader.Load(touchPath + "/touch_break_mine.png");
        TouchPoint = SpriteLoader.Load(touchPath + "/touch_point.png");
        TouchPoint_Each = SpriteLoader.Load(touchPath + "/touch_point_each.png");
        TouchPoint_Break = SpriteLoader.Load(touchPath + "/touch_break_point.png");
        TouchPoint_Mine = SpriteLoader.Load(touchPath + "/touch_point_mine.png");
        TouchPoint_Break_Mine = SpriteLoader.Load(touchPath + "/touch_break_point_mine.png");

        TouchJust = SpriteLoader.Load(touchPath + "/touch_just.png");

        TouchBorder[0] = SpriteLoader.Load(touchPath + "/touch_border_2.png");
        TouchBorder[1] = SpriteLoader.Load(touchPath + "/touch_border_3.png");
        TouchBorder_Each[0] = SpriteLoader.Load(touchPath + "/touch_border_2_each.png");
        TouchBorder_Each[1] = SpriteLoader.Load(touchPath + "/touch_border_3_each.png");
        TouchBorder_Break[0] = SpriteLoader.Load(touchPath + "/touch_break_border_2.png");
        TouchBorder_Break[1] = SpriteLoader.Load(touchPath + "/touch_break_border_3.png");
        TouchBorder_Mine[0] = SpriteLoader.Load(touchPath + "/touch_border_mine_2.png");
        TouchBorder_Mine[1] = SpriteLoader.Load(touchPath + "/touch_border_mine_3.png");
        TouchBorder_Break_Mine[0] = SpriteLoader.Load(touchPath + "/touch_break_border_mine_2.png");
        TouchBorder_Break_Mine[1] = SpriteLoader.Load(touchPath + "/touch_break_border_mine_3.png");

        for (var i = 0; i < 4; i++)
        {
            TouchHold[i] = SpriteLoader.Load(touchHoldPath + "/touchhold_" + i + ".png");
            TouchHold_Break[i] = SpriteLoader.Load(touchHoldPath + "/touchhold_break_" + i + ".png");
            TouchHold_Mine[i] = SpriteLoader.Load(touchHoldPath + "/touchhold_mine_" + i + ".png");
        }
        TouchHold_Border = SpriteLoader.Load(touchHoldPath + "/touchhold_border.png");
        TouchHold_Border_Break = SpriteLoader.Load(touchHoldPath + "/touchhold_break_border.png");
        TouchHold_Border_Break_Mine = SpriteLoader.Load(touchHoldPath + "/touchhold_break_mine.png");
        TouchHold_Border_Mine = SpriteLoader.Load(touchHoldPath + "/touchhold_mine.png");
        TouchHold_Border_Miss = SpriteLoader.Load(touchHoldPath + "/touchhold_off.png");

        Line = SpriteLoader.Load(noteGuidePath + "/Normal.png");
        Line_Each = SpriteLoader.Load(noteGuidePath + "/Each.png");
        Line_Break = SpriteLoader.Load(noteGuidePath + "/Break.png");
        Line_Star = SpriteLoader.Load(noteGuidePath + "/Slide.png");
        Line_Mine = SpriteLoader.Load(noteGuidePath + "/Mine.png");

        for (var i = 0; i < 4; i++)
            EachLine[i] = SpriteLoader.Load(noteGuidePath + "/EachLine" + (i + 1) + ".png");

        HoldEnd = SpriteLoader.Load(noteGuidePath + "/Hold_End.png");
        HoldEnd_Each = SpriteLoader.Load(noteGuidePath + "/Hold_Each_End.png");
        HoldEnd_Break = SpriteLoader.Load(noteGuidePath + "/Hold_Break_End.png");

        Ex = new Color32(255, 172, 255, 255);
        Ex_Star = new Color32(172, 251, 255, 255);
        Ex_Each = new Color32(255, 254, 119, 255);
        Ex_Break = Ex_Each;

        Ex_f4 = new float4(255, 172, 255, 255);
        Ex_Star_f4 = new float4(172, 251, 255, 255);
        Ex_Each_f4 = new float4(255, 254, 119, 255);
        Ex_Break_f4 = Ex_Each_f4;
    }
}