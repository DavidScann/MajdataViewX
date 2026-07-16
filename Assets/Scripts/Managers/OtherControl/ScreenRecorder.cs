#region

using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

using static MajCtx;

#endregion

public class ScreenRecorder : MonoBehaviour
{

    private const int MaxPendingReadbacks = 4;

    Text errText;

    public bool IsRecording { get; private set; }

    private void Awake()
    {
        _screenRecorder = this;
    }

    private void Start()
    {
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
        if (fps <= 0)
        {
            errText.text = "Output frame rate must be greater than zero.";
            return;
        }

        if (Screen.width % 2 != 0 || Screen.height % 2 != 0)
        {
            errText.text = $"无法渲染：分辨率 {Screen.width}x{Screen.height} 不是偶数。";
            return;
        }

        const string finalName = "out.mp4";

        var rt = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
        rt.Create();

        IsRecording = true;
        var frameDuration = 1.0 / fps;
        var recordingElapsedTime = 0.0;
        var outputSucceeded = false;
        var pendingReadbacks = new Queue<AsyncGPUReadbackRequest>(MaxPendingReadbacks);

        try
        {
            var outPath = Path.Combine(maidataPath, finalName);
            if (File.Exists(outPath)) File.Delete(outPath);

            FFmpegMediaEncoder.Initialize(outPath, Screen.width, Screen.height, fps,
                _audioManager.SampleRate, _audioManager.Channels);

            onStart?.Invoke();
            _audioManager.BeginRecordingAudio(_timeProvider.AudioTime, _timeProvider.CurrentSpeed);

            while (IsRecording)
            {
                await UniTask.WaitForEndOfFrame(this);
                var frameEndTime = recordingElapsedTime + frameDuration;
                _audioManager.UpdateRecordingAudioFrame(recordingElapsedTime, frameEndTime);

                ScreenCapture.CaptureScreenshotIntoRenderTexture(rt);
                pendingReadbacks.Enqueue(
                    AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32));
                DrainReadyVideoFrames(pendingReadbacks);
                if (pendingReadbacks.Count >= MaxPendingReadbacks)
                    EncodeOldestVideoFrame(pendingReadbacks, true);

                recordingElapsedTime = frameEndTime;
            }

            _audioManager.EndRecordingAudio((float)recordingElapsedTime);

            while (pendingReadbacks.Count > 0)
                EncodeOldestVideoFrame(pendingReadbacks, true);

            FFmpegMediaEncoder.Dispose();
            outputSucceeded = true;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            errText.text = $"Encoding failed: {e.Message}";
        }
        finally
        {
            IsRecording = false;
            try
            {
                if (FFmpegMediaEncoder.IsInitialized)
                    FFmpegMediaEncoder.Dispose();
            }
            catch (Exception ex)
            {
                outputSucceeded = false;
                Debug.LogException(ex);
                errText.text = $"Finalizing the recording failed: {ex.Message}";
            }

            // Do not release the render target while the GPU still owns it.
            while (pendingReadbacks.Count > 0)
            {
                var request = pendingReadbacks.Dequeue();
                if (!request.done) request.WaitForCompletion();
            }

            _audioManager.ReleaseRecordingAudio();
            var resultPath = Path.Combine(maidataPath, finalName);
            if (outputSucceeded && File.Exists(resultPath))
                OpenFileLocation(resultPath);

            RenderTexture.active = null;
            rt.Release();
            Destroy(rt);
        }
    }

    private static void DrainReadyVideoFrames(Queue<AsyncGPUReadbackRequest> requests)
    {
        while (EncodeOldestVideoFrame(requests, false))
        {
        }
    }

    private static bool EncodeOldestVideoFrame(
        Queue<AsyncGPUReadbackRequest> requests,
        bool waitForCompletion)
    {
        if (requests.Count == 0)
            return false;

        var request = requests.Peek();
        if (!request.done)
        {
            if (!waitForCompletion)
                return false;
            request.WaitForCompletion();
        }

        requests.Dequeue();
        if (request.hasError)
            throw new InvalidOperationException("Async GPU readback failed while recording a video frame.");

        FFmpegMediaEncoder.WriteVideoFrame(request.GetData<byte>());
        return true;
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