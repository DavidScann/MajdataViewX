using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using MajdataViewX.Native;
using MajdataViewX.Types.Enums;
using MajdataViewX.Types.Rendering;
using MajdataViewX.Utils;
using System;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UI;
using static MajdataViewX.Base.MajCtx;

namespace MajdataViewX.Managers
{

    public class ScreenRecorder : MonoBehaviour
    {
        private const string EncoderDllName = "RenderingOut";

        [DllImport(EncoderDllName, CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr video_encoder_create(
            int quality,
            int width,
            int height,
            int fps,
            [MarshalAs(UnmanagedType.LPStr)] string filename);

        [DllImport(EncoderDllName, CallingConvention = CallingConvention.StdCall)]
        private static extern int video_encoder_submit_frame(
            IntPtr encoder,
            IntPtr nativeTexture);

        [DllImport(EncoderDllName, CallingConvention = CallingConvention.StdCall)]
        private static extern int video_encoder_mux_audio(
            IntPtr encoder,
            IntPtr pcmData,
            int pcmLengthBytes,
            int sampleRate,
            int channels);

        [DllImport(EncoderDllName, CallingConvention = CallingConvention.StdCall)]
        private static extern void video_encoder_free(IntPtr encoder);

        Text errText;
        Canvas exportOverlayCanvas;
        RawImage exportPreviewImage;
        Text exportOverlayText;
        float exportFpsWindowAccum;
        int exportFpsWindowFrames;
        float exportFps;
        double _exportOverlayLastRealTime;

        public bool IsRecording { get; private set; }

        private void Awake()
        {
            _screenRecorder = this;
        }

        private void Start()
        {
            errText = GameObject.Find("ErrText").GetComponent<Text>();
            CreateExportOverlay();
        }

        private void CreateExportOverlay()
        {
            // Screen Space Overlay renders to the window regardless of the main
            // camera's target texture, so it stays visible while the scene is
            // being rendered into the export render target. It is not part of
            // the recorded video.
            // Reuse an existing overlay if one survived a scene reload.
            var existing = GameObject.Find("ExportOverlay");
            if (existing != null)
            {
                exportOverlayCanvas = existing.GetComponent<Canvas>();
                exportPreviewImage = existing.transform.Find("ExportPreviewImage")?.GetComponent<RawImage>();
                exportOverlayText = existing.transform.Find("ExportStatusText")?.GetComponent<Text>();
                if (exportOverlayCanvas != null && exportOverlayText != null)
                {
                    exportOverlayCanvas.enabled = false;
                    return;
                }
            }

            var overlayGo = new GameObject("ExportOverlay");
            DontDestroyOnLoad(overlayGo);
            exportOverlayCanvas = overlayGo.AddComponent<Canvas>();
            exportOverlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            exportOverlayCanvas.sortingOrder = 999;

            var scaler = overlayGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Full-screen preview of the export render target. While the main
            // camera renders into the RT, the screen backbuffer is never
            // repainted (stale idle frame + overlay text ghosting on top of
            // itself). Drawing the RT here repaints the whole window every frame.
            var imageGo = new GameObject("ExportPreviewImage");
            imageGo.transform.SetParent(overlayGo.transform, false);
            exportPreviewImage = imageGo.AddComponent<RawImage>();
            exportPreviewImage.raycastTarget = false;
            var imageRect = (RectTransform)exportPreviewImage.transform;
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;

            var textGo = new GameObject("ExportStatusText");
            textGo.transform.SetParent(overlayGo.transform, false);
            exportOverlayText = textGo.AddComponent<Text>();
            exportOverlayText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            exportOverlayText.fontSize = 48;
            exportOverlayText.fontStyle = FontStyle.Bold;
            exportOverlayText.alignment = TextAnchor.MiddleCenter;
            exportOverlayText.color = Color.white;
            exportOverlayText.horizontalOverflow = HorizontalWrapMode.Overflow;
            exportOverlayText.verticalOverflow = VerticalWrapMode.Overflow;

            var shadow = textGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(2, -2);

            var rect = (RectTransform)exportOverlayText.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            exportOverlayCanvas.enabled = false;
        }

        private void UpdateExportOverlay(double recordingElapsedTime, double totalTime)
        {
            if (exportOverlayCanvas == null) return;

            // Time.unscaledDeltaTime is pinned by Time.captureDeltaTime during
            // export, so measure real time for the actual render/export speed.
            var now = Time.realtimeSinceStartupAsDouble;
            var delta = (float)(now - _exportOverlayLastRealTime);
            _exportOverlayLastRealTime = now;

            exportFpsWindowAccum += delta;
            exportFpsWindowFrames++;
            if (exportFpsWindowAccum >= 0.5f)
            {
                exportFps = exportFpsWindowFrames / exportFpsWindowAccum;
                exportFpsWindowAccum = 0f;
                exportFpsWindowFrames = 0;
            }

            var percent = totalTime > 0
                ? Math.Clamp(recordingElapsedTime / totalTime, 0, 1) * 100
                : 0;

            exportOverlayText.text =
                $"Exporting...\n{percent:F2}%\n{exportFps:F0} FPS";
        }

        public async UniTask StartRecording(string maidataPath,
            int fps, ExportQuality quality, int exportWidth, int exportHeight, ExportCodec codec,
            [CanBeNull] Action onStart = null)
        {
            QualitySettings.vSyncCount = 0;
            try
            {
                await CaptureScreen(maidataPath, fps, quality, exportWidth, exportHeight, codec, onStart);
            }
            finally
            {
                QualitySettings.vSyncCount = 1;
            }
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
            int fps, ExportQuality quality, int exportWidth, int exportHeight, ExportCodec codec,
            [CanBeNull] Action onStart = null)
        {
            if (fps <= 0)
            {
                errText.text = "Encoding cannot start: Output frame rate must be greater than zero.";
                return;
            }

            var width = exportWidth > 0 ? exportWidth : Screen.width;
            var height = exportHeight > 0 ? exportHeight : Screen.height;

            if (width % 2 != 0 || height % 2 != 0)
            {
                errText.text = $"Encoding cannot start: Resolution width and height must be even numbers. Current: {width}x{height}.";
                return;
            }

            const string finalName = "out.mp4";

            IsRecording = true;
            var frameDuration = 1.0 / fps;
            var recordingElapsedTime = 0.0;
            var outputSucceeded = false;
            var captureTexture = new RenderTexture(
                width,
                height,
                24,
                RenderTextureFormat.BGRA32)
            {
                name = "Screen Recorder Capture",
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false
            };
            var encoder = IntPtr.Zero;

#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            const string tempVideoName = "temp.mp4";
            const string tempWavName = "temp.wav";
            var tempVideoPath = Path.Combine(maidataPath, tempVideoName);
            var tempWavPath = Path.Combine(maidataPath, tempWavName);
            var pipeProc = default(FFmpegPipe.PipeProcess);
            Texture2D cpuTex = null;
#endif
            var mainCamera = Camera.main;
            var prevTargetTexture = mainCamera != null ? mainCamera.targetTexture : null;

            try
            {
                if (!captureTexture.Create())
                    throw new InvalidOperationException(
                        "Could not create the screen recorder render target.");

                // Render the scene natively into the capture target at the export
                // resolution (e.g. 3840x2160) instead of capturing the window at
                // its current size. The main camera is restored in the finally block.
                if (mainCamera != null)
                    mainCamera.targetTexture = captureTexture;

                var outPath = Path.Combine(maidataPath, finalName);
                if (File.Exists(outPath)) File.Delete(outPath);

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                encoder = video_encoder_create(
                    (int)quality,
                    width,
                    height,
                    fps,
                    outPath);
                if (encoder == IntPtr.Zero)
                    throw new InvalidOperationException(
                        "RenderingOut could not create the video encoder.");
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
                if (File.Exists(tempVideoPath)) File.Delete(tempVideoPath);
                if (File.Exists(tempWavPath)) File.Delete(tempWavPath);

                var videoArgs = FfmpegEncoder.BuildVideoArgs(width, height, fps, quality, codec);
                var videoCmd = $"cd \"{maidataPath}\" && {FfmpegEncoder.Binary} " +
                    $"-hide_banner -y -f rawvideo -pix_fmt rgba -s {width}x{height} -r {fps} -i - " +
                    $"{videoArgs} -movflags +faststart \"{tempVideoName}\"";
                pipeProc = FFmpegPipe.Spawn(videoCmd);
                if (!pipeProc.IsValid)
                    throw new InvalidOperationException(
                        "FFmpeg could not be started. Make sure ffmpeg is installed and in PATH.");
                cpuTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
#else
                throw new PlatformNotSupportedException(
                    "Video export is only supported on Windows and Linux.");
#endif

                onStart?.Invoke();
                _audioManager.BeginRecordingAudio(_timeProvider.AudioTime, _timeProvider.CurrentSpeed);

                // The record covers the song from the starting position to the
                // track end. The overlay percentage counts up to 100% for this
                // segment, not the whole track (a "from here" export otherwise
                // stalls at ~16% even though it finishes the song).
                var recordStartTime = Math.Max(0, (double)_timeProvider.AudioTime);
                var totalTime = Math.Max(0.01,
                    _audioManager.TrackLength / Math.Max(_timeProvider.CurrentSpeed, 0.01f) -
                    recordStartTime);
                exportFpsWindowAccum = 0f;
                exportFpsWindowFrames = 0;
                exportFps = fps;
                _exportOverlayLastRealTime = Time.realtimeSinceStartupAsDouble;
                if (exportOverlayCanvas != null)
                {
                    if (exportPreviewImage != null)
                        exportPreviewImage.texture = captureTexture;
                    exportOverlayCanvas.enabled = true;
                }

                while (IsRecording && recordingElapsedTime < totalTime)
                {
                    await UniTask.WaitForEndOfFrame(this);
                    var frameEndTime = recordingElapsedTime + frameDuration;
                    _audioManager.UpdateRecordingAudioFrame(recordingElapsedTime, frameEndTime);

                    UpdateExportOverlay(recordingElapsedTime, totalTime);

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                    // The main camera renders directly into captureTexture at the
                    // export resolution; hand its native texture to the encoder.
                    var nativeTexture = captureTexture.GetNativeTexturePtr();
                    if (nativeTexture == IntPtr.Zero)
                        throw new InvalidOperationException(
                            "The screen recorder render target has no native texture.");

                    var submitResult = video_encoder_submit_frame(encoder, nativeTexture);
                    if (submitResult < 0)
                        throw new InvalidOperationException(
                            $"RenderingOut failed to encode a video frame ({submitResult}).");
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
                    // ReadPixels from the camera's render target (bottom-up,
                    // hence the vflip in the ffmpeg args). The generic
                    // GetRawTextureData<byte> returns a zero-copy NativeArray
                    // view; the non-generic overload would allocate an 8MB
                    // copy per frame and stall the export in a GC spiral.
                    RenderTexture.active = captureTexture;
                    cpuTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    RenderTexture.active = null;
                    var raw = cpuTex.GetRawTextureData<byte>();
                    if (FFmpegPipe.Write(pipeProc, raw) < 0)
                        throw new InvalidOperationException(
                            "FFmpeg pipe write failed.");
#else
                    throw new PlatformNotSupportedException(
                        "Video export is only supported on Windows and Linux.");
#endif

                    recordingElapsedTime = frameEndTime;
                }

                _audioManager.EndRecordingAudio((float)recordingElapsedTime);

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                MuxRecordingAudio(encoder);
                FreeEncoder(ref encoder);
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
                var tEnc = Time.realtimeSinceStartup;
                FFmpegPipe.ClosePipe(pipeProc);
                var exitCode = FFmpegPipe.Wait(pipeProc);
                Debug.Log($"[ScreenRecorder] encoder done in {(Time.realtimeSinceStartup - tEnc) * 1000:F0}ms (exit {exitCode})");
                if (exitCode != 0)
                    throw new InvalidOperationException(
                        $"FFmpeg video encode failed (exit code {exitCode}).");

                var tWav = Time.realtimeSinceStartup;
                var pcmData = _audioManager.GetRecordingBuffer(out var sampleCount);
                if (sampleCount > 0)
                {
                    WavFileWriter.WriteFile(
                        tempWavPath,
                        AudioManager.SAMPLERATE,
                        AudioManager.CHANNELS,
                        pcmData.ToArray());
                }
                Debug.Log($"[ScreenRecorder] wav written in {(Time.realtimeSinceStartup - tWav) * 1000:F0}ms");

                var tMux = Time.realtimeSinceStartup;
                var muxCmd = $"cd \"{maidataPath}\" && {FfmpegEncoder.Binary} " +
                    FfmpegEncoder.BuildMuxArgs(tempVideoName, tempWavName, finalName);
                var muxProc = FFmpegPipe.SpawnSimple(muxCmd);
                if (!muxProc.IsValid)
                    throw new InvalidOperationException(
                        "FFmpeg could not be started for audio muxing.");
                var muxExit = FFmpegPipe.Wait(muxProc);
                Debug.Log($"[ScreenRecorder] mux done in {(Time.realtimeSinceStartup - tMux) * 1000:F0}ms (exit {muxExit})");
                if (muxExit != 0)
                    throw new InvalidOperationException(
                        $"FFmpeg audio mux failed (exit code {muxExit}).");

                if (File.Exists(tempVideoPath)) File.Delete(tempVideoPath);
                if (File.Exists(tempWavPath)) File.Delete(tempWavPath);
#else
                throw new PlatformNotSupportedException(
                    "Video export is only supported on Windows and Linux.");
#endif
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
                    FreeEncoder(ref encoder);
                }
                catch (Exception ex)
                {
                    outputSucceeded = false;
                    Debug.LogException(ex);
                    errText.text = $"Finalizing the recording failed: {ex.Message}";
                }

#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
                if (pipeProc.IsValid)
                {
                    try
                    {
                        FFmpegPipe.Kill(pipeProc);
                        FFmpegPipe.ClosePipe(pipeProc);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
                if (cpuTex != null)
                    Destroy(cpuTex);
#endif

                // Restore the main camera to its original render target.
                if (mainCamera != null)
                    mainCamera.targetTexture = prevTargetTexture;

                if (exportOverlayCanvas != null)
                {
                    if (exportPreviewImage != null)
                        exportPreviewImage.texture = null;
                    exportOverlayCanvas.enabled = false;
                }

                _audioManager.ReleaseRecordingAudio();
                var resultPath = Path.Combine(maidataPath, finalName);
                if (outputSucceeded && File.Exists(resultPath))
                    OpenFileLocation(resultPath);

                RenderTexture.active = null;
                captureTexture.Release();
                Destroy(captureTexture);
            }
        }

        private static unsafe void MuxRecordingAudio(IntPtr encoder)
        {
            var pcmData = _audioManager.GetRecordingBuffer(out var sampleCount);
            if (sampleCount == 0)
                return;

            var pcmDataPointer = (IntPtr)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(pcmData);
            var muxResult = video_encoder_mux_audio(
                encoder,
                pcmDataPointer,
                checked(sampleCount * sizeof(float)),
                AudioManager.SAMPLERATE,
                AudioManager.CHANNELS);
            if (muxResult < 0)
                throw new InvalidOperationException(
                    $"RenderingOut failed to mux the recorded audio ({muxResult}).");
        }

        private static void FreeEncoder(ref IntPtr encoder)
        {
            if (encoder == IntPtr.Zero)
                return;

            var encoderToFree = encoder;
            encoder = IntPtr.Zero;
            video_encoder_free(encoderToFree);
        }



#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr ShellExecuteW(
            IntPtr window,
            string operation,
            string file,
            string parameters,
            string directory,
            int showCommand);
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
    [DllImport("libSystem.dylib", EntryPoint = "system")]
    private static extern int RunSystemCommand(string command);
#elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
    [DllImport("libc", EntryPoint = "system")]
    private static extern int RunSystemCommand(string command);
#endif
        private static void OpenFileLocation(string filePath)
        {
            var fullPath = Path.GetFullPath(filePath);

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            var result = ShellExecuteW(
                IntPtr.Zero,
                "open",
                "explorer.exe",
                $"/select,\"{fullPath}\"",
                string.Empty,
                1);
            if (result.ToInt64() > 32) return;
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        if (RunSystemCommand($"open -R {QuoteShellArgument(fullPath)}") == 0) return;
#elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
        var linuxDirectory = Path.GetDirectoryName(fullPath);
        if (linuxDirectory is not null &&
            RunSystemCommand($"xdg-open {QuoteShellArgument(linuxDirectory)} >/dev/null 2>&1 &") == 0)
            return;
#endif

            var directoryPath = Path.GetDirectoryName(fullPath);
            if (directoryPath is not null)
                Application.OpenURL(new Uri(directoryPath + Path.DirectorySeparatorChar).AbsoluteUri);
        }

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
    private static string QuoteShellArgument(string value) =>
        $"'{value.Replace("'", "'\"'\"'")}'";
#endif
    }
}