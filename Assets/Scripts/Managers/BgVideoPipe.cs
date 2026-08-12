#nullable enable

using MajdataViewX.Base;
using MajdataViewX.Native;
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using UnityEngine;

namespace MajdataViewX.Managers
{
    /// <summary>
    /// Background video player for platforms where Unity's VideoPlayer cannot
    /// decode the file (Linux has no H.264 decoder). Spawns ffmpeg, reads raw
    /// RGBA frames from its stdout on a worker thread, and uploads them to a
    /// Texture2D for the BgManager's SpriteRenderer to display.
    /// </summary>
    public class BgVideoPipe : IDisposable
    {
        const int MaxDimension = 1920; // cap to bound memory/bandwidth
        const float BaseWorldSize = 10.8f; // Bg SpriteRenderer base world size

        // Verified once per session: -hwaccels probing can list decoders that
        // still fail at runtime (driver/session issues), so the first video
        // does a 3-frame test decode and falls back to CPU if it errors.
        static bool _hwDecodeChecked;

        FFmpegPipe.PipeProcess _proc;
        Thread? _readerThread;
        volatile bool _running;
        volatile bool _paused;

        // Triple-buffered frame storage: the reader writes into free buffers
        // and publishes them; the main thread uploads the newest published one
        // without copying. No per-frame allocations (a clone of an 8MB frame
        // every frame was churning the LOH and causing GC hitches).
        readonly byte[][] _buffers;
        int _writeIndex;
        volatile int _published = -1;
        volatile int _consumed = -1;

        readonly double _fps;
        double _startSec;

        Texture2D _texture;
        Sprite _sprite;
        int _dbgFrameUploads;

        public int Width { get; }
        public int Height { get; }
        public Texture2D Texture => _texture;
        public Sprite Sprite => _sprite;

        public bool IsRunning => _running && _proc.IsValid;

        BgVideoPipe(int width, int height, double fps)
        {
            Width = width;
            Height = height;
            _fps = fps > 1 ? fps : 30;
            _startSec = 0;
            var frameBytes = width * height * 4;
            _buffers = new[]
            {
                new byte[frameBytes],
                new byte[frameBytes],
                new byte[frameBytes],
            };

            _texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            // The Bg SpriteRenderer's base size is 10.8 world units (camera
            // ortho size 5.4 * 2, matching Default_Background.png at 100 ppu).
            // Create the sprite at that world size so BgManager's existing
            // localScale + circle-material framing matches the static bg.
            _sprite = Sprite.Create(
                _texture,
                new Rect(0, 0, Width, Height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: Width / BaseWorldSize);
        }

        public static BgVideoPipe? Start(string videoPath, double startSec)
        {
            var (width, height, fps) = ProbeVideoInfo(videoPath);
            var player = Create(width, height, fps);
            if (!player.TryStart(videoPath, startSec))
            {
                player.Dispose();
                return null;
            }
            return player;
        }

        /// <summary>Creates the texture and sprite. Call on the main thread.</summary>
        public static BgVideoPipe Create(int width, int height, double fps)
        {
            var player = new BgVideoPipe(width, height, fps);
            return player;
        }

        /// <summary>
        /// Probes the video's native dimensions and frame rate with ffprobe and
        /// computes a scaled size that preserves the aspect ratio (max dimension
        /// 1920, even dimensions for ffmpeg). Spawns a process, so call it off
        /// the main thread (Task.Run) to keep the game loop responsive.
        /// </summary>
        public static (int Width, int Height, double Fps) ProbeVideoInfo(string videoPath)
        {
            // One-time per-session check that the detected hw decoder works.
            if (!_hwDecodeChecked)
            {
                _hwDecodeChecked = true;
                if (FfmpegEncoder.HwDecodePrefix.Length > 0 && !VerifyHwDecode(videoPath))
                {
                    Debug.Log("[BgVideoPipe] hardware decode unavailable, falling back to CPU");
                    FfmpegEncoder.DisableHwDecode();
                }
                else if (FfmpegEncoder.HwDecodePrefix.Length > 0)
                {
                    Debug.Log($"[BgVideoPipe] using hw decode: {FfmpegEncoder.HwDecodePrefix.Trim()}");
                }
            }

            int nativeW = 1280, nativeH = 720;
            double fps = 30;
            try
            {
                var quoted = QuoteShellArgument(videoPath);
                var cmd = $"ffprobe -v error -select_streams v:0 " +
                    $"-show_entries stream=width,height,r_frame_rate -of csv=s=x:p=0 {quoted}";
                var proc = FFmpegPipe.SpawnIo(cmd);
                if (proc.IsValid)
                {
                    var buf = new byte[128];
                    int got = FFmpegPipe.ReadFrame(proc, buf, buf.Length);
                    FFmpegPipe.Kill(proc);
                    FFmpegPipe.Wait(proc);
                    FFmpegPipe.ClosePipe(proc);
                    if (got > 0)
                    {
                        var text = System.Text.Encoding.UTF8.GetString(buf, 0, got).Trim();
                        var parts = text.Split('x');
                        if (parts.Length >= 2 &&
                            int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out nativeW) &&
                            int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out nativeH) &&
                            nativeW > 0 && nativeH > 0)
                        {
                            if (parts.Length >= 3 && TryParseFrameRate(parts[2], out var parsedFps))
                                fps = parsedFps;
                            Debug.Log($"[BgVideoPipe] probed {nativeW}x{nativeH} @ {fps:0.###}fps");
                        }
                        else
                        {
                            nativeW = 1280; nativeH = 720;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BgVideoPipe] probe failed, using 1280x720@30: {e.Message}");
                nativeW = 1280; nativeH = 720;
            }

            // Scale to fit MaxDimension while keeping aspect; force even numbers.
            float scale = Mathf.Min(1f, MaxDimension / (float)Math.Max(nativeW, nativeH));
            int w = Mathf.Clamp((int)(nativeW * scale) & ~1, 2, MaxDimension);
            int h = Mathf.Clamp((int)(nativeH * scale) & ~1, 2, MaxDimension);
            return (w, h, fps);
        }

        /// <summary>Parses an ffprobe frame rate like "24000/1001" or "29.97".</summary>
        static bool TryParseFrameRate(string value, out double fps)
        {
            fps = 0;
            try
            {
                var slash = value.IndexOf('/');
                if (slash > 0)
                {
                    var num = double.Parse(value.Substring(0, slash),
                        NumberStyles.Float, CultureInfo.InvariantCulture);
                    var den = double.Parse(value.Substring(slash + 1),
                        NumberStyles.Float, CultureInfo.InvariantCulture);
                    if (den > 0) fps = num / den;
                }
                else
                {
                    fps = double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
                }
            }
            catch
            {
                fps = 0;
            }
            return fps is > 1 and < 240;
        }

        /// <summary>
        /// Verifies that the detected hardware decoder actually decodes this
        /// file (3 frames to null). Runs on the background preload thread.
        /// </summary>
        static bool VerifyHwDecode(string videoPath)
        {
            try
            {
                var quoted = QuoteShellArgument(videoPath);
                var errFile = Path.Combine(Application.temporaryCachePath, "ffmpeg_hwdecode_test.txt");
                if (File.Exists(errFile)) File.Delete(errFile);
                var cmd = $"{FfmpegEncoder.Binary} -hide_banner -loglevel error " +
                    $"{FfmpegEncoder.HwDecodePrefix}-i {quoted} -frames:v 3 -f null - " +
                    $"> \"{errFile}\" 2>&1";
                var proc = FFmpegPipe.SpawnSimple(cmd);
                if (!proc.IsValid) return false;
                var code = FFmpegPipe.Wait(proc);
                if (code != 0 && File.Exists(errFile))
                {
                    var err = File.ReadAllText(errFile).Trim();
                    if (err.Length > 0)
                        Debug.LogWarning($"[BgVideoPipe] hw decode test failed: {err}");
                }
                return code == 0;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BgVideoPipe] hw decode test error: {e.Message}");
                return false;
            }
        }

        /// <summary>Spawns ffmpeg and starts the reader. Call on the main thread.</summary>
        public bool TryStart(string videoPath, double startSec)
        {
            try
            {
                var quoted = QuoteShellArgument(videoPath);
                var seek = startSec > 0
                    ? $"-ss {startSec.ToString("0.###", CultureInfo.InvariantCulture)} "
                    : "";
                // No -re: real-time pacing is done on our side by reading frames
                // at the song clock (AudioTime). ffmpeg's wall-clock -re would
                // fast-forward through the gap after a pause (audio stayed put
                // while its wall clock kept running), desyncing the video.
                var cmd =
                    $"{FfmpegEncoder.Binary} -hide_banner -loglevel error {seek}{FfmpegEncoder.HwDecodePrefix}-i {quoted} " +
                    $"-vf scale={Width}:{Height},vflip " +
                    $"-f rawvideo -pix_fmt rgba -vcodec rawvideo -";
                _proc = FFmpegPipe.SpawnIo(cmd);
                if (!_proc.IsValid) return false;

                _running = true;
                _startSec = Math.Max(0, startSec);
                _readerThread = new Thread(ReaderLoop) { IsBackground = true };
                _readerThread.Start();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[BgVideoPipe] failed to start: {e}");
                return false;
            }
        }

        public void Pause() => _paused = true;

        public void Resume() => _paused = false;

        void ReaderLoop()
        {
            var consecutiveEof = 0;
            var frameIndex = 0;
            var halfFrame = 0.5 / _fps;
            var lastLog = 0;
            while (_running)
            {
                // Paused: stop reading so ffmpeg's pipe fills and it blocks —
                // a natural pause that keeps the last frame on screen.
                if (_paused)
                {
                    Thread.Sleep(10);
                    continue;
                }

                // Pace by the song clock: read frame N only when the song time
                // has reached the frame's timestamp. AudioTime freezes while
                // paused, so this also covers pause without the _paused flag.
                var due = _startSec + frameIndex / _fps;
                var audioTime = (double)MajCtx._timeProvider.AudioTime;
                if (audioTime < due - halfFrame)
                {
                    Thread.Sleep(5);
                    continue;
                }

                // Never write into a buffer the main thread is consuming or
                // hasn't consumed yet (with 3 buffers this only spins briefly
                // if decoding outpaces the display).
                if (_writeIndex == _consumed || _writeIndex == _published)
                {
                    Thread.Sleep(2);
                    continue;
                }
                var idx = _writeIndex;
                int got = FFmpegPipe.ReadFrame(_proc, _buffers[idx], _buffers[idx].Length);
                if (got != _buffers[idx].Length)
                {
                    consecutiveEof++;
                    if (consecutiveEof >= 5) break;
                    Thread.Sleep(10);
                    continue;
                }
                consecutiveEof = 0;
                frameIndex++;
                _writeIndex = (idx + 1) % _buffers.Length;
                _published = idx;
                if (frameIndex - lastLog >= _fps * 2)
                {
                    lastLog = frameIndex;
                    Debug.Log($"[dbg][video] reader frame={frameIndex} audioTime={MajCtx._timeProvider.AudioTime:F2}");
                }
            }
            _running = false;
        }

        /// <summary>Uploads the newest frame to the texture. Call on the main thread.</summary>
        public void UpdateFrame()
        {
            int pub = _published;
            if (pub == _consumed)
                return; // nothing new since the last upload
            _texture.LoadRawTextureData(_buffers[pub]);
            _texture.Apply();
            _consumed = pub;
            if ((++_dbgFrameUploads & 255) == 0)
                Debug.Log($"[dbg][video] UpdateFrame uploads={_dbgFrameUploads}");
        }

        public void Dispose()
        {
            _running = false;
            _paused = false;
            try
            {
                if (_proc.IsValid)
                {
                    FFmpegPipe.Kill(_proc);
                    FFmpegPipe.Wait(_proc);
                    FFmpegPipe.ClosePipe(_proc);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BgVideoPipe] cleanup: {e.Message}");
            }

            try { _readerThread?.Join(500); } catch { }
            _readerThread = null;

            if (_sprite != null) UnityEngine.Object.Destroy(_sprite);
            if (_texture != null) UnityEngine.Object.Destroy(_texture);
            _sprite = null!;
            _texture = null!;
        }

        public static string QuoteShellArgument(string value) =>
            "'" + value.Replace("'", "'\"'\"'") + "'";
    }
}
