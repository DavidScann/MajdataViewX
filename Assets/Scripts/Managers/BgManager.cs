#nullable enable

#region

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

using static MajCtx;

#endregion

public class BgManager : MonoBehaviour
{
    [SerializeField]
    private Sprite bgDummy;
    [SerializeField]
    private Sprite defaultBg;

    private RawImage jacketImage;
    private GameObject songDetail;
    private static readonly int ShowHash = Animator.StringToHash("show");
    private Animator detailAnim;
    private SpriteRenderer spriteRender;
    private VideoPlayer videoPlayer;

    private float smoothRDelta;
    private float originalScaleX;

    private Sprite? Bg { get; set; }
    private string? VideoUrl { get; set; }

    public static bool hasBg;
    public static bool hasVideo;
    public bool IsBgLoaded => !hasBg || Bg != null;
    public bool IsVideoLoaded => !hasVideo || !string.IsNullOrWhiteSpace(VideoUrl);

    private void Awake()
    {
        _bgManager = this;
    }

    private void Start()
    {
        jacketImage = GameObject.Find("Jacket").GetComponent<RawImage>();
        songDetail = GameObject.Find("CanvasSongDetail");
        songDetail.SetActive(false);

        originalScaleX = gameObject.transform.localScale.x;
        spriteRender = GetComponent<SpriteRenderer>();
        videoPlayer = GetComponent<VideoPlayer>();
        detailAnim = songDetail.GetComponent<Animator>();
    }

    private void Update()
    {
        var delta = (float)videoPlayer.clockTime - _timeProvider.AudioTime;
        smoothRDelta += (Time.unscaledDeltaTime - smoothRDelta) * 0.01f;
        if (_timeProvider.AudioTime < 0) return;
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
        Bg = SpriteLoader.Load(path);
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
        VideoUrl = new Uri(path).AbsoluteUri;
    }

    public void ShowVideo()
    {
        if (!hasVideo)
            return;

        videoPlayer.url = VideoUrl;
        StartCoroutine(WaitFumenStart());
        IEnumerator WaitFumenStart()
        {
            videoPlayer.Prepare();

            //secret hack: if not so, the bg won't be set to defaultBg but full white
            spriteRender.sprite =
                Sprite.Create(new Texture2D(1080, 1080), new Rect(0, 0, 1080, 1080), new Vector2(0.5f, 0.5f));

            while (_timeProvider.AudioTime <= 0) yield return new WaitForEndOfFrame();
            while (!videoPlayer.isPrepared) yield return new WaitForEndOfFrame();
            videoPlayer.Play();
            videoPlayer.time = _timeProvider.AudioTime;

            var scale = videoPlayer.height / (float)videoPlayer.width;
            gameObject.transform.localScale = new Vector3(originalScaleX, originalScaleX * scale);
        }
    }

    public void PauseVideo()
    {
        if (!hasVideo) return;
        videoPlayer.Pause();
    }

    public void ContinueVideo()
    {
        if (!hasVideo) return;
        videoPlayer.Play();
    }

    public void ResetState()
    {
        videoPlayer.Stop();
        gameObject.transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);
        spriteRender.sprite = defaultBg;
        smoothRDelta = 0f;

        if (songDetail != null)
            songDetail.SetActive(false);
    }
}