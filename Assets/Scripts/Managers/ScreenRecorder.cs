#region

using System.Collections;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using Cysharp.Threading.Tasks;
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

    public async UniTask StartRecording(string maidataPath, int fps, bool useAlpha)
    {
        if (IsRecording) StopRecording();
        await CaptureScreen(maidataPath, fps, useAlpha);
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

    private async UniTask CaptureScreen(string maidataPath, int fps, bool useAlpha)
    {
        // 1. check
        if (Screen.width % 2 != 0 || Screen.height % 2 != 0)
        {
            errText.text = $"无法渲染：分辨率 {Screen.width}x{Screen.height} 不是偶数。";
            return;
        }

        // 2. args
        var ffmpegPath = Path.Combine(Application.streamingAssetsPath, "ffmpeg.exe");
        var pipeName = $"majdataRec_{Process.GetCurrentProcess().Id}_{System.Guid.NewGuid():N}";
        var wavName = "temp.wav";
        var videoName = useAlpha ? "temp.webm" : "temp.mp4";
        var finalName = useAlpha ? "out.webm" : "out.mp4";

        // 透明：VP9 (deadline realtime 提高速度, yuva420p 保留 Alpha)
        // 不透明：x264 (ultrafast 提高速度, yuv420p 体积最小)
        var videoCodecArgs = useAlpha
            ? "-c:v libvpx-vp9 -deadline realtime -cpu-used 8 -crf 22 -b:v 0 -pix_fmt yuva420p "
            : "-c:v libx264 -preset ultrafast -crf 20 -pix_fmt yuv420p ";
        var outArgs =
            "-hide_banner -y " +
            $"-f rawvideo -pix_fmt rgba -s {Screen.width}x{Screen.height} -r {fps} " +
            $@"-i \\.\pipe\{pipeName} " +
            "-vf vflip " +
            videoCodecArgs +
            $"\"{videoName}\"";

        //WebM => libopus，MP4 => aac
        var audioCodec = useAlpha ? "libopus" : "aac";
        var muxArgs =
            "-hide_banner -y " +
            $"-i \"{videoName}\" -i \"{wavName}\" " +
            $"-c:v copy -c:a {audioCodec} -b:a 320k -shortest " +
            $"\"{finalName}\"";

        // 3. objects
        var rt = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
        rt.Create();
        // 预分配 Texture2D，避免循环内 new 产生 GC
        var cpuTex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false);

        var camera = Camera.main;
        var oldRT = camera != null ? camera.targetTexture : null;
        if (useAlpha && camera != null)
            camera.targetTexture = rt;

        audioManager.PrepareRecordingBuffer(timeProvider.AudioTime, timeProvider.CurrentSpeed);
        IsRecording = true;

        var isTouchHoldRising = false;

        var deltaTime = 1.0f / fps; // duration per frame
        var recordingElapsedTime = 0f;
        var videoEncodeSucceeded = false;

        // 4. recording
        try
        {
            using (var pipeServer = new NamedPipeServerStream(pipeName, PipeDirection.Out, 1,
                       PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 1024 * 1024 * 16, 1024 * 1024 * 16))
            {
                var startInfo = new ProcessStartInfo(ffmpegPath, outArgs)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = maidataPath
                };
                var outProcess = Process.Start(startInfo);

                // 等待 FFmpeg 连接
                var connectTask = pipeServer.WaitForConnectionAsync();
                while (!connectTask.IsCompleted) await UniTask.Yield();

                using (var bw = new BinaryWriter(pipeServer))
                {
                    while (IsRecording && !outProcess.HasExited)
                    {
                        await UniTask.WaitForEndOfFrame(this);

                        // Audio
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

                        // Video
                        if (useAlpha)
                        {
                            RenderTexture.active = rt;
                        }
                        else
                        {
                            RenderTexture.active = null;
                        }
                        cpuTex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                        bw.Write(cpuTex.GetRawTextureData());
                        recordingElapsedTime += deltaTime;
                    }

                    bw.Flush();
                }

                if (!outProcess.HasExited) outProcess.WaitForExit();
                videoEncodeSucceeded = outProcess.ExitCode == 0;
                if (!videoEncodeSucceeded)
                    errText.text = $"视频编码失败，FFmpeg 退出码：{outProcess.ExitCode}";
            }
        }
        finally
        {
            camera.targetTexture = oldRT;
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
        errText.text = "正在处理音频导出...";
        audioManager.ExportFinalWav(Path.Combine(maidataPath, wavName));

        errText.text = "正在执行最终合并 (Muxing)...";
        var muxProcess = Process.Start(new ProcessStartInfo(ffmpegPath, muxArgs)
        {
            UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = maidataPath
        });
        muxProcess.WaitForExit();

        // 6. clean up
        var outPath = Path.Combine(maidataPath, finalName);
        if (File.Exists(outPath))
        {
            errText.text = "渲染成功：" + finalName;
            Process.Start("explorer", "/select,\"" + outPath + "\"");
        }

        timeProvider.Pause();
        bgManager.PauseVideo();
    }
}
