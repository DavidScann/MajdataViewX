#region

using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using static MajCtx;

#endregion

public class NoteSkinManager : MonoBehaviour
{
    // ============ Note Skin ============
    public const int COUNT = 184;

    public const int TAP = 0;
    public const int TAP_EACH = 1;
    public const int TAP_BREAK = 2;
    public const int TAP_EX = 3;
    public const int TAP_MINE = 4;
    public const int TAP_BREAK_MINE = 5;

    public const int SLIDE = 6;
    public const int SLIDE_EACH = 7;
    public const int SLIDE_BREAK = 8;
    public const int SLIDE_MINE = 9;
    public const int SLIDE_BREAK_MINE = 10;

    public const int WIFI_0 = 11;
    public const int WIFI_1 = 12;
    public const int WIFI_2 = 13;
    public const int WIFI_3 = 14;
    public const int WIFI_4 = 15;
    public const int WIFI_5 = 16;
    public const int WIFI_6 = 17;
    public const int WIFI_7 = 18;
    public const int WIFI_8 = 19;
    public const int WIFI_9 = 20;
    public const int WIFI_10 = 21;
    public const int WIFI_EACH_0 = 22;
    public const int WIFI_EACH_1 = 23;
    public const int WIFI_EACH_2 = 24;
    public const int WIFI_EACH_3 = 25;
    public const int WIFI_EACH_4 = 26;
    public const int WIFI_EACH_5 = 27;
    public const int WIFI_EACH_6 = 28;
    public const int WIFI_EACH_7 = 29;
    public const int WIFI_EACH_8 = 30;
    public const int WIFI_EACH_9 = 31;
    public const int WIFI_EACH_10 = 32;
    public const int WIFI_BREAK_0 = 33;
    public const int WIFI_BREAK_1 = 34;
    public const int WIFI_BREAK_2 = 35;
    public const int WIFI_BREAK_3 = 36;
    public const int WIFI_BREAK_4 = 37;
    public const int WIFI_BREAK_5 = 38;
    public const int WIFI_BREAK_6 = 39;
    public const int WIFI_BREAK_7 = 40;
    public const int WIFI_BREAK_8 = 41;
    public const int WIFI_BREAK_9 = 42;
    public const int WIFI_BREAK_10 = 43;
    public const int WIFI_MINE_0 = 44;
    public const int WIFI_MINE_1 = 45;
    public const int WIFI_MINE_2 = 46;
    public const int WIFI_MINE_3 = 47;
    public const int WIFI_MINE_4 = 48;
    public const int WIFI_MINE_5 = 49;
    public const int WIFI_MINE_6 = 50;
    public const int WIFI_MINE_7 = 51;
    public const int WIFI_MINE_8 = 52;
    public const int WIFI_MINE_9 = 53;
    public const int WIFI_MINE_10 = 54;
    public const int WIFI_BREAK_MINE_0 = 55;
    public const int WIFI_BREAK_MINE_1 = 56;
    public const int WIFI_BREAK_MINE_2 = 57;
    public const int WIFI_BREAK_MINE_3 = 58;
    public const int WIFI_BREAK_MINE_4 = 59;
    public const int WIFI_BREAK_MINE_5 = 60;
    public const int WIFI_BREAK_MINE_6 = 61;
    public const int WIFI_BREAK_MINE_7 = 62;
    public const int WIFI_BREAK_MINE_8 = 63;
    public const int WIFI_BREAK_MINE_9 = 64;
    public const int WIFI_BREAK_MINE_10 = 65;

    public const int STAR = 66;
    public const int STAR_DOUBLE = 67;
    public const int STAR_EACH = 68;
    public const int STAR_EACH_DOUBLE = 69;
    public const int STAR_BREAK = 70;
    public const int STAR_BREAK_DOUBLE = 71;
    public const int STAR_MINE = 72;
    public const int STAR_MINE_DOUBLE = 73;
    public const int STAR_EX = 74;
    public const int STAR_EX_DOUBLE = 75;
    public const int STAR_BREAK_MINE = 76;
    public const int STAR_BREAK_DOUBLE_MINE = 77;

    public const int HOLD = 78;
    public const int HOLD_ON = 79;
    public const int HOLD_OFF = 80;
    public const int HOLD_EACH = 81;
    public const int HOLD_EACH_ON = 82;
    public const int HOLD_BREAK = 83;
    public const int HOLD_BREAK_ON = 84;
    public const int HOLD_MINE = 85;
    public const int HOLD_MINE_ON = 86;
    public const int HOLD_BREAK_MINE = 87;
    public const int HOLD_BREAK_MINE_ON = 88;
    public const int HOLD_EX = 89;

    public const int JUST_STR_L = 90;
    public const int JUST_STR_R = 91;
    public const int JUST_CURV_L = 92;
    public const int JUST_CURV_R = 93;
    public const int JUST_WIFI_U = 94;
    public const int JUST_WIFI_D = 95;

    public const int JUST_STR_L_FAST_GR = 96;
    public const int JUST_STR_R_FAST_GR = 97;
    public const int JUST_CURV_L_FAST_GR = 98;
    public const int JUST_CURV_R_FAST_GR = 99;
    public const int JUST_WIFI_U_FAST_GR = 100;
    public const int JUST_WIFI_D_FAST_GR = 101;

    public const int JUST_STR_L_FAST_GD = 102;
    public const int JUST_STR_R_FAST_GD = 103;
    public const int JUST_CURV_L_FAST_GD = 104;
    public const int JUST_CURV_R_FAST_GD = 105;
    public const int JUST_WIFI_U_FAST_GD = 106;
    public const int JUST_WIFI_D_FAST_GD = 107;

    public const int JUST_STR_L_LATE_GR = 108;
    public const int JUST_STR_R_LATE_GR = 109;
    public const int JUST_CURV_L_LATE_GR = 110;
    public const int JUST_CURV_R_LATE_GR = 111;
    public const int JUST_WIFI_U_LATE_GR = 112;
    public const int JUST_WIFI_D_LATE_GR = 113;

    public const int JUST_STR_L_LATE_GD = 114;
    public const int JUST_STR_R_LATE_GD = 115;
    public const int JUST_CURV_L_LATE_GD = 116;
    public const int JUST_CURV_R_LATE_GD = 117;
    public const int JUST_WIFI_U_LATE_GD = 118;
    public const int JUST_WIFI_D_LATE_GD = 119;

    public const int JUST_STR_L_MISS = 120;
    public const int JUST_STR_R_MISS = 121;
    public const int JUST_CURV_L_MISS = 122;
    public const int JUST_CURV_R_MISS = 123;
    public const int JUST_WIFI_U_MISS = 124;
    public const int JUST_WIFI_D_MISS = 125;

    public const int JUDGE_TEXT_0 = 126;
    public const int JUDGE_TEXT_1 = 127;
    public const int JUDGE_TEXT_2 = 128;
    public const int JUDGE_TEXT_3 = 129;
    public const int JUDGE_TEXT_4 = 130;
    public const int JUDGE_TEXT_BREAK = 131;

    public const int FAST_TEXT = 132;
    public const int LATE_TEXT = 133;

    public const int TOUCH = 134;
    public const int TOUCH_EACH = 135;
    public const int TOUCH_BREAK = 136;
    public const int TOUCH_MINE = 137;
    public const int TOUCH_BREAK_MINE = 138;

    public const int TOUCH_POINT = 139;
    public const int TOUCH_POINT_EACH = 140;
    public const int TOUCH_POINT_BREAK = 141;
    public const int TOUCH_POINT_MINE = 142;
    public const int TOUCH_POINT_BREAK_MINE = 143;

    public const int TOUCH_JUST = 144;

    public const int TOUCH_BORDER_0 = 145;
    public const int TOUCH_BORDER_1 = 146;
    public const int TOUCH_BORDER_EACH_0 = 147;
    public const int TOUCH_BORDER_EACH_1 = 148;
    public const int TOUCH_BORDER_BREAK_0 = 149;
    public const int TOUCH_BORDER_BREAK_1 = 150;
    public const int TOUCH_BORDER_MINE_0 = 151;
    public const int TOUCH_BORDER_MINE_1 = 152;
    public const int TOUCH_BORDER_BREAK_MINE_0 = 153;
    public const int TOUCH_BORDER_BREAK_MINE_1 = 154;

    public const int TOUCH_HOLD_0 = 155;
    public const int TOUCH_HOLD_1 = 156;
    public const int TOUCH_HOLD_2 = 157;
    public const int TOUCH_HOLD_3 = 158;
    public const int TOUCH_HOLD_BREAK_0 = 159;
    public const int TOUCH_HOLD_BREAK_1 = 160;
    public const int TOUCH_HOLD_BREAK_2 = 161;
    public const int TOUCH_HOLD_BREAK_3 = 162;
    public const int TOUCH_HOLD_MINE_0 = 163;
    public const int TOUCH_HOLD_MINE_1 = 164;
    public const int TOUCH_HOLD_MINE_2 = 165;
    public const int TOUCH_HOLD_MINE_3 = 166;

    public const int TOUCH_HOLD_BORDER = 167;
    public const int TOUCH_HOLD_BORDER_BREAK = 168;
    public const int TOUCH_HOLD_BORDER_MINE = 169;
    public const int TOUCH_HOLD_BORDER_BREAK_MINE = 170;
    public const int TOUCH_HOLD_BORDER_MISS = 171;

    public const int LINE = 172;
    public const int LINE_EACH = 173;
    public const int LINE_MINE = 174;
    public const int LINE_BREAK = 175;
    public const int LINE_STAR = 176;

    public const int EACH_LINE_0 = 177;
    public const int EACH_LINE_1 = 178;
    public const int EACH_LINE_2 = 179;
    public const int EACH_LINE_3 = 180;

    public const int HOLD_END = 181;
    public const int HOLD_END_EACH = 182;
    public const int HOLD_END_BREAK = 183;

    public static readonly float4 Ex = new float4(255, 172, 255, 255) / 255f;
    public static readonly float4 Ex_Star = new float4(172, 251, 255, 255) / 255f;
    public static readonly float4 Ex_Each = new float4(255, 254, 119, 255) / 255f;
    public static readonly float4 Ex_Break = Ex_Each;

    public const float HoldBaseWidth = 1.22f;              // legacy spriteRenderer.size.x
    public const float HoldCapAllowance = 1.4f;            // legacy total sprite height
    public const float HoldCapEach = 58 / 100f;            // 58px / 100PPU
    public const float HoldNativeWidth = 122 / 100f;       // tex.width / 100
    public const float HoldNativeHeight = 200 / 100f;      // tex.height / 100
    public static readonly float2 HoldSliceBorder = new(HoldCapEach / HoldNativeHeight); // capWorld / nativeHeight

    public Texture2D Atlas;
    public NativeArray<float4> Uvs;

    // =========== Other Skin ============

    public Sprite[] JudgeText = new Sprite[5];
    public Sprite JudgeText_BPerfect;
    public Sprite FastText;
    public Sprite LateText;

    public Sprite[] TouchBorder_Normal = new Sprite[2];
    public Sprite[] TouchBorder_Each = new Sprite[2];
    public Sprite[] TouchBorder_Break = new Sprite[2];
    public Sprite[] TouchBorder_Mine = new Sprite[2];
    public Sprite[] TouchBorder_Break_Mine = new Sprite[2];

    public Sprite Outline;

    private void Awake()
    {
        _noteSkinManager = this;

        var skinPath = MajEnv.GetPath("Skin");
        var tapPath = Path.Combine(skinPath, "TapSkins");
        var slidePath = Path.Combine(skinPath, "SlideSkins");
        var wifiPath = Path.Combine(skinPath, "WifiSkins");
        var starPath = Path.Combine(skinPath, "StarSkins");
        var holdPath = Path.Combine(skinPath, "HoldSkins");
        var slideOkPath = Path.Combine(skinPath, "SlideOKSkins");
        var judgeTextPath = Path.Combine(skinPath, "JudgeTextSkins");
        var touchPath = Path.Combine(skinPath, "TouchSkins");
        var touchHoldPath = Path.Combine(skinPath, "TouchHoldSkins");
        var noteGuidePath = Path.Combine(skinPath, "NoteGuideSkins");

        var sources = new List<(string path, int index, Texture2D tex)>(COUNT);

        Add(sources, TAP, tapPath + "/tap.png");
        Add(sources, TAP_EACH, tapPath + "/tap_each.png");
        Add(sources, TAP_BREAK, tapPath + "/tap_break.png");
        Add(sources, TAP_EX, tapPath + "/tap_ex.png");
        Add(sources, TAP_MINE, tapPath + "/tap_mine.png");
        Add(sources, TAP_BREAK_MINE, tapPath + "/tap_break_mine.png");

        Add(sources, SLIDE, slidePath + "/slide.png");
        Add(sources, SLIDE_EACH, slidePath + "/slide_each.png");
        Add(sources, SLIDE_BREAK, slidePath + "/slide_break.png");
        Add(sources, SLIDE_MINE, slidePath + "/slide_mine.png");
        Add(sources, SLIDE_BREAK_MINE, slidePath + "/slide_break_mine.png");

        for (int i = 0; i < 11; i++)
        {
            Add(sources, WIFI_0 + i, wifiPath + "/wifi_" + i + ".png");
            Add(sources, WIFI_EACH_0 + i, wifiPath + "/wifi_each_" + i + ".png");
            Add(sources, WIFI_BREAK_0 + i, wifiPath + "/wifi_break_" + i + ".png");
            Add(sources, WIFI_MINE_0 + i, wifiPath + "/wifi_mine_" + i + ".png");
            Add(sources, WIFI_BREAK_MINE_0 + i, wifiPath + "/wifi_break_mine_" + i + ".png");
        }

        Add(sources, STAR, starPath + "/star.png");
        Add(sources, STAR_DOUBLE, starPath + "/star_double.png");
        Add(sources, STAR_EACH, starPath + "/star_each.png");
        Add(sources, STAR_EACH_DOUBLE, starPath + "/star_each_double.png");
        Add(sources, STAR_BREAK, starPath + "/star_break.png");
        Add(sources, STAR_BREAK_DOUBLE, starPath + "/star_break_double.png");
        Add(sources, STAR_MINE, starPath + "/star_mine.png");
        Add(sources, STAR_MINE_DOUBLE, starPath + "/star_double_mine.png");
        Add(sources, STAR_EX, starPath + "/star_ex.png");
        Add(sources, STAR_EX_DOUBLE, starPath + "/star_ex_double.png");
        Add(sources, STAR_BREAK_MINE, starPath + "/star_break_mine.png");
        Add(sources, STAR_BREAK_DOUBLE_MINE, starPath + "/star_break_double_mine.png");

        Add(sources, HOLD, holdPath + "/hold.png");
        Add(sources, HOLD_EACH, holdPath + "/hold_each.png");
        Add(sources, HOLD_BREAK, holdPath + "/hold_break.png");
        Add(sources, HOLD_MINE, holdPath + "/hold_mine.png");
        Add(sources, HOLD_BREAK_MINE, holdPath + "/hold_break_mine.png");
        Add(sources, HOLD_EX, holdPath + "/hold_ex.png");
        Add(sources, HOLD_OFF, holdPath + "/hold_off.png");
        Add(sources, HOLD_ON, File.Exists(holdPath + "/hold_on.png") ? holdPath + "/hold_on.png" : holdPath + "/hold.png");
        Add(sources, HOLD_EACH_ON, File.Exists(holdPath + "/hold_each_on.png") ? holdPath + "/hold_each_on.png" : holdPath + "/hold_each.png");
        Add(sources, HOLD_BREAK_ON, File.Exists(holdPath + "/hold_break_on.png") ? holdPath + "/hold_break_on.png" : holdPath + "/hold_break.png");
        Add(sources, HOLD_MINE_ON, File.Exists(holdPath + "/hold_mine_on.png") ? holdPath + "/hold_mine_on.png" : holdPath + "/hold_mine.png");
        Add(sources, HOLD_BREAK_MINE_ON, File.Exists(holdPath + "/hold_break_mine_on.png") ? holdPath + "/hold_break_mine_on.png" : holdPath + "/hold_break_mine.png");

        Add(sources, JUST_STR_L, slideOkPath + "/just_str_l.png");
        Add(sources, JUST_STR_R, slideOkPath + "/just_str_r.png");
        Add(sources, JUST_CURV_L, slideOkPath + "/just_curv_l.png");
        Add(sources, JUST_CURV_R, slideOkPath + "/just_curv_r.png");
        Add(sources, JUST_WIFI_U, slideOkPath + "/just_wifi_u.png");
        Add(sources, JUST_WIFI_D, slideOkPath + "/just_wifi_d.png");

        Add(sources, JUST_STR_L_FAST_GR, slideOkPath + "/just_str_l_fast_gr.png");
        Add(sources, JUST_STR_R_FAST_GR, slideOkPath + "/just_str_r_fast_gr.png");
        Add(sources, JUST_CURV_L_FAST_GR, slideOkPath + "/just_curv_l_fast_gr.png");
        Add(sources, JUST_CURV_R_FAST_GR, slideOkPath + "/just_curv_r_fast_gr.png");
        Add(sources, JUST_WIFI_U_FAST_GR, slideOkPath + "/just_wifi_u_fast_gr.png");
        Add(sources, JUST_WIFI_D_FAST_GR, slideOkPath + "/just_wifi_d_fast_gr.png");

        Add(sources, JUST_STR_L_FAST_GD, slideOkPath + "/just_str_l_fast_gd.png");
        Add(sources, JUST_STR_R_FAST_GD, slideOkPath + "/just_str_r_fast_gd.png");
        Add(sources, JUST_CURV_L_FAST_GD, slideOkPath + "/just_curv_l_fast_gd.png");
        Add(sources, JUST_CURV_R_FAST_GD, slideOkPath + "/just_curv_r_fast_gd.png");
        Add(sources, JUST_WIFI_U_FAST_GD, slideOkPath + "/just_wifi_u_fast_gd.png");
        Add(sources, JUST_WIFI_D_FAST_GD, slideOkPath + "/just_wifi_d_fast_gd.png");

        Add(sources, JUST_STR_L_LATE_GR, slideOkPath + "/just_str_l_late_gr.png");
        Add(sources, JUST_STR_R_LATE_GR, slideOkPath + "/just_str_r_late_gr.png");
        Add(sources, JUST_CURV_L_LATE_GR, slideOkPath + "/just_curv_l_late_gr.png");
        Add(sources, JUST_CURV_R_LATE_GR, slideOkPath + "/just_curv_r_late_gr.png");
        Add(sources, JUST_WIFI_U_LATE_GR, slideOkPath + "/just_wifi_u_late_gr.png");
        Add(sources, JUST_WIFI_D_LATE_GR, slideOkPath + "/just_wifi_d_late_gr.png");

        Add(sources, JUST_STR_L_LATE_GD, slideOkPath + "/just_str_l_late_gd.png");
        Add(sources, JUST_STR_R_LATE_GD, slideOkPath + "/just_str_r_late_gd.png");
        Add(sources, JUST_CURV_L_LATE_GD, slideOkPath + "/just_curv_l_late_gd.png");
        Add(sources, JUST_CURV_R_LATE_GD, slideOkPath + "/just_curv_r_late_gd.png");
        Add(sources, JUST_WIFI_U_LATE_GD, slideOkPath + "/just_wifi_u_late_gd.png");
        Add(sources, JUST_WIFI_D_LATE_GD, slideOkPath + "/just_wifi_d_late_gd.png");

        Add(sources, JUST_STR_L_MISS, slideOkPath + "/miss_str_l.png");
        Add(sources, JUST_STR_R_MISS, slideOkPath + "/miss_str_r.png");
        Add(sources, JUST_CURV_L_MISS, slideOkPath + "/miss_curv_l.png");
        Add(sources, JUST_CURV_R_MISS, slideOkPath + "/miss_curv_r.png");
        Add(sources, JUST_WIFI_U_MISS, slideOkPath + "/miss_wifi_u.png");
        Add(sources, JUST_WIFI_D_MISS, slideOkPath + "/miss_wifi_d.png");

        Add(sources, JUDGE_TEXT_0, judgeTextPath + "/judge_text_miss.png");
        Add(sources, JUDGE_TEXT_1, judgeTextPath + "/judge_text_good.png");
        Add(sources, JUDGE_TEXT_2, judgeTextPath + "/judge_text_great.png");
        Add(sources, JUDGE_TEXT_3, judgeTextPath + "/judge_text_perfect.png");
        Add(sources, JUDGE_TEXT_4, judgeTextPath + "/judge_text_cPerfect.png");
        Add(sources, JUDGE_TEXT_BREAK, judgeTextPath + "/judge_text_break.png");

        Add(sources, FAST_TEXT, judgeTextPath + "/fast.png");
        Add(sources, LATE_TEXT, judgeTextPath + "/late.png");

        Add(sources, TOUCH, touchPath + "/touch.png");
        Add(sources, TOUCH_EACH, touchPath + "/touch_each.png");
        Add(sources, TOUCH_BREAK, touchPath + "/touch_break.png");
        Add(sources, TOUCH_MINE, touchPath + "/touch_mine.png");
        Add(sources, TOUCH_BREAK_MINE, touchPath + "/touch_break_mine.png");

        Add(sources, TOUCH_POINT, touchPath + "/touch_point.png");
        Add(sources, TOUCH_POINT_EACH, touchPath + "/touch_point_each.png");
        Add(sources, TOUCH_POINT_BREAK, touchPath + "/touch_break_point.png");
        Add(sources, TOUCH_POINT_MINE, touchPath + "/touch_point_mine.png");
        Add(sources, TOUCH_POINT_BREAK_MINE, touchPath + "/touch_break_point_mine.png");

        Add(sources, TOUCH_JUST, touchPath + "/touch_just.png");

        Add(sources, TOUCH_BORDER_0, touchPath + "/touch_border_2.png");
        Add(sources, TOUCH_BORDER_1, touchPath + "/touch_border_3.png");
        Add(sources, TOUCH_BORDER_EACH_0, touchPath + "/touch_border_2_each.png");
        Add(sources, TOUCH_BORDER_EACH_1, touchPath + "/touch_border_3_each.png");
        Add(sources, TOUCH_BORDER_BREAK_0, touchPath + "/touch_break_border_2.png");
        Add(sources, TOUCH_BORDER_BREAK_1, touchPath + "/touch_break_border_3.png");
        Add(sources, TOUCH_BORDER_MINE_0, touchPath + "/touch_border_mine_2.png");
        Add(sources, TOUCH_BORDER_MINE_1, touchPath + "/touch_border_mine_3.png");
        Add(sources, TOUCH_BORDER_BREAK_MINE_0, touchPath + "/touch_break_border_mine_2.png");
        Add(sources, TOUCH_BORDER_BREAK_MINE_1, touchPath + "/touch_break_border_mine_3.png");

        for (int i = 0; i < 4; i++)
        {
            Add(sources, TOUCH_HOLD_0 + i, touchHoldPath + "/touchhold_" + i + ".png");
            Add(sources, TOUCH_HOLD_BREAK_0 + i, touchHoldPath + "/touchhold_break_" + i + ".png");
            Add(sources, TOUCH_HOLD_MINE_0 + i, touchHoldPath + "/touchhold_mine_" + i + ".png");
        }
        Add(sources, TOUCH_HOLD_BORDER, touchHoldPath + "/touchhold_border.png");
        Add(sources, TOUCH_HOLD_BORDER_BREAK, touchHoldPath + "/touchhold_break_border.png");
        Add(sources, TOUCH_HOLD_BORDER_BREAK_MINE, touchHoldPath + "/touchhold_break_mine.png");
        Add(sources, TOUCH_HOLD_BORDER_MINE, touchHoldPath + "/touchhold_mine.png");
        Add(sources, TOUCH_HOLD_BORDER_MISS, touchHoldPath + "/touchhold_off.png");

        Add(sources, LINE, noteGuidePath + "/Normal.png");
        Add(sources, LINE_EACH, noteGuidePath + "/Each.png");
        Add(sources, LINE_BREAK, noteGuidePath + "/Break.png");
        Add(sources, LINE_STAR, noteGuidePath + "/Slide.png");
        Add(sources, LINE_MINE, noteGuidePath + "/Mine.png");

        for (int i = 0; i < 4; i++)
            Add(sources, EACH_LINE_0 + i, noteGuidePath + "/EachLine" + (i + 1) + ".png");

        Add(sources, HOLD_END, noteGuidePath + "/Hold_End.png");
        Add(sources, HOLD_END_EACH, noteGuidePath + "/Hold_Each_End.png");
        Add(sources, HOLD_END_BREAK, noteGuidePath + "/Hold_Break_End.png");

        // Load judge sprites separately for EffectManager (atlas textures get destroyed)
        JudgeText[0] = LoadSprite(judgeTextPath + "/judge_text_miss.png");
        JudgeText[1] = LoadSprite(judgeTextPath + "/judge_text_good.png");
        JudgeText[2] = LoadSprite(judgeTextPath + "/judge_text_great.png");
        JudgeText[3] = LoadSprite(judgeTextPath + "/judge_text_perfect.png");
        JudgeText[4] = LoadSprite(judgeTextPath + "/judge_text_cPerfect.png");
        JudgeText_BPerfect = LoadSprite(judgeTextPath + "/judge_text_break.png");
        FastText = LoadSprite(judgeTextPath + "/fast.png");
        LateText = LoadSprite(judgeTextPath + "/late.png");

        TouchBorder_Normal[0] = LoadSprite(touchPath + "/TouchSkins/touch_border_2.png");
        TouchBorder_Normal[1] = LoadSprite(touchPath + "/TouchSkins/touch_border_3.png");
        TouchBorder_Each[0] = LoadSprite(touchPath + "/TouchSkins/touch_border_2_each.png");
        TouchBorder_Each[1] = LoadSprite(touchPath + "/TouchSkins/touch_border_3_each.png");
        TouchBorder_Break[0] = LoadSprite(touchPath + "/TouchSkins/touch_break_border_2.png");
        TouchBorder_Break[1] = LoadSprite(touchPath + "/TouchSkins/touch_break_border_3.png");
        TouchBorder_Mine[0] = LoadSprite(touchPath + "/TouchSkins/touch_mine_border_2.png");
        TouchBorder_Mine[1] = LoadSprite(touchPath + "/TouchSkins/touch_mine_border_3_mine.png");
        TouchBorder_Break_Mine[0] = LoadSprite(touchPath + "/TouchSkins/touch_break_mine_border_2.png");
        TouchBorder_Break_Mine[1] = LoadSprite(touchPath + "/TouchSkins/touch_break_mine_border_3.png");

        Outline = LoadSprite(Path.Combine(skinPath, "outline.png"));

        BuildAtlas(sources);
    }

    private void Start()
    {
        GetComponent<SpriteRenderer>().sprite = Outline;
    }

    private void Add(List<(string path, int index, Texture2D tex)> list, int index, string path)
    {
        var tex = LoadTextureFromFile(path);
        list.Add((path, index, tex));
    }

    private static Texture2D LoadTextureFromFile(string path)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!File.Exists(path))
            return tex;
        var bytes = File.ReadAllBytes(path);
        tex.LoadImage(bytes);
        return tex;
    }

    private static Sprite LoadSprite(string path)
    {
        var tex = LoadTextureFromFile(path);
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
    }

    private void BuildAtlas(List<(string path, int index, Texture2D tex)> sources)
    {
        const int atlasSize = 8192;

        sources.Sort((a, b) => b.tex.height.CompareTo(a.tex.height));

        Uvs = new NativeArray<float4>(COUNT, Allocator.Persistent);
        Atlas = new Texture2D(atlasSize, atlasSize, TextureFormat.RGBA32, false);
        var atlasPixels = Atlas.GetPixels32();

        var shelves = new List<(int y, int x, int remainingHeight)>();
        int nextY = 0;
        var placements = new (int x, int y)[COUNT];

        foreach (var (_, index, tex) in sources)
        {
            int w = tex.width;
            int h = tex.height;
            bool placed = false;

            for (int s = 0; s < shelves.Count; s++)
            {
                var shelf = shelves[s];
                if (h <= shelf.remainingHeight && shelf.x + w <= atlasSize)
                {
                    placements[index] = (shelf.x, shelf.y);
                    shelves[s] = (shelf.y, shelf.x + w, shelf.remainingHeight);
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                if (nextY + h > atlasSize)
                    break;

                placements[index] = (0, nextY);
                shelves.Add((nextY, w, h));
                nextY += h;
            }
        }

        foreach (var (_, index, tex) in sources)
        {
            var (px, py) = placements[index];
            var pixels = tex.GetPixels32();
            for (int y = 0; y < tex.height; y++)
            {
                for (int x = 0; x < tex.width; x++)
                {
                    int srcIdx = y * tex.width + x;
                    int dstIdx = (py + y) * atlasSize + (px + x);
                    atlasPixels[dstIdx] = pixels[srcIdx];
                }
            }

            float invSize = 1f / atlasSize;
            float halfTexel = 0.5f * invSize;
            Uvs[index] = new float4(
                px * invSize + halfTexel,
                py * invSize + halfTexel,
                (px + tex.width) * invSize - halfTexel,
                (py + tex.height) * invSize - halfTexel
            );

            Destroy(tex);
        }

        Atlas.SetPixels32(atlasPixels);
        Atlas.Apply();
    }

    private void OnDestroy()
    {
        if (Uvs.IsCreated) Uvs.Dispose();
        if (Atlas != null) Destroy(Atlas);
    }
}
