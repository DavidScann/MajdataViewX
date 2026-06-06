using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public class FFmpegPostBuild : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        string source = null;
        string destination = null;

        switch (report.summary.platform)
        {
            case BuildTarget.StandaloneWindows64:
                source = "Assets/ExternalTools/FFmpeg/win-x64/ffmpeg.exe";
                destination = Path.Combine(
                    Path.GetDirectoryName(report.summary.outputPath)!,
                    "ffmpeg.exe");
                break;
            case BuildTarget.StandaloneOSX:
                source = "Assets/ExternalTools/FFmpeg/osx/ffmpeg";
                destination = Path.Combine(
                    report.summary.outputPath,
                    "Contents/MacOS/ffmpeg");
                break;
            case BuildTarget.StandaloneLinux64:
                source = "Assets/ExternalTools/FFmpeg/linux-x64/ffmpeg";
                destination = Path.Combine(
                    Path.GetDirectoryName(report.summary.outputPath)!,
                    "ffmpeg");
                break;
        }

        if (source == null) return;

        File.Copy(source, destination, true);
        UnityEngine.Debug.Log($"Copied FFmpeg: {destination}");

        if (report.summary.platform == BuildTarget.StandaloneOSX)
        {
            Run("chmod", $"+x \"{destination}\"");
            Run("codesign", $"--remove-signature \"{destination}\"");
        }
        else if (report.summary.platform == BuildTarget.StandaloneLinux64)
        {
            Run("chmod", $"+x \"{destination}\"");
        }
    }

    private static void Run(string file, string args)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            CreateNoWindow = true,
            UseShellExecute = false
        };
        using var proc = Process.Start(psi);
        proc?.WaitForExit();
    }
}