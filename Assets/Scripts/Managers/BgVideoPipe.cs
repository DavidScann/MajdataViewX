#nullable enable

using MajdataViewX.Native;
using System;
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
        public const int Width = 1280;
        public const int Height = 720;

        const int FrameBytes = Width * Height * 4;

        FFmpegPipe.PipeProcess _proc;
        Thread? _readerThread;
        volatile bool _running;

        // Double buffering: the reader publishes freshly decoded frames into
        // _latestBuffer; the main thread copies under the lock so the reader
        // can immediately start filling the other buffer again.
        readonly byte[] _frameA = new byte[FrameBytes];
        readonly byte[] _frameB = new byte[FrameBytes];
        volatile byte[] _latestBuffer;
        readonly object _swapLock = new();

        Texture2D _texture;
        Sprite _sprite;

        public Texture2D Texture => _texture;
        public Sprite Sprite => _sprite;

        public bool IsRunning => _running && _proc.IsValid;
        public bool HasFrame => _latestBuffer != null;

        private BgVideoPipe()
        {
            _latestBuffer = _frameA;
            _texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            _sprite = Sprite.Create(
                _texture,
                new Rect(0, 0, Width, Height),
                new Vector2(0.5f, 0.5f));
        }

        public static BgVideoPipe? Start(string videoPath, double startSec)
        {
            var player = new BgVideoPipe();
            if (!player.TryStart(videoPath, startSec))
            {
                player.Dispose();
                return null;
            }
            return player;
        }

        bool TryStart(string videoPath, double startSec)
        {
            try
            {
                var quoted = QuoteShellArgument(videoPath);
                var seek = startSec > 0
                    ? $"-ss {startSec.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)} "
                    : "";
                var cmd =
                    $"{FfmpegEncoder.Binary} -hide_banner -loglevel error -re {seek}-i {quoted} " +
                    $"-vf scale={Width}:{Height},vflip " +
                    $"-f rawvideo -pix_fmt rgba -vcodec rawvideo -";
                _proc = FFmpegPipe.SpawnIo(cmd);
                if (!_proc.IsValid) return false;

                _running = true;
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

        void ReaderLoop()
        {
            var buffer = _frameA;
            var consecutiveEof = 0;
            while (_running)
            {
                int got = FFmpegPipe.ReadFrame(_proc, buffer, FrameBytes);
                if (got != FrameBytes)
                {
                    // EOF (video ended or ffmpeg died). Give up after a few
                    // consecutive short reads; Dispose() also stops the loop.
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
            if (_latestBuffer == null) return;

            byte[] frame;
            lock (_swapLock)
            {
                // Copy out so the reader can overwrite the buffer immediately.
                frame = (byte[])_latestBuffer.Clone();
            }

            _texture.LoadRawTextureData(frame);
            _texture.Apply();
        }

        public void Dispose()
        {
            _running = false;
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
