#region

using System;
using System.IO;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;

#endregion

public class ScreenRecorder : MonoBehaviour
{
    TimeProvider timeProvider;
    BgManager bgManager;
    AudioManager audioManager;

    Text errText;

    public bool IsRecording { get; private set; }

    private void Awake()
    {
        Majdata<ScreenRecorder>.Instance = this;
    }

    private void Start()
    {
        timeProvider = Majdata<TimeProvider>.Instance!;
        bgManager = Majdata<BgManager>.Instance!;
        audioManager = Majdata<AudioManager>.Instance!;
        errText = GameObject.Find("ErrText").GetComponent<Text>();
    }

    public async UniTask StartRecording(string maidataPath,
        int fps, bool resizeBg, [CanBeNull] Action onStart = null)
    {
        await CaptureScreen(maidataPath, fps, resizeBg, onStart);
    }

    public void StopRecording()
    {
        IsRecording = false;
    }

    public void ResetState()
    {
        StopRecording();
        errText.text = string.Empty;
    }

    private async UniTask CaptureScreen(string maidataPath,
        int fps, bool resizeBg, [CanBeNull] Action onStart = null)
    {
        if (Screen.width % 2 != 0 || Screen.height % 2 != 0)
        {
            errText.text = $"无法渲染：分辨率 {Screen.width}x{Screen.height} 不是偶数。";
            return;
        }
        
        // 1. args
        const string wavName = "temp.wav";
        const string videoName = "temp.mp4";
        const string finalName = "out.mp4";

        const string videoCodecArgs = "-c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p -movflags +faststart ";
        const string vfArgs = "-vf vflip ";

        var outArgs =
            "-hide_banner -y -report " +
            $"-f rawvideo -pix_fmt rgba -s {Screen.width}x{Screen.height} -r {fps} " +
            "-i - " +
            vfArgs +
            videoCodecArgs +
            $"\"{videoName}\"";

        var muxArgs =
            "-hide_banner -y " +
            $"-i \"{videoName}\" -i \"{wavName}\" " +
            "-c:v copy -c:a aac -b:a 320k -shortest " +
            $"\"{finalName}\"";

        // 2. vars
        var rt = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
        rt.Create();
        var cpuTex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false);

        IsRecording = true;

        var isTouchHoldRising = false;

        var deltaTime = 1.0f / fps;
        var recordingElapsedTime = 0f;
        var videoEncodeSucceeded = false;

        try
        {
            // 3. launch ffmpeg pipe
            var cmd = $"cd \"{maidataPath}\" && ffmpeg {outArgs}";
            var proc = FFmpegPipe.Spawn(cmd);
            if (!proc.IsValid)
            {
                errText.text = "无法启动 FFmpeg";
                return;
            }

            // 4. prepare
            onStart?.Invoke();
            audioManager.PrepareRecordingBuffer(timeProvider.AudioTime, timeProvider.CurrentSpeed);
            
            while (IsRecording)
            {
                // 5. recording
                await UniTask.WaitForEndOfFrame(this);
                ProcessSfx(deltaTime, recordingElapsedTime, ref isTouchHoldRising);
                RenderTexture.active = null;
                cpuTex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                var raw = cpuTex.GetRawTextureData();
                if (FFmpegPipe.Write(proc, raw, raw.Length) < 0)
                    break;
                recordingElapsedTime += deltaTime;
            }

            // 6. clean up
            FFmpegPipe.ClosePipe(proc);
            var exitCode = FFmpegPipe.Wait(proc);
            videoEncodeSucceeded = exitCode == 0;
        }
        finally
        {
            RenderTexture.active = null;
            rt.Release();
            Destroy(rt);
            Destroy(cpuTex);
        }

        if (!videoEncodeSucceeded)
        {
            errText.text = "video encode failed";
            return;
        }

        // 7. wav
        audioManager.ExportFinalWav(Path.Combine(maidataPath, wavName));

        // 8. mux
        var muxCmd = $"cd \"{maidataPath}\" && ffmpeg {muxArgs}";
        var muxProc = FFmpegPipe.SpawnSimple(muxCmd);
        if (!muxProc.IsValid)
        {
            errText.text = "无法启动 Muxing 进程";
            return;
        }
        FFmpegPipe.Wait(muxProc);

        var outPath = Path.Combine(maidataPath, finalName);
        if (File.Exists(outPath))
        {
            OpenFileLocation(outPath);
        }

        timeProvider.Pause();
        bgManager.PauseVideo();
    }

    private void ProcessSfx(float deltaTime, float recordingElapsedTime, ref bool isTouchHoldRising)
    {
        if (audioManager.IsShowingSongDetail)
            return;

        audioManager.UpdateAnswerSfx();
        for (var i = 0; i < AudioManager.noteSfxPlaybackRequests.Length; i++)
        {
            if (i == AudioManager.TRACK_START) continue;

            if (i == AudioManager.TOUCHHOLD)
            {
                var isRequested = AudioManager.noteSfxPlaybackRequests[i];
                if (isRequested)
                {
                    if (!isTouchHoldRising)
                    {
                        isTouchHoldRising = true;
                        audioManager.TriggerSfxRecording(AudioManager.TOUCHHOLD);
                    }
                }
                else
                {
                    if (isTouchHoldRising)
                    {
                        isTouchHoldRising = false;
                        audioManager.StopSfxRecording(AudioManager.TOUCHHOLD);
                    }
                }
            }
            else if (AudioManager.noteSfxPlaybackRequests[i])
            {
                audioManager.TriggerSfxRecording(i);
                AudioManager.noteSfxPlaybackRequests[i] = false;
            }
        }
        audioManager.UpdateSfxRecording(deltaTime, recordingElapsedTime);
    }

    private static void OpenFileLocation(string filePath)
    {
#if UNITY_EDITOR_WIN
        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
#elif UNITY_EDITOR_OSX
        System.Diagnostics.Process.Start("open", $"-R \"{filePath}\"");
#elif UNITY_STANDALONE_WIN
        FFmpegPipe.SpawnSimple($"explorer /select,\"{filePath}\"");
#elif UNITY_STANDALONE_OSX
        FFmpegPipe.SpawnSimple($"open -R \"{filePath}\"");
#elif UNITY_STANDALONE_LINUX
        FFmpegPipe.SpawnSimple($"xdg-open \"{Path.GetDirectoryName(filePath)}\"");
#endif
    }
}