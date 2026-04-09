#nullable enable

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ManagedBass;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using WebSocketSharp;
using WebSocketSharp.Server;

internal class WsServer: MonoBehaviour
{
    private WebSocketServer? webSocket;
    private void Awake()
    {
        Majdata<WsServer>.Instance = this;
    }

    void Start()
    {
        SceneManager.LoadScene(1);
        
        webSocket = new WebSocketServer("ws://127.0.0.1:8083");
        webSocket.AddWebSocketService<MajdataWsService>("/majdata");
        webSocket.Start();
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
    PlayManager? playManager => Majdata<PlayManager>.Instance;
    
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

    protected override async void OnMessage(MessageEventArgs e)
    {
        if (playManager is null) return;
        try
        {
            var json = string.Empty;
            if (e.IsText)
            {
                json = e.Data;
            }
            else if (e.IsBinary)
            {
                json = Encoding.UTF8.GetString(e.RawData);
            }
            else
            {
                return;
            }

            var req = JsonConvert.DeserializeObject<MajWsRequestBase>(json);
            var payloadJson = req.requestData?.ToString() ?? string.Empty;
            switch (req.requestType)
            {
                case MajWsRequestType.Setting:
                {
                    var payload = JsonConvert.DeserializeObject<MajWsRequestSetting>(payloadJson);
                    playManager.SyncSetting(payload.ViewSetting);
                    Response(MajWsResponseType.LoadOk, PlayManager.Summary);
                }
                    break;
                case MajWsRequestType.Load:
                {
                    var payload = JsonConvert.DeserializeObject<MajWsRequestLoad>(payloadJson);
                    await playManager.LoadAsync(payload.TrackPath, payload.ImagePath, payload.VideoPath);
                    Response(MajWsResponseType.LoadOk, PlayManager.Summary);
                }
                    break;
                case MajWsRequestType.Play:
                {
                    var payload = JsonConvert.DeserializeObject<MajWsRequestPlay>(payloadJson);
                    await playManager.PlayAsync(payload.Mode, payload.StartAt, payload.Offset, payload.Speed, 
                        payload.SimaiFumen, payload.Title, payload.Artist, payload.Difficulty, payload.MaidataPath);
                    Response(MajWsResponseType.PlayStarted, PlayManager.Summary);
                }
                    break;
                case MajWsRequestType.Resume:
                {
                    await playManager.ResumeAsync();
                    Response(MajWsResponseType.PlayResumed, PlayManager.Summary);
                }
                    break;
                case MajWsRequestType.Pause:
                {
                    await playManager.PauseAsync();
                    Response(MajWsResponseType.PlayPaused, PlayManager.Summary);
                }
                    break;
                case MajWsRequestType.Stop:
                {
                    await playManager.StopAsync();
                    Response(MajWsResponseType.PlayStopped, PlayManager.Summary);
                }
                    break;
                
                //TODO: Status
                case MajWsRequestType.State:
                {
                    Response(MajWsResponseType.Ok, PlayManager.Summary);
                }
                    break;
                default:
                    Error("Not Supported");
                    break;
            }
        }
        catch (Exception ex)
        {
            if (Bass.LastError is not (Errors.Empty or Errors.OK))
            {
                Error(Bass.LastError.ToString());
                return;
            }
            
            Error(ex);
            throw;
        }
    }

    void Error<T>(T exception) where T : Exception
    {
        Response(MajWsResponseType.Error, exception.ToString());
    }

    void Error(string errMsg)
    {
        Response(MajWsResponseType.Error, errMsg);
    }

    void Response(MajWsResponseType type = MajWsResponseType.Ok, object? data = null)
    {
        var rsp = new MajWsResponseBase()
        {
            responseType = type,
            responseData = data
        };
        Send(JsonConvert.SerializeObject(rsp));
    }
}