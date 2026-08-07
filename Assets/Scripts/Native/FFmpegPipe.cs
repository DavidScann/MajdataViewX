using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace MajdataViewX.Native
{
    public static class FFmpegPipe
    {
#if !UNITY_EDITOR
#if UNITY_STANDALONE_WIN
        const string DllName = "ffmpeg_pipe";
#elif UNITY_STANDALONE_LINUX
        // The shared library is deployed as libffmpeg_pipe.so next to the player.
        const string DllName = "libffmpeg_pipe";
#else
        // Other standalone platforms would link the plugin statically (IL2CPP).
        const string DllName = "__Internal";
#endif

        [DllImport(DllName)] static extern IntPtr ffmpeg_spawn(string command, out IntPtr stdin_fd);
        [DllImport(DllName)] static extern IntPtr ffmpeg_spawn_simple(string command);
        [DllImport(DllName)] static extern IntPtr ffmpeg_spawn_io(string command, out IntPtr stdin_fd, out IntPtr stdout_fd);
        [DllImport(DllName)] static extern int ffmpeg_write(IntPtr fd, byte[] buf, int len);
        [DllImport(DllName)] static extern int ffmpeg_read(IntPtr fd, byte[] buf, int len);
        [DllImport(DllName)] static extern int ffmpeg_read(IntPtr fd, IntPtr buf, int len);
        [DllImport(DllName)] static extern void ffmpeg_close(IntPtr fd);
        [DllImport(DllName)] static extern int ffmpeg_wait(IntPtr handle);
        [DllImport(DllName)] static extern void ffmpeg_kill(IntPtr handle);
#endif

        public struct PipeProcess
        {
#if UNITY_EDITOR
            public Process Process;
            public Stream StdinStream;
            public Stream StdoutStream;
            public bool IsValid => Process != null;
#else
            public IntPtr Handle;
            public IntPtr StdinFd;
            public IntPtr StdoutFd;
            public bool IsValid => Handle != IntPtr.Zero;
#endif
        }

        public static PipeProcess Spawn(string command)
        {
#if UNITY_EDITOR
            var parts = ParseCommand(command);
            var psi = new ProcessStartInfo(parts.FileName, parts.Arguments)
            {
                WorkingDirectory = parts.WorkDir ?? ".",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardInput = true
            };
            var proc = Process.Start(psi);
            return new PipeProcess { Process = proc, StdinStream = proc?.StandardInput.BaseStream };
#else
            var handle = ffmpeg_spawn(command, out var fd);
            return new PipeProcess { Handle = handle, StdinFd = fd };
#endif
        }

        /// <summary>
        /// Spawns a process with both a stdin write pipe and a stdout read pipe
        /// (used to stream decoded video frames out of ffmpeg).
        /// </summary>
        public static PipeProcess SpawnIo(string command)
        {
#if UNITY_EDITOR
            var parts = ParseCommand(command);
            var psi = new ProcessStartInfo(parts.FileName, parts.Arguments)
            {
                WorkingDirectory = parts.WorkDir ?? ".",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            };
            var proc = Process.Start(psi);
            return new PipeProcess
            {
                Process = proc,
                StdinStream = proc?.StandardInput.BaseStream,
                StdoutStream = proc?.StandardOutput.BaseStream
            };
#else
            var handle = ffmpeg_spawn_io(command, out var inFd, out var outFd);
            return new PipeProcess { Handle = handle, StdinFd = inFd, StdoutFd = outFd };
#endif
        }

        public static PipeProcess SpawnSimple(string command)
        {
#if UNITY_EDITOR
            var parts = ParseCommand(command);
            var psi = new ProcessStartInfo(parts.FileName, parts.Arguments)
            {
                WorkingDirectory = parts.WorkDir ?? ".",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            var proc = Process.Start(psi);
            return new PipeProcess { Process = proc, StdinStream = null };
#else
            var handle = ffmpeg_spawn_simple(command);
            return new PipeProcess { Handle = handle, StdinFd = IntPtr.Zero };
#endif
        }

        public static int Write(PipeProcess proc, byte[] buf, int len)
        {
#if UNITY_EDITOR
            if (proc.StdinStream == null) return -1;
            proc.StdinStream.Write(buf, 0, len);
            return len;
#else
            return ffmpeg_write(proc.StdinFd, buf, len);
#endif
        }

        /// <summary>
        /// Reads up to <paramref name="len"/> bytes; blocks until data is
        /// available. Returns bytes read, or 0 on EOF.
        /// </summary>
        public static int Read(PipeProcess proc, byte[] buf, int len)
        {
#if UNITY_EDITOR
            if (proc.StdoutStream == null) return -1;
            return proc.StdoutStream.Read(buf, 0, len);
#else
            return ffmpeg_read(proc.StdoutFd, buf, len);
#endif
        }

        /// <summary>Reads exactly <paramref name="len"/> bytes (one raw frame).</summary>
        public static int ReadFrame(PipeProcess proc, byte[] buf, int len)
        {
            int total = 0;
            while (total < len)
            {
                int n = Read(proc, buf, total, len - total);
                if (n <= 0) break;
                total += n;
            }
            return total;
        }

        public static int Read(PipeProcess proc, byte[] buf, int offset, int len)
        {
#if UNITY_EDITOR
            if (proc.StdoutStream == null) return -1;
            return proc.StdoutStream.Read(buf, offset, len);
#else
            unsafe
            {
                fixed (byte* p = &buf[offset])
                {
                    return ffmpeg_read(proc.StdoutFd, (IntPtr)p, len);
                }
            }
#endif
        }

        public static void ClosePipe(PipeProcess proc)
        {
#if UNITY_EDITOR
            proc.StdinStream?.Close();
            proc.StdoutStream?.Close();
#else
            if (proc.StdinFd != IntPtr.Zero) ffmpeg_close(proc.StdinFd);
            if (proc.StdoutFd != IntPtr.Zero) ffmpeg_close(proc.StdoutFd);
#endif
        }

        public static int Wait(PipeProcess proc)
        {
#if UNITY_EDITOR
            if (proc.Process == null) return -1;
            proc.Process.WaitForExit();
            return proc.Process.ExitCode;
#else
            return ffmpeg_wait(proc.Handle);
#endif
        }

        public static void Kill(PipeProcess proc)
        {
#if UNITY_EDITOR
            proc.Process?.Kill();
#else
            ffmpeg_kill(proc.Handle);
#endif
        }

#if UNITY_EDITOR
        private static (string FileName, string Arguments, string WorkDir) ParseCommand(string command)
        {
            command = command.Trim();
            string workDir = null;

            if (command.StartsWith("cd "))
            {
                var end = command.IndexOf(" && ");
                if (end > 0)
                {
                    workDir = command.Substring(3, end - 3).Trim('"');
                    command = command.Substring(end + 4);
                }
            }

            if (command.StartsWith("\""))
            {
                var end = command.IndexOf('"', 1);
                if (end > 0)
                    return (command.Substring(1, end - 1), command.Substring(end + 1).Trim(), workDir);
            }

            var space = command.IndexOf(' ');
            if (space > 0)
                return (command.Substring(0, space), command.Substring(space + 1), workDir);
            return (command, "", workDir);
        }
#endif
    }
}
