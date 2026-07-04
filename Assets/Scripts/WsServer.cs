#nullable enable

#region

using System;
using System.Collections.Concurrent;
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
    private CancellationToken _lifetimeCancellationToken;

    private void Awake()
    {
        _wsServer = this;
    }

    // 这里是游戏及游戏外部的初始化
    void Start()
    {
        SceneManager.LoadScene(1);

        QualitySettings.vSyncCount = 1;

        // Use port-based constructor: IL2CPP trimming removes the 'ws://' scheme
        // from System.Uri, so the URL-based constructor throws
        // ArgumentException: 'A relative URI' on Linux (works fine on Windows/.NET).
        webSocket = new WebSocketServer(8083, false);
        webSocket.AddWebSocketService<MajdataWsService>("/majdata");
        webSocket.Start();
        _lifetimeCancellationToken = this.GetCancellationTokenOnDestroy();
        ProcessQueue(_lifetimeCancellationToken).Forget();
        BroadcastHeartbeat(_lifetimeCancellationToken).Forget();

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        // 补全 Mac 常见的环境变量路径（Homebrew 在 Intel 和 Apple Silicon 的路径不同）
        var currentPath = Environment.GetEnvironmentVariable("PATH");
        var extraPath = "/usr/local/bin:/opt/homebrew/bin:/opt/homebrew/sbin";
        Environment.SetEnvironmentVariable("PATH", $"{currentPath}:{extraPath}");
#endif
    }

    private async UniTaskVoid ProcessQueue(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (MessageQueue.TryDequeue(out var json))
                {
                    while (_playManager == null ||
                           PlayManager.Summary.State is ViewStatus.Busy)
                        await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

                    Debug.Log($"dequeue: {json}");
                    await HandleMessageAsync(json);
                }
                else
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async UniTaskVoid BroadcastHeartbeat(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(1),
                    DelayType.Realtime,
                    PlayerLoopTiming.Update,
                    cancellationToken);
                Response(MajWsResponseType.Heartbeat, PlayManager.Summary);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
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

    public void Error<T>(T exception) where T : Exception
    {
        Response(MajWsResponseType.Error, exception.ToString());
    }

    public void Error(string errMsg)
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

public class MajdataWsService : WebSocketBehavior
{
    protected override void OnMessage(MessageEventArgs e)
    {
        var json = e.IsText ? e.Data : Encoding.UTF8.GetString(e.RawData);
        if (string.IsNullOrWhiteSpace(json))
            return;

        WsServer.MessageQueue.Enqueue(json);
    }
}
