#nullable enable


using MajdataViewX.Native;
using MajdataViewX.Utils;
using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

using static MajdataViewX.Base.MajBurst;
using static MajdataViewX.Base.MajCtx;

namespace MajdataViewX.Managers
{
    public class BgManager : MonoBehaviour
    {
        [SerializeField]
        private Sprite bgDummy;
        [SerializeField]
        private Sprite defaultBg;

        [SerializeField]
        private Material fullscreenBgMaterial;
        [SerializeField]
        private Material circledBgMaterial;

        private RawImage jacketImage;
        private GameObject songDetail;
        private static readonly int ShowHash = Animator.StringToHash("show");
        private Animator detailAnim;
        private SpriteRenderer spriteRender;
        private VideoPlayer videoPlayer;
        private BgVideoPipe? videoPipe;

        // Preloaded while the chart loads so pressing play starts the video
        // instantly instead of probing + spawning ffmpeg on the play path.
        private BgVideoPipe? _idlePipe;
        private (int Width, int Height, double Fps)? _probedInfo;
        private Task? _preloadTask;
        private int _preloadGeneration;

        private float smoothRDelta;
        private double _recordLastRealTime;

        const float CIRCLED_SCALE_X = 1.1f;
        const float FULLSCREEN_SCALE_X = 1.777f;

        private Sprite? Bg { get; set; }
        private string? VideoUrl { get; set; }
        private string? VideoPath { get; set; }
        private bool _resizeBg;

        public static bool hasBg;
        public static bool hasVideo;
        public bool IsBgLoaded => !hasBg || Bg != null;
        public bool IsVideoLoaded => !hasVideo || !string.IsNullOrWhiteSpace(VideoUrl);

        private static Sprite? _emptySprite;

        private void Awake()
        {
            _bgManager = this;
        }

        private void Start()
        {
            jacketImage = GameObject.Find("Jacket").GetComponent<RawImage>();
            songDetail = GameObject.Find("CanvasSongDetail");
            songDetail.SetActive(false);

            spriteRender = GetComponent<SpriteRenderer>();
            videoPlayer = GetComponent<VideoPlayer>();
            detailAnim = songDetail.GetComponent<Animator>();

            _emptySprite = Sprite.Create(new Texture2D(1080, 1080), new Rect(0, 0, 1080, 1080), new Vector2(0.5f, 0.5f));
        }

        private void Update()
        {
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            if (videoPipe != null)
            {
                videoPipe.UpdateFrame();
                spriteRender.sprite = videoPipe.Sprite;
                return;
            }
#endif

            var delta = (float)videoPlayer.clockTime - _timeProvider.AudioTime;
            smoothRDelta += (Time.unscaledDeltaTime - smoothRDelta) * 0.01f;
            if (_timeProvider.AudioTime < 0) return;

            if (_timeProvider.IsRecord)
            {
                // Export advances the song timeline by frame count and can run
                // faster than real time. Track it with playbackSpeed (VideoPlayer
                // caps at 16x); beyond that, seek directly.
                // Time.unscaledDeltaTime is pinned by Time.captureDeltaTime during
                // export, so measure real time via realtimeSinceStartupAsDouble.
                var now = Time.realtimeSinceStartupAsDouble;
                var realTimeStep = (float)(now - _recordLastRealTime);
                _recordLastRealTime = now;
                var songSpeed = realTimeStep > 0.0001f
                    ? TimeData.deltaTime / realTimeStep
                    : 1f;
                if (songSpeed <= 16f)
                {
                    videoPlayer.playbackSpeed = Mathf.Clamp(songSpeed, 0.05f, 16f);
                }
                else if (Mathf.Abs(delta) > 0.05f)
                {
                    videoPlayer.time = Mathf.Max(0, _timeProvider.AudioTime);
                }
                return;
            }

            var realSpeed = Time.deltaTime / smoothRDelta;

            if (Time.captureFramerate != 0)
            {
                videoPlayer.playbackSpeed = realSpeed - delta;
                return;
            }

            if (delta < -0.01f)
                videoPlayer.playbackSpeed = _timeProvider.CurrentSpeed + 0.2f;
            else if (delta > 0.01f)
                videoPlayer.playbackSpeed = _timeProvider.CurrentSpeed - 0.2f;
            else
                videoPlayer.playbackSpeed = _timeProvider.CurrentSpeed;
        }

        public void PlaySongDetail()
        {
            songDetail.SetActive(true);
            detailAnim.SetTrigger(ShowHash);
        }

        public void LoadBG(string path)
        {
            DestroyLoadedBackground();
            Bg = TexLoader.LoadSprite(path);
        }

        private void DestroyLoadedBackground()
        {
            if (Bg != null)
            {
                if (Bg.texture != null)
                    Destroy(Bg.texture);

                Destroy(Bg);
                Bg = null;
            }
        }

        public void ShowBG()
        {
            if (Bg == null || !hasBg)
            {
                jacketImage.texture = bgDummy.texture;
                spriteRender.sprite = defaultBg;
                return;
            }

            jacketImage.texture = Bg.texture;
            spriteRender.sprite = Bg;
            var scale = 1140f / Bg.texture.width;
            gameObject.transform.localScale = new Vector3(scale, scale, scale);
        }

        public void LoadVideo(string path)
        {
            // VideoPlayer requires a proper URL; "file://" + raw path breaks on
            // spaces/unicode (e.g. "起死開戦 (Kishi Kaisen)/pv.mp4"). Build a
            // percent-encoded file URL instead.
            VideoUrl = new Uri(path).AbsoluteUri;
            VideoPath = path;

            // Untie the ffmpeg process from play: probe (spawns ffprobe, slow)
            // and spawn the pipe on a background task while the chart loads,
            // so the first play starts the video with no delay.
            _preloadTask = PreloadVideoAsync(path);
        }

        async Task PreloadVideoAsync(string path)
        {
            var gen = ++_preloadGeneration;
            try
            {
                var info = await Task.Run(() => BgVideoPipe.ProbeVideoInfo(path));
                if (gen != _preloadGeneration)
                    return; // superseded by a newer load/preload
                _probedInfo = info;
                var pipe = BgVideoPipe.Create(info.Width, info.Height, info.Fps);
                if (pipe.TryStart(path, 0))
                {
                    _idlePipe?.Dispose();
                    _idlePipe = pipe;
                }
                else
                {
                    pipe.Dispose();
                    Debug.LogError($"[BgManager] ffmpeg video preload failed to start: {path}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BgManager] video preload failed: {e.Message}");
            }
        }

        public Task ShowVideoAsync(bool resizeBg, double startAt = 0)
        {
            if (!hasVideo) return Task.CompletedTask;
            _resizeBg = resizeBg;

#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            // Unity's VideoPlayer has no H.264 decoder on Linux; decode with
            // ffmpeg into a texture instead.
            return ShowVideoPipeAsync(resizeBg, startAt);
#else
            ShowVideoPlayer(resizeBg);
            return Task.CompletedTask;
#endif
        }

        void ShowVideoPlayer(bool resizeBg)
        {
            if (!hasVideo) return;

            videoPlayer.url = VideoUrl;
            StartCoroutine(WaitFumenStart());
            IEnumerator WaitFumenStart()
            {
                videoPlayer.Prepare();

                //secret hack: if not so, the bg won't be set to defaultBg but full white
                spriteRender.sprite = _emptySprite;

                while (_timeProvider.AudioTime <= 0) yield return new WaitForEndOfFrame();

                // Don't hang forever if Prepare() fails (bad URL, unsupported codec).
                var timeout = Time.realtimeSinceStartup + 15f;
                while (!videoPlayer.isPrepared)
                {
                    if (Time.realtimeSinceStartup > timeout)
                    {
                        Debug.LogError($"[BgManager] Video prepare timed out: {videoPlayer.url}");
                        yield break;
                    }
                    yield return new WaitForEndOfFrame();
                }
                if (videoPlayer.isPlaying) videoPlayer.Stop();

                videoPlayer.Play();
                videoPlayer.time = _timeProvider.AudioTime;

                var scale = videoPlayer.height / (float)videoPlayer.width;
                if (resizeBg)
                {
                    gameObject.transform.localScale = new Vector3(FULLSCREEN_SCALE_X, FULLSCREEN_SCALE_X * scale);
                    spriteRender.material = fullscreenBgMaterial;
                }
                else
                {
                    gameObject.transform.localScale = new Vector3(CIRCLED_SCALE_X, CIRCLED_SCALE_X * scale);
                    spriteRender.material = circledBgMaterial;
                }
            }
        }

        async Task ShowVideoPipeAsync(bool resizeBg, double startAt = 0)
        {
            StopVideoPipe();

            // Invalidate any in-flight preload: it must not spawn a second
            // decoder while this play takes/spawns the pipe (a leaked process
            // also kept the export encoder's stdin write end open, blocking
            // its EOF and hanging the finalize's waitpid).
            _preloadGeneration++;

            // AudioTime is reset by SetStartTime AFTER ShowVideo is called, so
            // use the play's start time directly: seek there and pace the video
            // from it, matching the song clock.
            var startSec = Math.Max(0, startAt);

            BgVideoPipe? pipe = null;
            if (startSec <= 0.05 && _idlePipe != null)
            {
                // Playing from the top: the preloaded pipe is already decoded
                // and parked at 0:00 — instant.
                pipe = _idlePipe;
                _idlePipe = null;
            }
            else
            {
                // Mid-song play: spawn a fresh pipe seeking to the start time.
                // The probe is cached (or runs in a background task), so this
                // only costs an ffmpeg spawn, not a main-thread stall.
                _idlePipe?.Dispose();
                _idlePipe = null;
                var info = _probedInfo ??
                    await Task.Run(() => BgVideoPipe.ProbeVideoInfo(VideoPath));
                _probedInfo = info;
                pipe = BgVideoPipe.Create(info.Width, info.Height, info.Fps);
                if (!pipe.TryStart(VideoPath, startSec))
                {
                    pipe.Dispose();
                    pipe = null;
                }
            }

            if (pipe == null)
            {
                Debug.LogError($"[BgManager] ffmpeg video failed to start: {VideoPath}");
                return;
            }
            videoPipe = pipe;

            // The video is always limited to the playfield: the circled
            // material clips it to the play circle. Best-fit scale: the whole
            // video stays visible inside the playfield (no crop), with black
            // bars on the short sides where the aspect doesn't fill.
            var aspect = (float)videoPipe.Height / videoPipe.Width;
            var s = Mathf.Min(1f, 1f / aspect);
            gameObject.transform.localScale = new Vector3(s, s);
            spriteRender.material = circledBgMaterial;
        }

        void StopVideoPipe()
        {
            if (videoPipe != null)
            {
                videoPipe.Dispose();
                videoPipe = null;
            }
            spriteRender.sprite = null;
        }

        public void PauseVideo()
        {
            if (!hasVideo) return;
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            // Stopping the read lets ffmpeg's pipe fill and block: the video
            // freezes on its last frame without killing the process.
            videoPipe?.Pause();
            return;
#else
            videoPlayer.Pause();
#endif
        }

        public void ContinueVideo()
        {
            if (!hasVideo) return;
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            videoPipe?.Resume();
            return;
#else
            videoPlayer.Play();
#endif
        }

        public void ResetState()
        {
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            StopVideoPipe();
            // Re-arm a parked pipe in the background so the next play is
            // instant too (the stopped pipe is consumed, not reusable).
            if (!string.IsNullOrWhiteSpace(VideoPath))
                _preloadTask = PreloadVideoAsync(VideoPath);
#else
            videoPlayer.Stop();
#endif
            // 销毁上一曲背景图(Texture2D/Sprite)，避免滞留到下次 LoadBG
            DestroyLoadedBackground();
            gameObject.transform.localScale = new Vector3(CIRCLED_SCALE_X, CIRCLED_SCALE_X, CIRCLED_SCALE_X);
            spriteRender.material = circledBgMaterial;
            spriteRender.sprite = defaultBg;
            smoothRDelta = 0f;

            if (songDetail != null)
                songDetail.SetActive(false);
        }

        private void OnDestroy()
        {
            StopVideoPipe();
            if (_idlePipe != null)
            {
                _idlePipe.Dispose();
                _idlePipe = null;
            }
            DestroyLoadedBackground();
            if (_emptySprite != null)
            {
                var texture = _emptySprite.texture;
                Destroy(_emptySprite);
                if (texture != null)
                    Destroy(texture);
                _emptySprite = null;
            }
        }
    }
}