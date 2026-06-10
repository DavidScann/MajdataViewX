#nullable enable

#region

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;
using WebSocketSharp;
using WebSocketSharp.Server;
using Debug = UnityEngine.Debug;

using static MajCtx;

#endregion

public class WsServer : MonoBehaviour
{
    public static readonly ConcurrentQueue<string> MessageQueue = new();
    private WebSocketServer? webSocket;

    private void Awake()
    {
        _wsServer = this;
    }

    // 这里是游戏及游戏外部的初始化
    void Start()
    {
        SceneManager.LoadScene(1);

        webSocket = new WebSocketServer("ws://127.0.0.1:8083");
        webSocket.AddWebSocketService<MajdataWsService>("/majdata");
        webSocket.Start();
        ProcessQueue().Forget();

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        // 补全 Mac 常见的环境变量路径（Homebrew 在 Intel 和 Apple Silicon 的路径不同）
        var currentPath = Environment.GetEnvironmentVariable("PATH");
        var extraPath = "/usr/local/bin:/opt/homebrew/bin:/opt/homebrew/sbin";
        Environment.SetEnvironmentVariable("PATH", $"{currentPath}:{extraPath}");
#endif
    }

    private async UniTaskVoid ProcessQueue()
    {
        while (this != null)
        {
            if (MessageQueue.TryDequeue(out var json))
            {
                while (_playManager == null)
                    await UniTask.Yield();

                Debug.Log($"dequeue: {json}");
                await HandleMessageAsync(json);
            }
            else
            {
                await UniTask.Yield();
            }
        }
    }

    private async UniTask HandleMessageAsync(string json)
    {
        try
        {
            var req = JsonConvert.DeserializeObject<MajWsRequestBase>(json);
            var payloadJson = req.requestData?.ToString() ?? string.Empty;
            switch (req.requestType)
            {
                case MajWsRequestType.Setting:
                    {
                        var payload = JsonConvert.DeserializeObject<MajWsRequestSetting>(payloadJson);
                        _playManager.Setting(payload.ViewSetting, payload.VolumeSetting);
                        Response(MajWsResponseType.Ok, PlayManager.Summary);
                        Debug.Log("dequeued: Setting");
                    }
                    break;
                case MajWsRequestType.Load:
                    {
                        var payload = JsonConvert.DeserializeObject<MajWsRequestLoad>(payloadJson);
                        await _playManager.LoadAsync(payload.TrackPath, payload.ImagePath, payload.VideoPath);
                        Response(MajWsResponseType.LoadOk, PlayManager.Summary);
                        Debug.Log("dequeued: Load");
                    }
                    break;
                case MajWsRequestType.Play:
                    {
                        var payload = JsonConvert.DeserializeObject<MajWsRequestPlay>(payloadJson);
                        await _playManager.PlayAsync(payload.Mode,
                            payload.StartAt, payload.Speed,
                            payload.Title, payload.Artist, payload.Offset,
                            payload.Designer, payload.Level, payload.Fumen,
                            payload.Commands, payload.Difficulty, payload.MaidataPath);
                        if (payload.Mode != PlaybackMode.Record)
                            Response(MajWsResponseType.PlayStarted, PlayManager.Summary);
                        Debug.Log("dequeued: Play");
                    }
                    break;
                case MajWsRequestType.Resume:
                    {
                        if (_screenRecorder.IsRecording) return;
                        await _playManager.ResumeAsync();
                        Response(MajWsResponseType.PlayResumed, PlayManager.Summary);
                        Debug.Log("dequeued: Resume");
                    }
                    break;
                case MajWsRequestType.Pause:
                    {
                        if (_screenRecorder.IsRecording) return;
                        await _playManager.PauseAsync();
                        Response(MajWsResponseType.PlayPaused, PlayManager.Summary);
                        Debug.Log("dequeued: Pause");
                    }
                    break;
                case MajWsRequestType.Stop:
                    {
                        await _playManager.StopAsync();
                        Response(MajWsResponseType.PlayStopped, PlayManager.Summary);
                        Debug.Log("dequeued: Stop");
                    }
                    break;
                case MajWsRequestType.State:
                    {
                        Response(MajWsResponseType.Ok, PlayManager.Summary);
                        Debug.Log("dequeued: State");
                    }
                    break;
                default:
                    Error("Not Supported");
                    Debug.LogError("dequeue: Not Supported");
                    break;
            }
        }
        catch (Exception ex)
        {
            Error(ex);
            throw;
        }
    }

    // for self stopping without request
    public void SendStopResponse()
    {
        Response(MajWsResponseType.PlayStopped, PlayManager.Summary);
    }

    private void Response(MajWsResponseType type, object? data = null)
    {
        var rsp = new MajWsResponseBase
        {
            responseType = type,
            responseData = data ?? PlayManager.Summary
        };
        webSocket?.WebSocketServices["/majdata"].Sessions.
            Broadcast(JsonConvert.SerializeObject(rsp));
    }

    void Error<T>(T exception) where T : Exception
    {
        Response(MajWsResponseType.Error, exception.ToString());
    }

    void Error(string errMsg)
    {
        Response(MajWsResponseType.Error, errMsg);
    }

    void OnDestroy()
    {
        if (webSocket is not null)
        {
            webSocket.RemoveWebSocketService("/majdata");
            webSocket.Stop();
        }
    }
}

public class MajdataWsService : WebSocketBehavior, IDisposable
{
    public MajdataWsService()
    {
        _ = UniTask.RunOnThreadPool(() =>
        {
            while (true)
            {
                try
                {
                    if (Sessions is null)
                        continue;
                    var json = GetSummaryJson();
                    Sessions.Broadcast(json);
                }
                catch (InvalidOperationException)
                {
                }
                finally
                {
                    Thread.Sleep(1000);
                }
            }

        });
    }

    private static string GetSummaryJson()
    {
        var rsp = new MajWsResponseBase()
        {
            responseType = MajWsResponseType.Heartbeat,
            responseData = PlayManager.Summary
        };
        var json = JsonConvert.SerializeObject(rsp);
        return json;
    }

    public void Dispose()
    {
    }

    protected override void OnMessage(MessageEventArgs e)
    {
        var json = e.IsText ? e.Data : Encoding.UTF8.GetString(e.RawData);
        if (string.IsNullOrWhiteSpace(json))
            return;

        WsServer.MessageQueue.Enqueue(json);
    }
}