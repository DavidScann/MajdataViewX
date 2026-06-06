using System.IO;
using UnityEngine;

public static class MajEnv
{
#if UNITY_EDITOR
    // 编辑器下，指向项目根目录（Assets 的上一级）
    public static string BaseDir => 
        new DirectoryInfo(Application.dataPath).Parent!.FullName;
#else
        // 打包后，Application.dataPath 的上一级在 Windows 下是 exe 目录
        // 但为了兼顾 Mac 等平台，用 AppContext 或者是 dataPath 的物理同级更安全
        public static string BaseDir => System.AppDomain.CurrentDomain.BaseDirectory;
#endif
    
    public static string GetPath(string relativePath) => 
        Path.Combine(BaseDir, relativePath);
    
    public static string FFmpegPath
    {
        get
        {
#if UNITY_STANDALONE_WIN
        return Path.Combine(
            Path.GetDirectoryName(Application.dataPath)!,
            "ffmpeg.exe");
#elif UNITY_STANDALONE_OSX
            return Path.Combine(
                Application.dataPath,
                "MacOS/ffmpeg");
#elif UNITY_STANDALONE_LINUX
        return Path.Combine(
            Path.GetDirectoryName(Application.dataPath)!,
            "ffmpeg");
#else
        throw new PlatformNotSupportedException();
#endif
        }
    }
}