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
        bool legacySlideLayer,
        bool smoothSlideAnime)
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

        MajBurst.InputData.ResetIndex();

        _noteManager.NoteSpeed = noteSpeed;
        _noteManager.TouchSpeed = touchSpeed;
        _noteManager.legacySlideLayer = legacySlideLayer;
        _noteManager.smoothSlideAnime = smoothSlideAnime;
        _noteManager.Load(chart);

        await UniTask.Yield();
    }

    private static string GetDifficultyText(int difficulty)
    {
        return difficulty switch
        {
            0 => "Basic",
            1 => "Advanced",
            2 => "Expert",
            3 => "Master",
            4 => "Re:Master",
            _ => "?"
        };
    }

    public void ResetState()
    {
    }
}
