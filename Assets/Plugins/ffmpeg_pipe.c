#ifdef _WIN32
#include <windows.h>
#else
#include <unistd.h>
#include <stdlib.h>
#include <sys/wait.h>
#include <errno.h>
#include <signal.h>
#endif

#include <string.h>
#include <stdint.h>

#ifdef _WIN32

intptr_t ffmpeg_spawn(const char* command, intptr_t* stdin_fd)
{
    HANDLE hRead, hWrite;
    SECURITY_ATTRIBUTES sa;
    sa.nLength = sizeof(SECURITY_ATTRIBUTES);
    sa.lpSecurityDescriptor = NULL;
    sa.bInheritHandle = TRUE;

    if (!CreatePipe(&hRead, &hWrite, &sa, 0))
        return -1;
    SetHandleInformation(hWrite, HANDLE_FLAG_INHERIT, HANDLE_FLAG_INHERIT);

    STARTUPINFOA si;
    memset(&si, 0, sizeof(si));
    si.cb = sizeof(si);
    si.dwFlags = STARTF_USESTDHANDLES;
    si.hStdInput = hRead;
    si.hStdOutput = GetStdHandle(STD_OUTPUT_HANDLE);
    si.hStdError = GetStdHandle(STD_ERROR_HANDLE);

    size_t len = strlen(command) + 16;
    char* cmd = (char*)malloc(len);
    if (!cmd)
    {
        CloseHandle(hRead);
        CloseHandle(hWrite);
        return -1;
    }
    snprintf(cmd, len, "cmd /c \"%s\"", command);

    PROCESS_INFORMATION pi;
    memset(&pi, 0, sizeof(pi));

    if (!CreateProcessA(NULL, cmd, NULL, NULL, TRUE,
                        CREATE_NO_WINDOW, NULL, NULL, &si, &pi))
    {
        free(cmd);
        CloseHandle(hRead);
        CloseHandle(hWrite);
        return -1;
    }

    free(cmd);
    CloseHandle(hRead);
    CloseHandle(pi.hThread);
    *stdin_fd = (intptr_t)hWrite;
    return (intptr_t)pi.hProcess;
}

intptr_t ffmpeg_spawn_simple(const char* command)
{
    size_t len = strlen(command) + 16;
    char* cmd = (char*)malloc(len);
    if (!cmd) return -1;
    snprintf(cmd, len, "cmd /c \"%s\"", command);

    STARTUPINFOA si;
    memset(&si, 0, sizeof(si));
    si.cb = sizeof(si);

    PROCESS_INFORMATION pi;
    memset(&pi, 0, sizeof(pi));

    if (!CreateProcessA(NULL, cmd, NULL, NULL, FALSE,
                        CREATE_NO_WINDOW, NULL, NULL, &si, &pi))
    {
        free(cmd);
        return -1;
    }

    free(cmd);
    CloseHandle(pi.hThread);
    return (intptr_t)pi.hProcess;
}

int ffmpeg_write(intptr_t fd, const void* buf, int len)
{
    DWORD written;
    if (!WriteFile((HANDLE)fd, buf, (DWORD)len, &written, NULL))
        return -1;
    return (int)written;
}

void ffmpeg_close(intptr_t fd)
{
    CloseHandle((HANDLE)fd);
}

int ffmpeg_wait(intptr_t handle)
{
    WaitForSingleObject((HANDLE)handle, INFINITE);
    DWORD exitCode;
    if (!GetExitCodeProcess((HANDLE)handle, &exitCode))
        return -1;
    CloseHandle((HANDLE)handle);
    return (int)exitCode;
}

void ffmpeg_kill(intptr_t handle)
{
    TerminateProcess((HANDLE)handle, 1);
    CloseHandle((HANDLE)handle);
}

#else /* Unix */

intptr_t ffmpeg_spawn(const char* command, intptr_t* stdin_fd)
{
    int pipefd[2];
    if (pipe(pipefd) == -1)
        return -1;

    pid_t pid = fork();
    if (pid == -1)
    {
        close(pipefd[0]);
        close(pipefd[1]);
        return -1;
    }

    if (pid == 0)
    {
        dup2(pipefd[0], STDIN_FILENO);
        close(pipefd[0]);
        close(pipefd[1]);
        execl("/bin/sh", "sh", "-c", command, (char*)NULL);
        _exit(127);
    }

    close(pipefd[0]);
    *stdin_fd = (intptr_t)pipefd[1];
    return (intptr_t)pid;
}

intptr_t ffmpeg_spawn_simple(const char* command)
{
    pid_t pid = fork();
    if (pid == -1)
        return -1;

    if (pid == 0)
    {
        execl("/bin/sh", "sh", "-c", command, (char*)NULL);
        _exit(127);
    }

    return (intptr_t)pid;
}

int ffmpeg_write(intptr_t fd, const void* buf, int len)
{
    int f = (int)fd;
    ssize_t total = 0;
    while (total < len)
    {
        ssize_t n = write(f, (const char*)buf + total, (size_t)(len - total));
        if (n == -1)
        {
            if (errno == EINTR) continue;
            return -1;
        }
        total += n;
    }
    return (int)total;
}

void ffmpeg_close(intptr_t fd)
{
    close((int)fd);
}

int ffmpeg_wait(intptr_t handle)
{
    int status;
    if (waitpid((pid_t)handle, &status, 0) == -1)
        return -1;
    if (WIFEXITED(status))
        return WEXITSTATUS(status);
    if (WIFSIGNALED(status))
        return 128 + WTERMSIG(status);
    return -1;
}

void ffmpeg_kill(intptr_t handle)
{
    kill((pid_t)handle, SIGTERM);
}

#endif