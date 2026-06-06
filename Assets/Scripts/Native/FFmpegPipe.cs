using System;
using System.Runtime.InteropServices;

public static class FFmpegPipe
{
#if UNITY_STANDALONE_WIN
    const string DllName = "ffmpeg_pipe";
#else
    const string DllName = "__Internal";
#endif

    [DllImport(DllName)] static extern IntPtr ffmpeg_spawn(string command, out IntPtr stdin_fd);
    [DllImport(DllName)] static extern IntPtr ffmpeg_spawn_simple(string command);
    [DllImport(DllName)] static extern int ffmpeg_write(IntPtr fd, byte[] buf, int len);
    [DllImport(DllName)] static extern void ffmpeg_close(IntPtr fd);
    [DllImport(DllName)] static extern int ffmpeg_wait(IntPtr handle);
    [DllImport(DllName)] static extern void ffmpeg_kill(IntPtr handle);

    public struct PipeProcess
    {
        public IntPtr Handle;
        public IntPtr StdinFd;
    }

    public static PipeProcess Spawn(string command)
    {
        var handle = ffmpeg_spawn(command, out var fd);
        return new PipeProcess { Handle = handle, StdinFd = fd };
    }

    public static IntPtr SpawnSimple(string command)
    {
        return ffmpeg_spawn_simple(command);
    }

    public static int Write(IntPtr fd, byte[] buf, int len)
    {
        return ffmpeg_write(fd, buf, len);
    }

    public static void ClosePipe(IntPtr fd)
    {
        ffmpeg_close(fd);
    }

    public static int Wait(IntPtr handle)
    {
        return ffmpeg_wait(handle);
    }

    public static void Kill(IntPtr handle)
    {
        ffmpeg_kill(handle);
    }
}