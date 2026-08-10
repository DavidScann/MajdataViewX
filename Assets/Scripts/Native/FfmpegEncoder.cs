using System;
using System.IO;
using System.Linq;
using MajdataViewX.Types.Enums;
using MajdataViewX.Types.Rendering;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MajdataViewX.Native
{
    /// <summary>
    /// Picks the best available video encoder for the Linux pipe pipeline.
    /// Probes the ffmpeg binary once (cached) and prefers hardware encoders
    /// per selected codec:
    ///   H264: h264_nvenc &gt; h264_vaapi &gt; h264_qsv &gt; h264_videotoolbox &gt; libx264
    ///   HEVC: hevc_nvenc &gt; hevc_vaapi &gt; hevc_qsv &gt; hevc_videotoolbox &gt; libx265
    ///   AV1 : av1_nvenc &gt; av1_vaapi &gt; libsvtav1 (CPU)
    /// </summary>
    public static class FfmpegEncoder
    {
        public enum HwKind
        {
            None,
            Nvenc,
            Vaapi,
            Qsv,
            VideoToolbox,
        }

        private static string _encodersOutput;
        private static string _vaapiDevice;
        private static string _hwaccelsOutput;
        private static string _decoderPrefix;
        private static bool _decoderProbed;

        /// <summary>Binary resolved from PATH, matching the editor's ffmpeg usage.</summary>
        public static string Binary => "ffmpeg";

        /// <summary>
        /// ffmpeg arguments (everything between the rawvideo input and the output file)
        /// for encoding piped RGBA frames at the given settings.
        /// </summary>
        public static string BuildVideoArgs(int width, int height, int fps, ExportQuality quality, ExportCodec codec)
        {
            var qp = quality switch
            {
                ExportQuality.Low => 28,
                ExportQuality.Medium => 23,
                ExportQuality.High => 18,
                _ => 14,
            };

            var encoder = PickEncoder(codec);
            if (encoder == null)
                throw new InvalidOperationException(
                    $"No usable video encoder found for codec {codec}. " +
                    "Check that ffmpeg is installed with the required encoders.");

            Debug.Log($"[FfmpegEncoder] using {encoder}");
            switch (encoder)
            {
                case "libx264":
                    return $"-vf vflip -c:v libx264 -preset veryfast -crf {qp} -pix_fmt yuv420p";
                case "libx265":
                    return $"-vf vflip -c:v libx265 -preset veryfast -crf {qp} -pix_fmt yuv420p";
                case "libsvtav1":
                    return $"-vf vflip -c:v libsvtav1 -preset 8 -crf {qp} -pix_fmt yuv420p";
                case "h264_nvenc":
                case "hevc_nvenc":
                case "av1_nvenc":
                    return $"-vf vflip -c:v {encoder} -preset p5 -tune hq -cq {qp} -pix_fmt yuv420p";
                case "h264_vaapi":
                case "hevc_vaapi":
                case "av1_vaapi":
                    return $"-init_hw_device vaapi=va:{_vaapiDevice} -filter_hw_device va " +
                        $"-vf vflip,format=nv12,hwupload -c:v {encoder} -qp {qp}";
                case "h264_qsv":
                case "hevc_qsv":
                    return $"-vf vflip,format=nv12 -c:v {encoder} -global_quality {qp}";
                case "h264_videotoolbox":
                case "hevc_videotoolbox":
                    return $"-vf vflip -c:v {encoder} -q:v {qp} -pix_fmt yuv420p";
                default:
                    throw new InvalidOperationException(
                        $"Unhandled encoder {encoder} for codec {codec}.");
            }
        }

        public static string BuildMuxArgs(string videoName, string wavName, string finalName) =>
            $"-hide_banner -y -i \"{videoName}\" -i \"{wavName}\" " +
            "-c:v copy -c:a aac -b:a 320k -shortest " +
            $"\"{finalName}\"";

        private static string PickEncoder(ExportCodec codec)
        {
            var output = GetEncodersOutput();
            var candidates = codec switch
            {
                ExportCodec.HEVC => new[] { "hevc_nvenc", "hevc_vaapi", "hevc_qsv", "hevc_videotoolbox", "libx265" },
                ExportCodec.AV1 => new[] { "av1_nvenc", "av1_vaapi", "libsvtav1" },
                _ => new[] { "h264_nvenc", "h264_vaapi", "h264_qsv", "h264_videotoolbox", "libx264" },
            };
            foreach (var candidate in candidates)
            {
                if (!output.Contains(candidate))
                    continue;
                // Free hardware-presence check (no process spawn): skips
                // encoders whose GPU is not present at all, e.g. h264_nvenc
                // on a machine without an NVIDIA GPU.
                if (!HwEncoderPresent(candidate))
                    continue;
                // CPU encoders always work if listed - no test needed.
                if (IsCpuEncoder(candidate) || VerifyEncoder(candidate))
                    return candidate;
                Debug.LogWarning($"[FfmpegEncoder] {candidate} failed verification, trying next");
            }
            return null;
        }

        private static bool IsCpuEncoder(string encoder) =>
            encoder is "libx264" or "libx265" or "libsvtav1";

        private static bool HwEncoderPresent(string encoder)
        {
            if (encoder.Contains("vaapi") || encoder.Contains("qsv"))
                return _vaapiDevice != null;
            if (encoder.Contains("nvenc"))
                return HasNvidia();
            if (encoder.Contains("videotoolbox"))
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                return true;
#else
                return false;
#endif
            return true;
        }

        /// <summary>True when an NVIDIA driver is present (Linux exposes its version here).</summary>
        public static bool HasNvidia()
        {
            try
            {
                return File.Exists("/proc/driver/nvidia/version");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Encodes one test frame to confirm the encoder actually works on
        /// this machine. ffmpeg -encoders lists encoders that are compiled in
        /// but unusable here (e.g. h264_nvenc with a stale driver), which made
        /// the export fail with "FFmpeg pipe write failed". The success result
        /// is cached on disk, so this spawns at most once per machine.
        /// </summary>
        private static bool VerifyEncoder(string encoder)
        {
            try
            {
                var cacheFile = Path.Combine(Application.temporaryCachePath, $"ffmpeg_enc_ok_{encoder}.txt");
                if (File.Exists(cacheFile))
                    return true;

                var errFile = Path.Combine(Application.temporaryCachePath, $"ffmpeg_enc_test_{encoder}.txt");
                if (File.Exists(errFile)) File.Delete(errFile);
                var deviceArgs = encoder.Contains("vaapi") && _vaapiDevice != null
                    ? $"-init_hw_device vaapi=va:{_vaapiDevice} -filter_hw_device va "
                    : string.Empty;
                var filterArgs = encoder.Contains("vaapi")
                    ? " -vf format=nv12,hwupload"
                    : string.Empty;
                var cmd = $"{Binary} -hide_banner -loglevel error {deviceArgs}" +
                    $"-f lavfi -i testsrc=s=128x72:r=30:d=0.05 -frames:v 1" +
                    $"{filterArgs} -c:v {encoder} -f null - > \"{errFile}\" 2>&1";
                var proc = FFmpegPipe.SpawnSimple(cmd);
                if (!proc.IsValid) return false;
                var code = FFmpegPipe.Wait(proc);
                if (code != 0)
                {
                    if (File.Exists(errFile))
                    {
                        var err = File.ReadAllText(errFile).Trim();
                        if (err.Length > 0)
                            Debug.LogWarning($"[FfmpegEncoder] {encoder} verification failed: {err}");
                    }
                    return false;
                }
                File.WriteAllText(cacheFile, "ok");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FfmpegEncoder] {encoder} verification error: {ex.Message}");
                return false;
            }
        }

        private static string FirstOf(string encodersOutput, params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (candidate.Contains("vaapi") && _vaapiDevice == null)
                    continue;
                if (encodersOutput.Contains(candidate))
                    return candidate;
            }
            return null;
        }

        /// <summary>
        /// ffmpeg args enabling hardware decode of the background video
        /// ("-hwaccel cuda -hwaccel_output_format nv12 " etc.), or "" when
        /// no usable GPU decoder was detected. Decoded frames are downloaded
        /// to system memory so the existing scale/vflip/rawvideo chain keeps
        /// working unchanged.
        /// </summary>
        public static string HwDecodePrefix
        {
            get
            {
                if (!_decoderProbed)
                {
                    _decoderProbed = true;
                    _decoderPrefix = DetectHwDecode();
                }
                return _decoderPrefix;
            }
        }

        /// <summary>Falls back to CPU decoding for the rest of the session.</summary>
        public static void DisableHwDecode()
        {
            _decoderProbed = true;
            _decoderPrefix = string.Empty;
        }

        private static string DetectHwDecode()
        {
            try
            {
                var hwaccels = GetHwaccelsOutput();
                if (string.IsNullOrEmpty(hwaccels))
                    return string.Empty;

                if (HasNvidia() && hwaccels.Contains("cuda"))
                    return "-hwaccel cuda -hwaccel_output_format nv12 ";

                var vaapiDevice = FindVaapiDevice();
                if (hwaccels.Contains("vaapi") && vaapiDevice != null)
                    return $"-init_hw_device vaapi=va:{vaapiDevice} " +
                        "-hwaccel vaapi -hwaccel_output_format nv12 ";

                if (hwaccels.Contains("qsv") && vaapiDevice != null)
                    return "-hwaccel qsv -hwaccel_output_format nv12 ";
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FfmpegEncoder] hw decode probe failed: {ex.Message}");
            }
            return string.Empty;
        }

        private static string GetHwaccelsOutput()
        {
            if (_hwaccelsOutput != null)
                return _hwaccelsOutput;

            try
            {
                // IL2CPP on Linux cannot spawn processes via System.Diagnostics
                // (that's why the pipe plugin exists), so run the probe through
                // FFmpegPipe's shell spawn and capture output via redirection.
                var probeFile = Path.Combine(Application.temporaryCachePath, "ffmpeg_hwaccels.txt");
                if (File.Exists(probeFile)) File.Delete(probeFile);
                var probeCmd = $"{Binary} -hide_banner -hwaccels > \"{probeFile}\" 2>&1";
                var proc = FFmpegPipe.SpawnSimple(probeCmd);
                if (proc.IsValid)
                {
                    FFmpegPipe.Wait(proc);
                    if (File.Exists(probeFile))
                        _hwaccelsOutput = File.ReadAllText(probeFile);
                }
                if (_hwaccelsOutput == null)
                    _hwaccelsOutput = string.Empty;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FfmpegEncoder] hwaccels probe failed: {ex.Message}");
                _hwaccelsOutput = string.Empty;
            }
            return _hwaccelsOutput;
        }

        private static string GetEncodersOutput()
        {
            if (_encodersOutput != null)
                return _encodersOutput;

            _vaapiDevice = FindVaapiDevice();
            try
            {
                // IL2CPP on Linux cannot spawn processes via System.Diagnostics
                // (that's why the pipe plugin exists), so run the probe through
                // FFmpegPipe's shell spawn and capture output via redirection.
                var probeFile = Path.Combine(Application.temporaryCachePath, "ffmpeg_encoders.txt");
                if (File.Exists(probeFile)) File.Delete(probeFile);
                var probeCmd = $"{Binary} -hide_banner -encoders > \"{probeFile}\" 2>&1";
                var proc = FFmpegPipe.SpawnSimple(probeCmd);
                if (proc.IsValid)
                {
                    FFmpegPipe.Wait(proc);
                    if (File.Exists(probeFile))
                        _encodersOutput = File.ReadAllText(probeFile);
                }
                if (_encodersOutput == null)
                    _encodersOutput = string.Empty;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FfmpegEncoder] encoder probe failed: {ex.Message}");
                _encodersOutput = string.Empty;
            }
            return _encodersOutput;
        }

        private static string FindVaapiDevice()
        {
            try
            {
                var dir = new DirectoryInfo("/dev/dri");
                if (!dir.Exists) return null;
                return dir.GetFiles("renderD*")
                    .OrderBy(f => f.Name)
                    .Select(f => f.FullName)
                    .FirstOrDefault();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FfmpegEncoder] VA-API device scan failed: {ex.Message}");
                return null;
            }
        }
    }
}
