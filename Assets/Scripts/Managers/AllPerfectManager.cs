#nullable enable

using Cysharp.Threading.Tasks;
using MajdataViewX.Types.Enums;
using UnityEngine;

using static MajdataViewX.Base.MajCtx;

namespace MajdataViewX.Managers
{
    public class AllPerfectManager : MonoBehaviour
    {
        private static readonly int PlayAllPerfectHash = Animator.StringToHash("playAllPerfect");
        [SerializeField]
        private Animator AllPerfect;

        private bool isPlayed;

        private void Awake()
        {
            _allPerfectManager = this;
        }

        private void Start()
        {
            AllPerfect.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (PlayManager.Summary.State is not ViewStatus.Playing)
                return;

            if (_objectCounter.AllFinished)
            {
                if (isPlayed)
                {
                    if (!AllPerfect.gameObject.activeSelf)
                    {
                        _playManager.StopAsync().Forget();
                        _wsServer.SendStopResponse();
                    }
                }
                else
                {
                    AllPerfect.gameObject.SetActive(true);
                    AllPerfect.SetTrigger(PlayAllPerfectHash);
                    _audioManager.noteSfxPlaybackRequests[AudioManager.ALL_PERFECT] = true;
                    isPlayed = true;
                }
            }
        }

        public void ResetState()
        {
            AllPerfect.gameObject.SetActive(false);
            isPlayed = false;
        }
    }
}