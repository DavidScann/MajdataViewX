#nullable enable

using Cysharp.Threading.Tasks;
using MajSimai;
using UnityEngine;
using UnityEngine.UI;

using static MajdataViewX.Base.MajCtx;

namespace MajdataViewX.Managers
{
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

        private void Awake()
        {
            _dataLoader = this;
        }

        public async UniTask Load(
            SimaiChart chart,
            string title,
            string artist,
            int diff)
        {
            titleText.text = title;
            artistText.text = artist;
            diffText.text = GetDifficultyText(diff);
            cardImage.color = diffColors[diff];
            levelText.text = chart.Level;
            designText.text = chart.Designer;

            _timeProvider.LoadSV(chart.CommaTimings);

            _noteManager.Load(chart);

            await UniTask.Yield();
        }

        private static string GetDifficultyText(int index) =>
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
    }
}