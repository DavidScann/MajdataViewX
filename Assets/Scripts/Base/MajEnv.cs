using System.IO;
using UnityEngine;

namespace MajdataViewX.Base
{
    public static class MajEnv
    {
#if UNITY_EDITOR
        // 编辑器下，指向项目根目录（Assets 的上一级）
        public static string MajBase = new DirectoryInfo(Application.dataPath).Parent!.FullName;
#else
        // 打包后，Application.dataPath 的上一级在 Windows 下是 exe 目录
        // 但为了兼顾 Mac 等平台，用 AppContext 或者是 dataPath 的物理同级更安全
        public static string MajBase => System.AppDomain.CurrentDomain.BaseDirectory;
#endif

        public static string GetPath(string relativePath) =>
            Path.Combine(MajBase, relativePath);

#if UNITY_STANDALONE_OSX
        // macOS Finder launch has minimal PATH (no homebrew). Augment once, parity with editor's MainWindow mac PATH fix.
        static MajEnv()
        {
            try
            {
                var cur = System.Environment.GetEnvironmentVariable("PATH") ?? "";
                if (!cur.Contains("/opt/homebrew/bin"))
                    System.Environment.SetEnvironmentVariable("PATH", $"{cur}:/usr/local/bin:/opt/homebrew/bin:/opt/homebrew/sbin");
            }
            catch { }
        }
#endif
    }
}