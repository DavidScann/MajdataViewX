#region

using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using MajSimai;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using static MajCtx;

#endregion

public partial class ObjectCounter : MonoBehaviour
{
    [SerializeField]
    Color AchievementDudColor;
    [SerializeField]
    Color AchievementBronzeColor;
    [SerializeField]
    Color AchievementSilverColor;
    [SerializeField]
    Color AchievementGoldColor;

    public BgInfoDisplay TextMode { get; private set; }
    public UIType? CurrentUIType { get; private set; } = null;

    //Legacy UI
    [SerializeField]
    private GameObject legacyUIRoot;
    [SerializeField]
    private Text timeDisplay;
    [SerializeField]
    private Text objectCount;
    [SerializeField]
    private Text objectRate;
    [SerializeField]
    private Text judgeResultCount;

    //Trg UI
    [SerializeField]
    private GameObject trgUIRoot;
    [SerializeField]
    private TextMeshProUGUI objTime;
    [SerializeField]
    private TextMeshProUGUI objRate;
    [SerializeField]
    private TextMeshProUGUI objCombo;
    [SerializeField]
    private TextMeshProUGUI objNoteCount;
    [SerializeField]
    private TextMeshProUGUI objMeter;
    [SerializeField]
    private TextMeshProUGUI objBpm;
    [SerializeField]
    private TextMeshProUGUI objBpmRange;
    [SerializeField]
    private TextMeshProUGUI objJudgeResult;
    [SerializeField]
    private TextMeshProUGUI objAutoMode;

    //Main Output
    [SerializeField]
    private Text statusAchievement;
    [SerializeField]
    private Text statusCombo;
    [SerializeField]
    private Text statusDXScore;

    public void StartOutput(BgInfoDisplay mode, UIType type)
    {
        TextMode = mode;
        switch (mode)
        {
            case BgInfoDisplay.None:
                statusCombo.gameObject.SetActive(false);
                statusAchievement.gameObject.SetActive(false);
                statusDXScore.gameObject.SetActive(false);
                break;
            case BgInfoDisplay.Combo:
                statusCombo.gameObject.SetActive(true);
                statusAchievement.gameObject.SetActive(false);
                statusDXScore.gameObject.SetActive(false);
                break;
            case BgInfoDisplay.Achievement_101:
            case BgInfoDisplay.Achievement_100:
            case BgInfoDisplay.Achievement:
            case BgInfoDisplay.AchievementClassical:
            case BgInfoDisplay.AchievementClassical_100:
            case BgInfoDisplay.S_Border:
            case BgInfoDisplay.SS_Border:
            case BgInfoDisplay.SSS_Border:
                statusCombo.gameObject.SetActive(false);
                statusAchievement.gameObject.SetActive(true);
                statusDXScore.gameObject.SetActive(false);
                break;
            case BgInfoDisplay.DXScore:
                statusCombo.gameObject.SetActive(false);
                statusAchievement.gameObject.SetActive(false);
                statusDXScore.gameObject.SetActive(true);
                break;
        }
        if (type is UIType.TrgUI)
        {
            switch (NoteHelper.AutoPlayMode)
            {
                case AutoPlayMode.Enable:
                    objAutoMode.text = "ENABLED\nNONE";
                    break;
                case AutoPlayMode.DJAutoButton:
                    objAutoMode.text = "ENABLED\nDJAuto (Btn)";
                    break;
                case AutoPlayMode.DJAutoSensor:
                    objAutoMode.text = "ENABLED\nDJAuto";
                    break;
                case AutoPlayMode.Random:
                    objAutoMode.text = "ENABLED\nRANDOM";
                    break;
                case AutoPlayMode.Disable:
                    objAutoMode.text = "DISABLED\nNONE";
                    break;
            }
        }
        if (CurrentUIType == type) return;
        switch (type)
        {
            case UIType.Legacy:
                {
                    CurrentUIType = type;
                    legacyUIRoot.SetActive(true);
                    trgUIRoot.SetActive(false);
                    break;
                }
            case UIType.TrgUI:
                {
                    CurrentUIType = type;
                    legacyUIRoot.SetActive(false);
                    trgUIRoot.SetActive(true);
                    break;
                }
        }
    }

    public async UniTask ReportMeterBpmAsync(SimaiChart chart)
    {
        await UniTask.RunOnThreadPool(() =>
        {
            foreach (var timing in chart.CommaTimings)
            {
                var (lastNum, lastDeno) = meterList.LastOrDefault().Value;
                if (timing.SignatureNumerator != lastNum || timing.SignatureDenominator != lastDeno)
                    meterList.TryAdd(timing.Timing, (timing.SignatureNumerator, timing.SignatureDenominator));
                if (timing.Bpm != bpmList.LastOrDefault().Value)
                    bpmList.TryAdd(timing.Timing, timing.Bpm);
            }
        });
        float min, max;
        min = max = bpmList.FirstOrDefault().Value;
        foreach (var bpm in bpmList.Values)
        {
            if (bpm < min) min = bpm;
            if (bpm > max) max = bpm;
        }
        objBpmRange.text = $"{min} ～ {max}";
    }

    private void UpdateOutput()
    {
        OutputMain();
        OutputSide();
        OutputTime();
    }

    private void OutputMain()
    {
        switch (TextMode)
        {
            case BgInfoDisplay.Combo:
                {
                    statusCombo.text = combo > 0 ?
                        combo.ToString() : string.Empty;
                }
                break;
            case BgInfoDisplay.Achievement_101:
                {
                    statusAchievement.text = $"{accRate[2]:0.0000}%";
                    UpdateAchievementColor(accRate[2], statusAchievement);
                }
                break;
            case BgInfoDisplay.Achievement_100:
                {
                    statusAchievement.text = $"{accRate[3]:0.0000}%";
                    UpdateAchievementColor(accRate[3], statusAchievement);
                }
                break;
            case BgInfoDisplay.Achievement:
                {
                    statusAchievement.text = $"{accRate[4]:0.0000}%";
                    UpdateAchievementColor(accRate[4], statusAchievement);
                }
                break;
            case BgInfoDisplay.AchievementClassical:
                {
                    statusAchievement.text = $"{accRate[0]:0.0000}%";
                    UpdateAchievementColor(accRate[0], statusAchievement);
                }
                break;
            case BgInfoDisplay.AchievementClassical_100:
                {
                    statusAchievement.text = $"{accRate[1]:0.0000}%";
                    UpdateAchievementColor(accRate[1], statusAchievement);
                }
                break;
            case BgInfoDisplay.DXScore:
                {
                    statusDXScore.text = (totalDXScore + lostDXScore).ToString();
                }
                break;
            case BgInfoDisplay.S_Border:
                {
                    var rate = accRate[2] - 97;
                    UpdateBorder(rate, statusAchievement);
                }
                break;
            case BgInfoDisplay.SS_Border:
                {
                    var rate = accRate[2] - 99;
                    UpdateBorder(rate, statusAchievement);
                }
                break;
            case BgInfoDisplay.SSS_Border:
                {
                    var rate = accRate[2] - 100;
                    UpdateBorder(rate, statusAchievement);
                }
                break;
        }
        void UpdateAchievementColor(double rate, Text textElement)
        {
            var newColor = rate switch
            {
                >= 100 => AchievementGoldColor,
                >= 97f => AchievementSilverColor,
                >= 80f => AchievementBronzeColor,
                _ => AchievementDudColor
            };

            if (textElement.color != newColor)
            {
                textElement.color = newColor;
            }

            var headerElement = textElement.transform.GetChild(0).GetComponent<Text>();
            if (headerElement.color != newColor)
            {
                headerElement.color = newColor;
            }
        }

        void UpdateBorder(double rate, Text textElement)
        {
            if (rate <= 0)
            {
                textElement.gameObject.SetActive(false);
                return;
            }
            textElement.text = $"{rate:0.0000}%";
        }
    }

    private void OutputSide()
    {
        if (CurrentUIType is UIType.Legacy)
        {
            objectCount.text = string.Format(
                "TAP: {0} / {5}\n" +
                "HOD: {1} / {6}\n" +
                "SLD: {2} / {7}\n" +
                "TOH: {3} / {8}\n" +
                "BRK: {4} / {9}\n" +
                "ALL: {10} / {11}\n" +
                "MOD: {12}",
                TapFinishedCount, HoldFinishedCount, SlideFinishedCount, TouchFinishedCount, BreakFinishedCount,
                TapSum, HoldSum, SlideSum, TouchSum, BreakSum,
                NoteFinishedCount, NoteSum,
                NoteHelper.AutoPlayMode switch
                {
                    AutoPlayMode.Enable => "Enable",
                    AutoPlayMode.DJAutoButton => "DJAuto (Btn)",
                    AutoPlayMode.DJAutoSensor => "DJAuto",
                    AutoPlayMode.Random => "Random",
                    AutoPlayMode.Disable => "Disable",
                    _ => "INVALID"
                }
            );

            objectRate.text = string.Format(
                "FiNALE  Rate:\n" +
                $"{ClassicRateFromCount:000.00}   %\n" +
                "DELUXE Rate:\n" +
                $"{DeluxeRateFromCount:000.0000} % "
            );

            judgeResultCount.text = $"{cPerfectCount}\n" +
                                    $"{perfectCount}\n" +
                                    $"{greatCount}\n" +
                                    $"{goodCount}\n" +
                                    $"{missCount}\n\n" +
                                    $"{fastCount}\n" +
                                    $"{lateCount}";
        }
        else
        {
            objNoteCount.text =
                $"{TapFinishedCount} / {TapSum}\n" +
                $"{HoldFinishedCount} / {HoldSum}\n" +
                $"{SlideFinishedCount} / {SlideSum}\n" +
                $"{TouchFinishedCount} / {TouchSum}\n" +
                $"{BreakFinishedCount} / {BreakSum}\n" +
                $"{NoteFinishedCount} / {NoteSum}";

            var rate = DeluxeRateFromCount;
            var intPart = (int)rate;
            var fracPart = (rate - intPart) * 10000;
            objRate.text =
                $"<size=7.5>{intPart:0}</size><size=5.7>.{fracPart:0000}</size> <size=3.7>%</size>";

            objJudgeResult.text =
                $"{cPerfectCount}\n{perfectCount}\n{greatCount}\n{goodCount}\n{missCount}";

            objCombo.text = combo.ToString();

            var time = _timeProvider.NoteTime;
            for (var i = meterList.Count - 1; i >= 0; i--)
            {
                var meter = meterList.ElementAt(i);
                if (meter.Key > time) continue;

                var (num, deno) = meter.Value;
                objMeter.text = $"{num}\n{deno}";
                break;
            }
            for (var i = bpmList.Count - 1; i >= 0; i--)
            {
                var bpm = bpmList.ElementAt(i);
                if (bpm.Key > time) continue;

                objBpm.text = bpm.Value.ToString();
                break;
            }
        }
    }
    private void OutputTime()
    {
        var ctime = _timeProvider.AudioTime;
        var timeNowInt = (int)ctime;
        var minute = timeNowInt / 60;
        var second = timeNowInt - 60 * minute;
        double milli = (ctime - timeNowInt) * 10000;

        string target;
        if (ctime < 0)
        {
            minute = Math.Abs(minute);
            second = Math.Abs(second);
            milli = Math.Abs(milli);
            target = string.Format("-{0}:{1:00}.{2:000}", minute, second, milli / 10);
        }
        else
        {
            target = string.Format("{0}:{1:00}.{2:0000}", minute, second, milli);
        }

        if (CurrentUIType == UIType.Legacy)
            timeDisplay.text = target;
        else
            objTime.text = target;
    }
}
