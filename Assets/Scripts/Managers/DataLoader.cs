#nullable enable

#region

using Cysharp.Threading.Tasks;
using MajSimai;
using UnityEngine;
using UnityEngine.UI;

using static MajCtx;
using static MajBurst;

#endregion

public class DataLoader : MonoBehaviour
{
    //serialized field
    public Text diffText;
    public Text levelText;
    public Text titleText;
    public Text artistText;
    public Text designText;
    public RawImage cardImage;
    public Color[] diffColors = new Color[7];
    public Text errText;

    private void Awake()
    {
        _dataLoader = this;
    }

    public async UniTask Load(
        SimaiChart chart,
        double ignoreOffset,
        string title,
        string artist,
        int diff,
        float noteSpeed,
        float touchSpeed,
        bool smoothSlideAnime,
        bool legacySlideLayer,
        bool mineAutoSlide)
    {
        titleText.text = title;
        artistText.text = artist;
        diffText.text = GetDifficultyText(diff);
        cardImage.color = diffColors[diff];
        levelText.text = chart.Level;
        designText.text = chart.Designer;

        _objectCounter.CountNoteSumAsync(chart).Forget();
        _objectCounter.ReportMeterBpmAsync(chart).Forget();

        _timeProvider.LoadSV(chart.CommaTimings);

        _noteManager.NoteSpeed = noteSpeed;
        _noteManager.TouchSpeed = touchSpeed;
        _noteManager.SmoothSlideAnime = smoothSlideAnime;
        _noteManager.LegacySlideLayer = legacySlideLayer;
        _noteManager.Ignore = ignoreOffset;
        _noteManager.MineAutoSlide = mineAutoSlide;
        _noteManager.Load(chart);

        await UniTask.Yield();
    }

    public static string GetDifficultyText(int index) =>
        index switch
        {
            0 => "EASY",
            1 => "BASIC",
            2 => "ADVANCED",
            3 => "EXPERT",
            4 => "MASTER",
            5 => "Re:MASTER",
            6 => "ORIGINAL",
            _ => "DEFAULT"
        };

    public void ResetState()
    {
    }
}
