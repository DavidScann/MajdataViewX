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

        if (source != null)
        {
            File.Copy(source, destination, true);
            UnityEngine.Debug.Log($"Copied FFmpeg: {destination}");
        }
    }
}