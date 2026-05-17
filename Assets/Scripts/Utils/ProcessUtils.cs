using System;
using System.Runtime.InteropServices;

/// <summary>
/// 进程工具类 - 使用 P/Invoke 实现，FUCKING IL2CPP
/// </summary>
public static class ProcessUtils
{
    #region P/Invoke Declarations

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public int dwX, dwY, dwXSize, dwYSize;
        public int dwXCountChars, dwYCountChars, dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessW(
        string lpApplicationName,
        string lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ShellExecuteW(IntPtr hwnd, string lpOperation, string lpFile,
        string lpParameters, string lpDirectory, int nShowCmd);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentProcessId();

    private const uint CREATE_NO_WINDOW = 0x08000000;
    private const uint INFINITE = 0xFFFFFFFF;
    private const uint WAIT_OBJECT_0 = 0;

    #endregion

    /// <summary>
    /// 获取当前进程 ID
    /// </summary>
    public static uint CurrentProcessId => GetCurrentProcessId();

    /// <summary>
    /// 启动进程并等待其完成
    /// </summary>
    /// <param name="exePath">可执行文件路径</param>
    /// <param name="arguments">命令行参数</param>
    /// <param name="workingDir">工作目录</param>
    /// <returns>(是否成功启动, 退出码或错误码)</returns>
    public static (bool success, int exitCode) StartAndWait(string exePath, string arguments, string workingDir)
    {
        var si = new STARTUPINFO { cb = Marshal.SizeOf<STARTUPINFO>() };
        var commandLine = $"\"{exePath}\" {arguments}";

        if (!CreateProcessW(null, commandLine, IntPtr.Zero, IntPtr.Zero, false,
                CREATE_NO_WINDOW, IntPtr.Zero, workingDir, ref si, out var pi))
        {
            return (false, Marshal.GetLastWin32Error());
        }

        WaitForSingleObject(pi.hProcess, INFINITE);
        GetExitCodeProcess(pi.hProcess, out var exitCode);
        CloseHandle(pi.hProcess);
        CloseHandle(pi.hThread);

        return (true, (int)exitCode);
    }

    /// <summary>
    /// 启动进程但不等待（返回进程句柄）
    /// </summary>
    /// <param name="exePath">可执行文件路径</param>
    /// <param name="arguments">命令行参数</param>
    /// <param name="workingDir">工作目录</param>
    /// <returns>(是否成功启动, 进程句柄)</returns>
    public static (bool success, IntPtr processHandle) Start(string exePath, string arguments, string workingDir)
    {
        var si = new STARTUPINFO { cb = Marshal.SizeOf<STARTUPINFO>() };
        var commandLine = $"\"{exePath}\" {arguments}";

        if (!CreateProcessW(null, commandLine, IntPtr.Zero, IntPtr.Zero, false,
                CREATE_NO_WINDOW, IntPtr.Zero, workingDir, ref si, out var pi))
        {
            return (false, IntPtr.Zero);
        }

        CloseHandle(pi.hThread);
        return (true, pi.hProcess);
    }

    /// <summary>
    /// 检查进程是否已退出
    /// </summary>
    /// <param name="processHandle">进程句柄</param>
    /// <param name="exitCode">退出码（如果已退出）</param>
    /// <returns>是否已退出</returns>
    public static bool HasExited(IntPtr processHandle, out int exitCode)
    {
        exitCode = 0;
        if (WaitForSingleObject(processHandle, 0) == WAIT_OBJECT_0)
        {
            GetExitCodeProcess(processHandle, out var code);
            exitCode = (int)code;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 等待进程完成并获取退出码
    /// </summary>
    /// <param name="processHandle">进程句柄</param>
    /// <returns>退出码</returns>
    public static int WaitForExit(IntPtr processHandle)
    {
        WaitForSingleObject(processHandle, INFINITE);
        GetExitCodeProcess(processHandle, out var exitCode);
        return (int)exitCode;
    }

    /// <summary>
    /// 关闭进程句柄
    /// </summary>
    /// <param name="processHandle">进程句柄</param>
    public static void CloseProcessHandle(IntPtr processHandle)
    {
        if (processHandle != IntPtr.Zero)
            CloseHandle(processHandle);
    }

    /// <summary>
    /// 使用 Shell 打开文件或 URL
    /// </summary>
    /// <param name="file">文件路径或 URL</param>
    /// <param name="arguments">参数</param>
    /// <param name="operation">操作类型（如 "open", "edit"）</param>
    public static void ShellOpen(string file, string arguments = null, string operation = "open")
    {
        ShellExecuteW(IntPtr.Zero, operation, file, arguments, null, 1);
    }

    /// <summary>
    /// 在资源管理器中选中文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    public static void ShowInExplorer(string filePath)
    {
        ShellExecuteW(IntPtr.Zero, "open", "explorer.exe", $"/select,\"{filePath}\"", null, 1);
    }
}
