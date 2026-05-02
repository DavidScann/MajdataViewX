#region

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

#endregion

public class ScreenRecorder : MonoBehaviour
{
    TimeProvider timeProvider;
    BgManager bgManager;
    AudioManager audioManager;

    Text errText;

    public bool IsRecording { get; private set; }

    private void Awake()
    {
        Majdata<ScreenRecorder>.Instance = this;
    }

    private void Start()
    {
        timeProvider = Majdata<TimeProvider>.Instance!;
        bgManager = Majdata<BgManager>.Instance!;
        audioManager = Majdata<AudioManager>.Instance!;
        errText = GameObject.Find("ErrText").GetComponent<Text>();
    }

    public void StartRecording(string maidataPath, int fps)
    {
        StartCoroutine(CaptureScreen(maidataPath, fps));
    }

    public void StopRecording()
    {
        IsRecording = false;
    }

    public void ResetState()
    {
        StopRecording();
    }

    private IEnumerator CaptureScreen(string maidataPath, int fps)
    {
        //BUG: maybe still has some problemSSSSSSSS...
        // 分辨率偶数检查
        if (Screen.width % 2 != 0 || Screen.height % 2 != 0)
        {
            errText.text = $"无法渲染：分辨率 {Screen.width}x{Screen.height} 不是偶数。";
            yield break;
        }
        
        // args
        var ffmpegPath = Application.streamingAssetsPath + "\\ffmpeg.exe";
        var wavName = "temp.wav";
        var videoName = "temp.mov"; 
        var finalName = "out.mov";
        
        var outArgs = 
            "-hide_banner -y " +
            $"-f rawvideo -pix_fmt rgba -s {Screen.width}x{Screen.height} -r {fps} " +
            @"-i \\.\pipe\majdataRec " +
            "-vf vflip " +
            "-c:v libvpx-vp9 -crf 25 -b:v 0 -pix_fmt yuva420p " + 
            $"\"{videoName}\"";
        
        var muxArgs = 
            "-hide_banner -y " +
            $"-i \"{videoName}\" -i \"{wavName}\" " +
            "-c:v copy -c:a libopus -b:a 320k -shortest " +
            $"\"{finalName}\"";
        
        var startInfo = new ProcessStartInfo(ffmpegPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = maidataPath
        };
        
        // camera
        var rt = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
        rt.Create();
        var camera = Camera.main;
        camera.targetTexture = rt;

        // audio
        audioManager.PrepareRecordingBuffer();
        
        IsRecording = true;

        var frameQueue = new Queue<byte[]>();
        var pendingFrameCount = 0;
        var touchHoldStartTime = 0f;
        var isTouchHoldRising = false;
        using (var pipeServer = new NamedPipeServerStream("majdataRec", PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
        {
            startInfo.Arguments = outArgs;
            var outProcess = Process.Start(startInfo);
            pipeServer.WaitForConnection();

            using (var bw = new BinaryWriter(pipeServer))
            {
                // writing thread
                var writeTask = UniTask.RunOnThreadPool(() => {
                    while (IsRecording || frameQueue.Count > 0 || pendingFrameCount > 0)
                    {
                        byte[] frame = null;
                        lock (frameQueue)
                        {
                            if (frameQueue.Count > 0) frame = frameQueue.Dequeue();
                        }
                        if (frame != null)
                        {
                            bw.Write(frame);
                            bw.Flush();
                        }
                        else { Thread.Sleep(1); }
                    }
                });
                
                // recording
                while (IsRecording && !outProcess.HasExited)
                {
                    yield return new WaitForEndOfFrame();
                    
                    // audio
                    audioManager.UpdateAnswerSfx();
                    for (var i = 0; i < AudioManager.noteSfxPlaybackRequests.Length - 1; i++)
                    {
                        if (i == AudioManager.TRACK_START) continue;
                        var currentNoteTime = Majdata<TimeProvider>.Instance!.NoteTime;
                        if (i == AudioManager.TOUCHHOLD)
                        {
                            var isRequested = AudioManager.noteSfxPlaybackRequests[i];
                            if (isRequested && !isTouchHoldRising) { isTouchHoldRising = true; touchHoldStartTime = currentNoteTime; }
                            else if (!isRequested && isTouchHoldRising) { isTouchHoldRising = false; audioManager.MixSfxToBuffer(AudioManager.TOUCHHOLD, touchHoldStartTime, currentNoteTime - touchHoldStartTime); }
                        }
                        else if (AudioManager.noteSfxPlaybackRequests[i])
                        {
                            audioManager.MixSfxToBuffer(i);
                            AudioManager.noteSfxPlaybackRequests[i] = false;
                        }
                    }
                    
                    // video
                    Interlocked.Increment(ref pendingFrameCount);
                    AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32, (request) => 
                    {
                        if (!request.hasError)
                        {
                            var data = request.GetData<byte>().ToArray();
                            lock (frameQueue) { frameQueue.Enqueue(data); }
                        }
                        Interlocked.Decrement(ref pendingFrameCount);
                    });
                }
                
                yield return new WaitUntil(() => pendingFrameCount == 0 && frameQueue.Count == 0);
                yield return new WaitUntil(() => writeTask.Status.IsCompleted());
            }
            
            if (!outProcess.HasExited) outProcess.WaitForExit();

            // mux
            audioManager.ExportFinalWav(Path.Combine(maidataPath, wavName));
            
            startInfo.Arguments = muxArgs;
            var muxProcess = Process.Start(startInfo);
            muxProcess.WaitForExit();

            // camera
            camera.targetTexture = null;
            rt.Release();
            
            // output
            var outPath = Path.Combine(maidataPath, finalName);
            if (File.Exists(outPath))
            {
                errText.text = "渲染成功：" + finalName;
                Process.Start("explorer", "/select,\"" + outPath + "\"");
            }
        }
        
        timeProvider.Pause();
        bgManager.PauseVideo();
    }
}