using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using MajdataViewX.Types.Rendering;
using UnityEngine;

namespace MajdataViewX.Native
{
    /// <summary>
    /// Picks the best available video encoder for the Linux pipe pipeline.
    /// Probes the ffmpeg binary once (cached) and prefers hardware encoders:
    /// h264_nvenc &gt; h264_vaapi &gt; h264_qsv &gt; h264_videotoolbox &gt; libx264.
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

        private static HwKind? _probed;
        private static string _vaapiDevice;

        /// <summary>Binary resolved from PATH, matching the editor's ffmpeg usage.</summary>
        public static string Binary => "ffmpeg";

        public static HwKind ProbedKind => _probed ??= Probe();

        /// <summary>
        /// ffmpeg arguments (everything between the rawvideo input and the output file)
        /// for encoding piped RGBA frames at the given settings.
        /// </summary>
        public static string BuildVideoArgs(int width, int height, int fps, ExportQuality quality)
        {
            var qp = quality switch
            {
                ExportQuality.Low => 28,
                ExportQuality.Medium => 23,
                ExportQuality.High => 18,
                _ => 14,
            };

            switch (ProbedKind)
            {
                case HwKind.Nvenc:
                    Debug.Log("[FfmpegEncoder] using h264_nvenc");
                    return $"-vf vflip -c:v h264_nvenc -preset p5 -tune hq -cq {qp} -pix_fmt yuv420p";
                case HwKind.Vaapi:
                    Debug.Log($"[FfmpegEncoder] using h264_vaapi ({_vaapiDevice})");
                    return $"-init_hw_device vaapi=va:{_vaapiDevice} -filter_hw_device va " +
                        $"-vf vflip,format=nv12,hwupload -c:v h264_vaapi -qp {qp}";
                case HwKind.Qsv:
                    Debug.Log("[FfmpegEncoder] using h264_qsv");
                    return $"-vf vflip,format=nv12 -c:v h264_qsv -global_quality {qp}";
                case HwKind.VideoToolbox:
                    Debug.Log("[FfmpegEncoder] using h264_videotoolbox");
                    return $"-vf vflip -c:v h264_videotoolbox -q:v {qp} -pix_fmt yuv420p";
                default:
                    Debug.Log("[FfmpegEncoder] using libx264");
                    return $"-vf vflip -c:v libx264 -preset veryfast -crf {qp} -pix_fmt yuv420p";
            }
        }

        public static string BuildMuxArgs(string videoName, string wavName, string finalName) =>
            $"-hide_banner -y -i \"{videoName}\" -i \"{wavName}\" " +
            "-c:v copy -c:a aac -b:a 320k -shortest " +
            $"\"{finalName}\"";

        private static HwKind Probe()
        {
            _vaapiDevice = FindVaapiDevice();
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var proc = new Process
                {
                    StartInfo = new ProcessStartInfo(Binary, "-hide_banner -encoders")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    }
                };
                proc.Start();
                var stdoutTask = proc.StandardOutput.ReadToEndAsync();
                var stderrTask = proc.StandardError.ReadToEndAsync();
                if (!proc.WaitForExit((int)TimeSpan.FromSeconds(5).TotalMilliseconds))
                {
                    proc.Kill();
                    return HwKind.None;
                }
                var output = stdoutTask.GetAwaiter().GetResult() +
                             stderrTask.GetAwaiter().GetResult();

                if (output.Contains("h264_nvenc")) return HwKind.Nvenc;
                if (output.Contains("h264_vaapi") && _vaapiDevice != null) return HwKind.Vaapi;
                if (output.Contains("h264_qsv")) return HwKind.Qsv;
                if (output.Contains("h264_videotoolbox")) return HwKind.VideoToolbox;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FfmpegEncoder] encoder probe failed: {ex.Message}");
            }
            return HwKind.None;
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
