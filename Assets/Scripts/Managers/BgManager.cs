using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class BgManager : MonoBehaviour
{
    private TimeProvider provider;
    
    private RawImage rawImage;
    private GameObject songDetail;
    private SpriteRenderer spriteRender;
    private VideoPlayer videoPlayer;

    private float smoothRDelta;
    private float playSpeed;
    private float originalScaleX;

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
        rawImage = GameObject.Find("Jacket").GetComponent<RawImage>();
        songDetail = GameObject.Find("CanvasSongDetail");
        songDetail.SetActive(false);
    }

    private void Update()
    {
        var delta = (float)videoPlayer.clockTime - provider.AudioTime;
        smoothRDelta += (Time.unscaledDeltaTime - smoothRDelta) * 0.01f;
        if (provider.AudioTime < 0) return;
        var realSpeed = Time.deltaTime / smoothRDelta;

        if (Time.captureFramerate != 0)
        {
            //print("speed="+realSpeed+" delta="+delta);
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

    public void ContinueVideo(float speed)
    {
        videoPlayer.playbackSpeed = speed;
        playSpeed = speed;
        videoPlayer.Play();
    }


    public void LoadBGFromPath(string path, float speed)
    {
        var pictureName = new[] { "Cover", "bg" };
        var pictureExt = new[] { ".png", ".jpg", ".jpeg" };

        var videoName = new[] { "pv.mp4", "mv.mp4", "bg.mp4" };

        foreach (var name in pictureName)
        {
            var finished = false;
            foreach (var ext in pictureExt)
                if (File.Exists(path + "/" + name + ext))
                {
                    StartCoroutine(loadPic(path + "/" + name + ext));
                    finished = true;
                    break;
                }

            if (finished) break;
        }

        foreach (var name in videoName)
        {
            if (!File.Exists(path + "/" + name)) continue;
            
            loadVideo(path + "/" + name, speed);
            break;
        }
    }

    private IEnumerator loadPic(string path)
    {
        Sprite sprite;
        yield return sprite = SpriteLoader.Load(path);
        rawImage.texture = sprite.texture;
        spriteRender.sprite = sprite;
        var scale = 1140f / sprite.texture.width;
        gameObject.transform.localScale = new Vector3(scale, scale, scale);
    }

    private void loadVideo(string path, float speed)
    {
        videoPlayer.url = "file://" + path;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPlayer.playbackSpeed = speed;
        playSpeed = speed;
        StartCoroutine(waitFumenStart());
    }

    private IEnumerator waitFumenStart()
    {
        videoPlayer.Prepare();
        //videoPlayer.timeReference = VideoTimeReference.ExternalTime;
        while (provider.AudioTime <= 0) yield return new WaitForEndOfFrame();
        while (!videoPlayer.isPrepared) yield return new WaitForEndOfFrame();
        videoPlayer.Play();
        videoPlayer.time = provider.AudioTime;

        var scale = videoPlayer.height / (float)videoPlayer.width;
        spriteRender.sprite =
            Sprite.Create(new Texture2D(1080, 1080), new Rect(0, 0, 1080, 1080), new Vector2(0.5f, 0.5f));
        
        gameObject.transform.localScale = new Vector3(originalScaleX, originalScaleX * scale);
    }
}