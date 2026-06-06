#region

using System;
using System.Diagnostics;
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

        var ffmpegPath = MajEnv.FFmpegPath;
        var wavName = "temp.wav";
        var videoName = "temp.mp4";
        var finalName = "out.mp4";

        var videoCodecArgs = "-c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p -movflags +faststart ";

        var vfArgs = "-vf vflip ";

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
#if UNITY_EDITOR
            var ffmpegPsi = new ProcessStartInfo(ffmpegPath, outArgs)
            {
                WorkingDirectory = maidataPath,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardInput = true
            };
            using var ffmpegProcess = Process.Start(ffmpegPsi);
            if (ffmpegProcess == null)
            {
                errText.text = "无法启动 FFmpeg";
                return;
            }

            using (var bw = new BinaryWriter(ffmpegProcess.StandardInput.BaseStream))
            {
                onStart?.Invoke();
                audioManager.PrepareRecordingBuffer(timeProvider.AudioTime, timeProvider.CurrentSpeed);
                while (IsRecording && !ffmpegProcess.HasExited)
                {
                    await UniTask.WaitForEndOfFrame(this);
                    ProcessSfx(deltaTime, recordingElapsedTime, ref isTouchHoldRising);
                    RenderTexture.active = null;
                    cpuTex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                    bw.Write(cpuTex.GetRawTextureData());
                    recordingElapsedTime += deltaTime;
                }
                bw.Flush();
            }

            ffmpegProcess.WaitForExit();
            videoEncodeSucceeded = ffmpegProcess.ExitCode == 0;
            if (!videoEncodeSucceeded)
                errText.text = $"视频编码失败，FFmpeg 退出码：{ffmpegProcess.ExitCode}";
#else
            var cmd = $"cd \"{maidataPath}\" && \"{ffmpegPath}\" {outArgs}";
            var proc = FFmpegPipe.Spawn(cmd);
            if (proc.Handle == IntPtr.Zero)
            {
                errText.text = "无法启动 FFmpeg";
                return;
            }

            onStart?.Invoke();
            audioManager.PrepareRecordingBuffer(timeProvider.AudioTime, timeProvider.CurrentSpeed);
            while (IsRecording)
            {
                await UniTask.WaitForEndOfFrame(this);
                ProcessSfx(deltaTime, recordingElapsedTime, ref isTouchHoldRising);
                RenderTexture.active = null;
                cpuTex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                var raw = cpuTex.GetRawTextureData();
                if (FFmpegPipe.Write(proc.StdinFd, raw, raw.Length) < 0)
                    break;
                recordingElapsedTime += deltaTime;
            }

            FFmpegPipe.ClosePipe(proc.StdinFd);
            var exitCode = FFmpegPipe.Wait(proc.Handle);
            videoEncodeSucceeded = exitCode == 0;
            if (!videoEncodeSucceeded)
                errText.text = $"视频编码失败，FFmpeg 退出码：{exitCode}";
#endif
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

        audioManager.ExportFinalWav(Path.Combine(maidataPath, wavName));

#if UNITY_EDITOR
        var muxPsi = new ProcessStartInfo(ffmpegPath, muxArgs)
        {
            WorkingDirectory = maidataPath,
            CreateNoWindow = true,
            UseShellExecute = false
        };
        using var muxProcess = Process.Start(muxPsi);
        if (muxProcess == null)
        {
            errText.text = "无法启动 Muxing 进程";
            return;
        }
        muxProcess.WaitForExit();
#else
        var muxCmd = $"cd \"{maidataPath}\" && \"{ffmpegPath}\" {muxArgs}";
        var muxHandle = FFmpegPipe.SpawnSimple(muxCmd);
        if (muxHandle == IntPtr.Zero)
        {
            errText.text = "无法启动 Muxing 进程";
            return;
        }
        FFmpegPipe.Wait(muxHandle);
#endif

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
#if UNITY_EDITOR
        try
        {
            Process.Start("explorer.exe", $"/select,\"{filePath}\"");
        }
        catch { }
#elif UNITY_STANDALONE_WIN
        FFmpegPipe.SpawnSimple($"explorer /select,\"{filePath}\"");
#elif UNITY_STANDALONE_OSX
        FFmpegPipe.SpawnSimple($"open -R \"{filePath}\"");
#elif UNITY_STANDALONE_LINUX
        FFmpegPipe.SpawnSimple($"xdg-open \"{Path.GetDirectoryName(filePath)}\"");
#endif
    }
}