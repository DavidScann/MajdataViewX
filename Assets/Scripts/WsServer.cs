#nullable enable

using Cysharp.Threading.Tasks;
using MajdataViewX.Managers;
using MajdataViewX.Types.Enums;
using MajdataViewX.Types.MajWs;
using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Server;
using static MajdataViewX.Base.MajCtx;
using Debug = UnityEngine.Debug;

namespace MajdataViewX
{
    public class WsServer : MonoBehaviour
    {
        public static readonly ConcurrentQueue<string> MessageQueue = new();
        private WebSocketServer? _webSocket;
        private CancellationToken _lifetimeCancellationToken;

        private void Awake()
        {
            _wsServer = this;
        }

        // 这里是游戏及游戏外部的初始化
        void Start()
        {
            QualitySettings.vSyncCount = 1;

            // Use port-based constructor: IL2CPP trimming removes the 'ws://' scheme
            // from System.Uri, so the URL-based constructor throws
            // ArgumentException: 'A relative URI' on Linux (works fine on Windows/.NET).
            _webSocket = new WebSocketServer(WsProtocol.PORT, false);
            _webSocket.AddWebSocketService<MajdataWsService>(WsProtocol.SERVICE_PATH);
            _webSocket.Start();
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
                               PlayManager.Summary.State == ViewStatus.Busy)
                            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

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

        private async Task HandleMessageAsync(string json)
        {
            try
            {
                var req = WsJson.Deserialize<MajWsRequest>(json);
                if (req is null)
                {
                    Error("empty or invalid json");
                    Debug.LogError($"request failed: invalid json");
                    return;
                }

                switch (req.Type)
                {
                    case MajWsRequestType.Setting:
                        _playManager.Setting(req.ViewSetting ?? new(), req.VolumeSetting ?? new());
                        Response(MajWsResponseType.Ok, PlayManager.Summary);
                        Debug.Log("request finished: Setting");
                        break;
                    case MajWsRequestType.Load:
                        await _playManager.LoadAsync(
                            req.TrackPath ?? string.Empty,
                            req.ImagePath ?? string.Empty,
                            req.VideoPath);
                        Response(MajWsResponseType.LoadOk, PlayManager.Summary);
                        Debug.Log("request finished: Load");
                        break;
                    case MajWsRequestType.Update:
                        await _playManager.UpdateAsync(
                            req.ChartText,
                            req.SelectedDifficulty,
                            req.Title ?? string.Empty,
                            req.Artist ?? string.Empty,
                            req.Level ?? string.Empty,
                            req.Designer ?? string.Empty,
                            req.Offset,
                            req.ClockCount);
                        Response(MajWsResponseType.Ok, PlayManager.Summary);
                        Debug.Log("request finished: Update");
                        break;
                    case MajWsRequestType.Play:
                        await _playManager.PlayAsync(
                            req.PlayMode, req.StartAt, req.Speed, req.MaidataPath ?? string.Empty);
                        if (req.PlayMode != PlaybackMode.Record)
                            Response(MajWsResponseType.PlayStarted, PlayManager.Summary);
                        Debug.Log("request finished: Play");
                        break;
                    case MajWsRequestType.Pause:
                        if (_screenRecorder.IsRecording) break;
                        await _playManager.PauseAsync();
                        Response(MajWsResponseType.PlayPaused, PlayManager.Summary);
                        Debug.Log("request finished: Pause");
                        break;
                    case MajWsRequestType.Stop:
                        await _playManager.StopAsync();
                        Response(MajWsResponseType.PlayStopped, PlayManager.Summary);
                        Debug.Log("request finished: Stop");
                        break;
                    case MajWsRequestType.State:
                        Response(MajWsResponseType.Ok, PlayManager.Summary);
                        Debug.Log("request finished: State");
                        break;
                    default:
                        Error("Not Supported");
                        Debug.LogError("request failed: Not Supported");
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

        private void Response(MajWsResponseType type, ViewSummary? summary = null, string? error = null)
        {
            var rsp = new MajWsResponse
            {
                ResponseType = type,
                Summary = summary ?? PlayManager.Summary,
                Error = error
            };
            _webSocket?.WebSocketServices[WsProtocol.SERVICE_PATH].Sessions.
                Broadcast(WsJson.SerializeToUtf8(rsp));
        }

        public void Error<T>(T exception) where T : Exception
        {
            Response(MajWsResponseType.Error, error: exception.ToString());
        }

        public void Error(string errMsg)
        {
            Response(MajWsResponseType.Error, error: errMsg);
        }

        private void OnDestroy()
        {
            if (_webSocket is null) return;
            _webSocket.RemoveWebSocketService(WsProtocol.SERVICE_PATH);
            _webSocket.Stop();
        }
    }

    public class MajdataWsService : WebSocketBehavior
    {
        protected override void OnMessage(MessageEventArgs e)
        {
            // 文本帧为 JSON 请求；二进制帧按 UTF-8 解码为 JSON（兼容老客户端误发二进制的情况）
            var json = e.IsBinary ? Encoding.UTF8.GetString(e.RawData) : e.Data;
            if (string.IsNullOrEmpty(json))
                return;

            WsServer.MessageQueue.Enqueue(json);
        }
    }
}
