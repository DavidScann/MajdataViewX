using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildScript
{
    public static void Build()
    {
        var targetStr = Environment.GetEnvironmentVariable("BUILD_TARGET") ?? "StandaloneLinux64";
        var target = targetStr switch
        {
            "StandaloneWindows" => BuildTarget.StandaloneWindows,
            "StandaloneWindows64" => BuildTarget.StandaloneWindows64,
            "StandaloneOSX" => BuildTarget.StandaloneOSX,
            _ => BuildTarget.StandaloneLinux64
        };
        var outputPath = Environment.GetEnvironmentVariable("BUILD_OUTPUT_PATH") ?? "build/Linux/MajdataViewX";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);

        // IL2CPP compiler config: "release" (no LTO, ~30s link) or "master" (full LTO, ~5min link, single-threaded)
        // Default to "release" for fast iteration; set IL2CPP_CONFIG=master for production builds.
        var il2cppConfig = Environment.GetEnvironmentVariable("IL2CPP_CONFIG");
        if (string.IsNullOrEmpty(il2cppConfig) || il2cppConfig == "release")
        {
            PlayerSettings.SetIl2CppCompilerConfiguration(
                BuildTargetGroup.Standalone,
                Il2CppCompilerConfiguration.Release);
        }
        else if (il2cppConfig == "master")
        {
            PlayerSettings.SetIl2CppCompilerConfiguration(
                BuildTargetGroup.Standalone,
                Il2CppCompilerConfiguration.Master);
        }

        var options = new BuildPlayerOptions
        {
            scenes = new[]
            {
                "Assets/Scenes/Server.unity",
                "Assets/Scenes/SampleScene.unity"
            },
            locationPathName = outputPath,
            target = target,
            targetGroup = BuildTargetGroup.Standalone,
        };
        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception($"Build failed: {report.summary.result}");
        Debug.Log($"Build succeeded: {outputPath}");
    }
}
