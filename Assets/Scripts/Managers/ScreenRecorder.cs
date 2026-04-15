using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using UnityEngine;
using UnityEngine.UI;

public class ScreenRecorder : MonoBehaviour
{
    public GameObject APObj;
    DataLoader loader;
    ObjectCounter counter;

    private bool isRecording;

    private void Awake()
    {
        Majdata<ScreenRecorder>.Instance = this;
    }

    private void Start()
    {
        loader = Majdata<DataLoader>.Instance!;
        counter = Majdata<ObjectCounter>.Instance!;
    }
    
    private void Update()
    {
        if(isRecording)
        {
            if (PlayManager.Summary.State is not ViewStatus.Playing)
                return;
            if(counter.AllFinished && APObj == null)
                isRecording = false;
        }
    }

    public void StartRecording(string maidata_path, int fps)
    {
        StartCoroutine(CaptureScreen(maidata_path, fps));
    }

    public void StopRecording()
    {
        print("stop recording");
        isRecording = false;
    }

    private IEnumerator CaptureScreen(string maidata_path, int fps)
    {
        var timeProvider = Majdata<TimeProvider>.Instance!;
        var bgManager = Majdata<BgManager>.Instance!;
        var errText = GameObject.Find("ErrText").GetComponent<Text>();
        
        if (Screen.width % 2 != 0 || Screen.height % 2 != 0)
        {
            errText.text =
                "无法开始编码，因为分辨率宽度或高度不是偶数。\nCan not start render because the width/height is not even.\n当前分辨率:" +
                Screen.width + "x" + Screen.height + "\n";
            yield break;
        }

        if (File.Exists(maidata_path + "\\out.mp4"))
            File.Delete(maidata_path + "\\out.mp4");
        
        if (!File.Exists(maidata_path + "\\out.wav"))
        {
            errText.text =
                "无法开始编码，因为没有out.wav文件。\nCan not start render because out.wav not found.\n当前分辨率:" +
                Screen.width + "x" + Screen.height + "\n";
            yield break;
        } //TODO: Render Sound Effect
        
        byte[] data;
        var texture = new Texture2D(0, 0);
        using (var pipeServer = new NamedPipeServerStream("majdataRec", PipeDirection.Out))
        {
            var wavpath = "out.wav";
            var outputfile = "out.mp4";
            
            var arguments =
                $"-hide_banner -y " +
                $"-thread_queue_size 512 " +
                $"-f rawvideo -pix_fmt rgba -s {Screen.width}x{Screen.height} -framerate {fps} " +
                $"-i \\\\.\\pipe\\majdataRec " +
                $"-thread_queue_size 512 " +
                $"-i {wavpath} " +
                $"-vf vflip " +
                $"-c:v libx264 -preset fast -pix_fmt yuv420p " +
                $"-c:a aac -b:a 320k " +
                $"-shortest " +
                outputfile;
            var startInfo = new ProcessStartInfo(Application.streamingAssetsPath + "\\ffmpeg.exe", arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = maidata_path
            };
            startInfo.EnvironmentVariables.Add("FFREPORT", "file=out.log:level=24");
            print(arguments);
            
            var p = Process.Start(startInfo);
            pipeServer.WaitForConnection();
            isRecording = true;
            using (var bw = new BinaryWriter(pipeServer))
            {
                do
                {
                    yield return new WaitForEndOfFrame();
                    try
                    {
                        texture.Reinitialize(0, 0);
                        texture = ScreenCapture.CaptureScreenshotAsTexture();

                        data = texture.GetRawTextureData();

                        bw.Write(data, 0, data.Length);
                        bw.Flush();
                    }
                    catch (Exception e)
                    {
                        errText.text += e.Message;
                    }
                } while (
                    pipeServer.IsConnected &&
                    isRecording &&
                    !p!.HasExited
                );
            }

            p.WaitForExit();

            if (File.Exists(maidata_path + "/out.mp4") && p.ExitCode == 0)
            {
                errText.text += "渲染成功，视频生成在" + maidata_path 
                                + "\\out.mp4\nRender Successed\nExitCode:" + p.ExitCode;
                Process.Start("explorer", "/select,\"" + maidata_path + "\\out.mp4" + "\"");
            }
            else
            {
                errText.text +=
                    "编码器已退出\nFFmpeg Exited.\nExitCode:" + p.ExitCode;
            }
        }

        timeProvider.Pause();
        bgManager.PauseVideo();
    }
}