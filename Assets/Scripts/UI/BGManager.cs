using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class BGManager : MonoBehaviour
{
    public bool isMaimai;

    private float playSpeed;
    private AudioTimeProvider provider;

    private RawImage rawImage;
    private SpriteRenderer rawImageM;

    // Update is called once per frame
    private float smoothRDelta;
    private GameObject SongDetail;
    private GameObject SongDetailM;
    private SpriteRenderer spriteRender;

    private VideoPlayer videoPlayer;

    private float originalScaleX;
    // Start is called before the first frame update
    private void Start()
    {
        originalScaleX = gameObject.transform.localScale.x;
        spriteRender = GetComponent<SpriteRenderer>();
        videoPlayer = GetComponent<VideoPlayer>();
        rawImage = GameObject.Find("Jacket").GetComponent<RawImage>();
        rawImageM = GameObject.Find("JacketM").GetComponent<SpriteRenderer>();
        provider = GameObject.Find("AudioTimeProvider").GetComponent<AudioTimeProvider>();
        SongDetail = GameObject.Find("CanvasSongDetail");
        SongDetailM = GameObject.Find("CanvasSongDetailMaimai");
        SongDetail.SetActive(false);
        SongDetailM.SetActive(false);
        isMaimai = false;
    }

    private void Update()
    {
        //videoPlayer.externalReferenceTime = provider.AudioTime;
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
        if (isMaimai) SongDetailM.SetActive(true);
        else SongDetail.SetActive(true);
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
        var pictureName = "bg";
        var pictureNameM = "bgM";
        var pictureExt = new[] { ".png", ".jpg", ".jpeg" };

        var videoName = "pv.mp4";
        foreach (var ext in pictureExt)
        {
            if (File.Exists(path + "/" + pictureName + ext))
            {
                StartCoroutine(loadPic(path + "/" + pictureName + ext));
                break;
            }
            else if (File.Exists(path + "/" + pictureNameM + ext))
            {
                isMaimai = true;
                StartCoroutine(loadPic(path + "/" + pictureNameM + ext, true));
                break;
            }
        }

        if (File.Exists(path + "/" + videoName)) loadVideo(path + "/" + videoName, speed);
    }

    private IEnumerator loadPic(string path, bool isMaimai = false)
    {
        Sprite sprite;
        yield return sprite = SpriteLoader.LoadSpriteFromFile(path);
        if (isMaimai)
        {
            rawImageM.sprite = sprite;
        }
        else rawImage.texture = sprite.texture;
        spriteRender.sprite = sprite;
        var scale = 1080f / sprite.texture.width;
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