#nullable enable

using MajdataViewX.Native;
using System;
using System.Globalization;
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

        FFmpegPipe.PipeProcess _proc;
        Thread? _readerThread;
        volatile bool _running;
        volatile bool _paused;

        byte[] _frameA;
        byte[] _frameB;
        volatile byte[] _latestBuffer;
        readonly object _swapLock = new();

        Texture2D _texture;
        Sprite _sprite;

        public int Width { get; }
        public int Height { get; }
        public Texture2D Texture => _texture;
        public Sprite Sprite => _sprite;

        public bool IsRunning => _running && _proc.IsValid;

        BgVideoPipe(int width, int height)
        {
            Width = width;
            Height = height;
            var frameBytes = width * height * 4;
            _frameA = new byte[frameBytes];
            _frameB = new byte[frameBytes];
            _latestBuffer = _frameA;

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
            var (width, height) = ProbeScaledSize(videoPath);
            var player = new BgVideoPipe(width, height);
            if (!player.TryStart(videoPath, startSec))
            {
                player.Dispose();
                return null;
            }
            return player;
        }

        /// <summary>
        /// Probes the video's native dimensions with ffprobe and computes a
        /// scaled size that preserves the aspect ratio (max dimension 1920,
        /// even dimensions for ffmpeg).
        /// </summary>
        static (int Width, int Height) ProbeScaledSize(string videoPath)
        {
            int nativeW = 1280, nativeH = 720;
            try
            {
                var quoted = QuoteShellArgument(videoPath);
                var cmd = $"ffprobe -v error -select_streams v:0 " +
                    $"-show_entries stream=width,height -of csv=s=x:p=0 {quoted}";
                var proc = FFmpegPipe.SpawnIo(cmd);
                if (proc.IsValid)
                {
                    var buf = new byte[64];
                    int got = FFmpegPipe.ReadFrame(proc, buf, buf.Length);
                    FFmpegPipe.Kill(proc);
                    FFmpegPipe.Wait(proc);
                    FFmpegPipe.ClosePipe(proc);
                    if (got > 0)
                    {
                        var text = System.Text.Encoding.UTF8.GetString(buf, 0, got).Trim();
                        var parts = text.Split('x');
                        if (parts.Length == 2 &&
                            int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out nativeW) &&
                            int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out nativeH) &&
                            nativeW > 0 && nativeH > 0)
                        {
                            Debug.Log($"[BgVideoPipe] probed {nativeW}x{nativeH}");
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
                Debug.LogWarning($"[BgVideoPipe] probe failed, using 1280x720: {e.Message}");
                nativeW = 1280; nativeH = 720;
            }

            // Scale to fit MaxDimension while keeping aspect; force even numbers.
            float scale = Mathf.Min(1f, MaxDimension / (float)Math.Max(nativeW, nativeH));
            int w = Mathf.Clamp((int)(nativeW * scale) & ~1, 2, MaxDimension);
            int h = Mathf.Clamp((int)(nativeH * scale) & ~1, 2, MaxDimension);
            return (w, h);
        }

        bool TryStart(string videoPath, double startSec)
        {
            try
            {
                var quoted = QuoteShellArgument(videoPath);
                var seek = startSec > 0
                    ? $"-ss {startSec.ToString("0.###", CultureInfo.InvariantCulture)} "
                    : "";
                var cmd =
                    $"{FfmpegEncoder.Binary} -hide_banner -loglevel error -re {seek}-i {quoted} " +
                    $"-vf scale={Width}:{Height},vflip " +
                    $"-f rawvideo -pix_fmt rgba -vcodec rawvideo -";
                _proc = FFmpegPipe.SpawnIo(cmd);
                if (!_proc.IsValid) return false;

                _running = true;
                _paused = false;
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
            var buffer = _frameA;
            var frameBytes = _frameA.Length;
            var consecutiveEof = 0;
            while (_running)
            {
                // Paused: stop reading so ffmpeg's pipe fills and it blocks —
                // a natural pause that keeps the last frame on screen.
                if (_paused)
                {
                    Thread.Sleep(10);
                    continue;
                }

                int got = FFmpegPipe.ReadFrame(_proc, buffer, frameBytes);
                if (got != frameBytes)
                {
                    consecutiveEof++;
                    if (consecutiveEof >= 5) break;
                    Thread.Sleep(10);
                    continue;
                }
                consecutiveEof = 0;

                lock (_swapLock)
                {
                    _latestBuffer = buffer;
                }
                buffer = ReferenceEquals(buffer, _frameA) ? _frameB : _frameA;
            }
            _running = false;
        }

        /// <summary>Uploads the newest frame to the texture. Call on the main thread.</summary>
        public void UpdateFrame()
        {
            byte[] frame;
            lock (_swapLock)
            {
                frame = (byte[])_latestBuffer.Clone();
            }

            _texture.LoadRawTextureData(frame);
            _texture.Apply();
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
