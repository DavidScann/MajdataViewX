#nullable enable

using Cysharp.Threading.Tasks;
using Cimai;
using UnityEngine;
using UnityEngine.UI;

using static MajdataViewX.Base.MajCtx;

namespace MajdataViewX.Managers
{
    public class DataLoader : MonoBehaviour
    {
        //serialized field
        public Text diffText = null!;
        public Text levelText = null!;
        public Text titleText = null!;
        public Text artistText = null!;
        public Text designText = null!;
        public RawImage cardImage = null!;
        public Color[] diffColors = new Color[7];

        private void Awake()
        {
            _dataLoader = this;
        }

        public async UniTask Load(
            SimaiChart chart,
            string title,
            string artist,
            string level,
            string designer,
            int diff)
        {
            titleText.text = title;
            artistText.text = artist;
            diffText.text = GetDifficultyText(diff);
            cardImage.color = diffColors[diff];
            levelText.text = level;
            designText.text = designer;

            _timeProvider.LoadSV(chart.Timings);
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