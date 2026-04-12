using System;
using System.Collections;
using System.IO;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
#nullable enable

public class BgManager : MonoBehaviour
{
    private TimeProvider provider;

    [SerializeField] 
    private Sprite defaultBg;
    
    private RawImage jacketImage;
    private GameObject songDetail;
    private SpriteRenderer spriteRender;
    private VideoPlayer videoPlayer;

    private float smoothRDelta;
    private float playSpeed;
    private float originalScaleX;

    private Sprite? Bg;

    private void Awake()
    {
        Majdata<BgManager>.Instance = this;
    }

    private void Start()
    {
        provider = Majdata<TimeProvider>.Instance!;
        
        originalScaleX = gameObject.transform.localScale.x;
        spriteRender = GetComponent<SpriteRenderer>();
        videoPlayer = GetComponent<VideoPlayer>();
        jacketImage = GameObject.Find("Jacket").GetComponent<RawImage>();
        songDetail = GameObject.Find("CanvasSongDetail");
        songDetail.SetActive(false);
    }

    private void Update()
    {
        var delta = (float)videoPlayer.clockTime - provider.NoteTime;
        smoothRDelta += (Time.unscaledDeltaTime - smoothRDelta) * 0.01f;
        if (provider.NoteTime < 0) return;
        var realSpeed = Time.deltaTime / smoothRDelta;

        if (Time.captureFramerate != 0)
        {
            videoPlayer.playbackSpeed = realSpeed - delta;
            return;
        }

        if (delta < -0.01f)
            videoPlayer.playbackSpeed = playSpeed + 0.2f;
        else if (delta > 0.01f)
            videoPlayer.playbackSpeed = playSpeed - 0.2f;
        else
            videoPlayer.playbackSpeed = playSpeed;
    }

    public void PlaySongDetail()
    {
        songDetail.SetActive(true);
    }

    public void PauseVideo()
    {
        videoPlayer.Pause();
    }

    public void ContinueVideo()
    {
        videoPlayer.Play();
    }

    public void SetSpeed(float speed)
    {
        videoPlayer.playbackSpeed = speed;
        playSpeed = speed;
    }
    
    public void LoadBG(string path)
    {
        Bg = SpriteLoader.Load(path);
        jacketImage.texture = Bg.texture;
    }

    public void ShowBG()
    {
        if (Bg == null) return;
        
        spriteRender.sprite = Bg;
        var scale = 1140f / Bg.texture.width;
        gameObject.transform.localScale = new Vector3(scale, scale, scale);
    }

    public void LoadVideo(string path)
    {
        videoPlayer.url = "file://" + path;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
    }

    public void ShowVideo()
    {
        StartCoroutine(waitFumenStart());
    }

    private IEnumerator waitFumenStart()
    {
        videoPlayer.Prepare();
        while (provider.NoteTime <= 0) yield return new WaitForEndOfFrame();
        while (!videoPlayer.isPrepared) yield return new WaitForEndOfFrame();
        videoPlayer.Play();
        videoPlayer.time = provider.NoteTime;

        var scale = videoPlayer.height / (float)videoPlayer.width;
        spriteRender.sprite =
            Sprite.Create(new Texture2D(1080, 1080), new Rect(0, 0, 1080, 1080), new Vector2(0.5f, 0.5f));
        
        gameObject.transform.localScale = new Vector3(originalScaleX, originalScaleX * scale);
    }
}