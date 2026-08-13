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
        // Show.anim is 3.13s plus a 0.25s transition; wait it out before
        // stopping (the animator holds the last frame, so the game object
        // never deactivates on its own).
        const float AP_SHOW_DURATION = 3.5f;
        [SerializeField]
        private Animator AllPerfect;

        private bool isPlayed;
        private float apStartedAt;

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

            // The AP banner is the end marker for every play, including
            // Record/export mode: it triggers once all notes are judged,
            // regardless of whether a Perfect was lost, and the export stops
            // when the banner has played out.
            if (_objectCounter.AllFinished)
            {
                if (isPlayed)
                {
                    if (Time.time - apStartedAt >= AP_SHOW_DURATION)
                    {
                        // In record mode the capture loop runs to
                        // recordEndTime (last note + banner tail) and its
                        // natural end sends PlayStopped; stopping here would
                        // cut the export at the same instant the counter
                        // fills, before the banner frames are captured.
                        if (_timeProvider.IsRecord) return;
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
                    apStartedAt = Time.time;
                }
            }
        }

        public void ResetState()
        {
            AllPerfect.gameObject.SetActive(false);
            isPlayed = false;
            apStartedAt = 0f;
        }
    }
}