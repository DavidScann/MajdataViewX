#region

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
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

    public void StartRecording(string maidataPath, int fps, bool useAlpha)
    {
        StartCoroutine(CaptureScreen(maidataPath, fps, useAlpha));
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
    
    private IEnumerator CaptureScreen(string maidataPath, int fps, bool useAlpha)
    {
        // 1. 环境检查
        if (Screen.width % 2 != 0 || Screen.height % 2 != 0)
        {
            errText.text = $"无法渲染：分辨率 {Screen.width}x{Screen.height} 不是偶数。";
            yield break;
        }

        // 2. 路径与参数准备
        var ffmpegPath = Path.Combine(Application.streamingAssetsPath, "ffmpeg.exe");
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
            @"-i \\.\pipe\majdataRec " +
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

        // 3. 准备 RenderTexture 和 离线 Texture2D
        var rt = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
        rt.Create();
        // 预分配 Texture2D，避免循环内 new 产生 GC
        var cpuTex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false);
    
        var camera = Camera.main;
        var oldRT = camera.targetTexture;
        camera.targetTexture = rt;

        audioManager.PrepareRecordingBuffer();
        IsRecording = true;

        var touchHoldStartTime = 0f;
        var isTouchHoldRising = false;

        // 4. 启动管道（增加缓冲区大小到 16MB 提高吞吐量）
        using (var pipeServer = new NamedPipeServerStream("majdataRec", PipeDirection.Out, 1, 
                   PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 1024 * 1024 * 16, 1024 * 1024 * 16))
        {
            var startInfo = new ProcessStartInfo(ffmpegPath, outArgs)
            {
                UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = maidataPath
            };
            var outProcess = Process.Start(startInfo);
            
            // 等待 FFmpeg 连接
            var connectTask = pipeServer.WaitForConnectionAsync();
            while (!connectTask.IsCompleted) yield return null;

            using (var bw = new BinaryWriter(pipeServer))
            {
                // --- 录制主循环 ---
                while (IsRecording && !outProcess.HasExited)
                {
                    yield return new WaitForEndOfFrame();

                    // A. 处理音频逻辑 (完全保留你原始逻辑)
                    audioManager.UpdateAnswerSfx();
                    for (var i = 0; i < AudioManager.noteSfxPlaybackRequests.Length - 1; i++)
                    {
                        if (i == AudioManager.TRACK_START) continue;
                        var currentNoteTime = Majdata<TimeProvider>.Instance!.NoteTime;
                        if (i == AudioManager.TOUCHHOLD)
                        {
                            var isRequested = AudioManager.noteSfxPlaybackRequests[i];
                            if (isRequested && !isTouchHoldRising) { isTouchHoldRising = true; touchHoldStartTime = currentNoteTime; }
                            else if (!isRequested && isTouchHoldRising) 
                            { 
                                isTouchHoldRising = false; 
                                audioManager.MixSfxToBuffer(AudioManager.TOUCHHOLD, touchHoldStartTime, currentNoteTime - touchHoldStartTime); 
                            }
                        }
                        else if (AudioManager.noteSfxPlaybackRequests[i])
                        {
                            audioManager.MixSfxToBuffer(i);
                            AudioManager.noteSfxPlaybackRequests[i] = false;
                        }
                    }

                    // B. 视频抓取与同步写入
                    RenderTexture.active = rt;
                    cpuTex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                    bw.Write(cpuTex.GetRawTextureData());
                }
                bw.Flush();
            }

            if (!outProcess.HasExited) outProcess.WaitForExit();
        }

        // 5. 音频导出与最终合并
        errText.text = "正在处理音频导出...";
        audioManager.ExportFinalWav(Path.Combine(maidataPath, wavName));

        errText.text = "正在执行最终合并 (Muxing)...";
        var muxProcess = Process.Start(new ProcessStartInfo(ffmpegPath, muxArgs)
        {
            UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = maidataPath
        });
        muxProcess.WaitForExit();

        // 6. 清理
        camera.targetTexture = oldRT;
        rt.Release();

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