using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
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

        // Linux keeps IL2CPP; Windows/Mac cross-builds from a Linux host use Mono
        // because those IL2CPP toolchains are not installed on Linux (mac-il2cpp and
        // win-il2cpp are macOS/Windows-host-only). Behaviourally identical to Linux:
        // set IL2CPP_MAC=1 when building on an actual macOS host to get native IL2CPP.
        var namedTarget = NamedBuildTarget.Standalone;
        var isMacHost = SystemInfo.operatingSystemFamily == OperatingSystemFamily.MacOSX;
        var forceMacIl2Cpp = Environment.GetEnvironmentVariable("IL2CPP_MAC") == "1";
        var useIl2Cpp = target is BuildTarget.StandaloneLinux64
            || (target is BuildTarget.StandaloneOSX && (isMacHost || forceMacIl2Cpp));
        PlayerSettings.SetScriptingBackend(
            namedTarget,
            useIl2Cpp ? ScriptingImplementation.IL2CPP : ScriptingImplementation.Mono2x);

        if (useIl2Cpp)
        {
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
