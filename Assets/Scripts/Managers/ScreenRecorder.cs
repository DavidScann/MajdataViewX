#region

using System;
using System.IO;
using System.IO.Pipes;
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
        //成功的情况下不需要调用errText.text，一路走下去根本看不见错误提示
        // 1. check
        if (Screen.width % 2 != 0 || Screen.height % 2 != 0)
        {
            errText.text = $"无法渲染：分辨率 {Screen.width}x{Screen.height} 不是偶数。";
            return;
        }

        // 2. args
        var ffmpegPath = Path.Combine(Application.streamingAssetsPath, "ffmpeg.exe");
        var pipeName = $"majdataRec_{ProcessUtils.CurrentProcessId}_{System.Guid.NewGuid():N}";
        var wavName = "temp.wav";
        var videoName = "temp.mp4";
        var finalName = "out.mp4";

        var videoCodecArgs = "-c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p -movflags +faststart ";

        var vfArgs = "-vf vflip ";

        var outArgs =
            "-hide_banner -y -report " +
            $"-f rawvideo -pix_fmt rgba -s {Screen.width}x{Screen.height} -r {fps} " +
            $@"-i \\.\pipe\{pipeName} " +
            vfArgs +
            videoCodecArgs +
            $"\"{videoName}\"";

        var muxArgs =
            "-hide_banner -y " +
            $"-i \"{videoName}\" -i \"{wavName}\" " +
            "-c:v copy -c:a aac -b:a 320k -shortest " +
            $"\"{finalName}\"";

        // 3. objects
        var rt = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
        rt.Create();
        // 预分配 Texture2D，避免循环内 new 产生 GC
        var cpuTex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false);

        //audioManager.PrepareRecordingBuffer(timeProvider.AudioTime, timeProvider.CurrentSpeed);
        IsRecording = true;

        var isTouchHoldRising = false;

        var deltaTime = 1.0f / fps; // duration per frame
        var recordingElapsedTime = 0f;
        var videoEncodeSucceeded = false;

        // 4. recording
        IntPtr ffmpegProcessHandle = IntPtr.Zero;
        try
        {
            using (var pipeServer = new NamedPipeServerStream(pipeName, PipeDirection.Out, 1,
                       PipeTransmissionMode.Byte, PipeOptions.None, 1024 * 1024 * 16, 1024 * 1024 * 16))
            {
                var (started, handle) = ProcessUtils.Start(ffmpegPath, outArgs, maidataPath);
                if (!started)
                {
                    errText.text = "无法启动 FFmpeg";
                    return;
                }
                ffmpegProcessHandle = handle;

                // 等待 FFmpeg 连接
                pipeServer.WaitForConnection();

                using (var bw = new BinaryWriter(pipeServer))
                {
                    onStart?.Invoke();
                    //这时再传入时间点，onstart启动了timeprovider
                    audioManager.PrepareRecordingBuffer(timeProvider.AudioTime, timeProvider.CurrentSpeed);
                    while (IsRecording && !ProcessUtils.HasExited(ffmpegProcessHandle, out _))
                    {
                        await UniTask.WaitForEndOfFrame(this);

                        // Audio
                        if (!audioManager.IsShowingSongDetail)
                        {
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
                                        // TouchHold 不重置指针，让它继续播
                                    }
                                    else
                                    {
                                        if (isTouchHoldRising)
                                        {
                                            isTouchHoldRising = false;
                                            audioManager.StopSfxRecording(AudioManager.TOUCHHOLD); // 停止播放
                                        }
                                    }
                                }
                                else if (AudioManager.noteSfxPlaybackRequests[i])
                                {
                                    // 重置指针
                                    audioManager.TriggerSfxRecording(i);
                                    AudioManager.noteSfxPlaybackRequests[i] = false;
                                }
                            }
                            audioManager.UpdateSfxRecording(deltaTime, recordingElapsedTime);
                        }

                        // Video
                        RenderTexture.active = null;
                        cpuTex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                        bw.Write(cpuTex.GetRawTextureData());
                        recordingElapsedTime += deltaTime;
                    }

                    bw.Flush();
                }

                // 等待 FFmpeg 完成
                var exitCode = ProcessUtils.WaitForExit(ffmpegProcessHandle);
                videoEncodeSucceeded = exitCode == 0;
                if (!videoEncodeSucceeded)
                    errText.text = $"视频编码失败，FFmpeg 退出码：{exitCode}";
            }
        }
        finally
        {
            ProcessUtils.CloseProcessHandle(ffmpegProcessHandle);
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

        // 5. audio export and mux
        //errText.text = "正在处理音频导出...";
        audioManager.ExportFinalWav(Path.Combine(maidataPath, wavName));

        //errText.text = "正在执行最终合并 (Muxing)...";
        var (muxStarted, muxExitCode) = ProcessUtils.StartAndWait(ffmpegPath, muxArgs, maidataPath);
        if (!muxStarted)
        {
            errText.text = $"无法启动 Muxing 进程，错误码：{muxExitCode}";
            return;
        }

        // 6. clean up
        var outPath = Path.Combine(maidataPath, finalName);
        if (File.Exists(outPath))
        {
            //errText.text = "渲染成功：" + finalName;
            ProcessUtils.ShowInExplorer(outPath);
        }

        timeProvider.Pause();
        bgManager.PauseVideo();
    }
}
