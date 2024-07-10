#if NETCOREAPP
using System.Net.WebSockets;
using NewLife.Http;
using NewLife.Log;
using NewLife.Remoting.Models;
using NewLife.Serialization;
using WebSocket = System.Net.WebSockets.WebSocket;
using WebSocketMessageType = System.Net.WebSockets.WebSocketMessageType;

namespace NewLife.Remoting.Clients;

/// <summary>WebSocket</summary>
class WsChannelCore : WsChannel
{
    private readonly ClientBase _client;
    public WsChannelCore(ClientBase client) : base(client) => _client = client;

    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        StopWebSocket();
    }

    public override async Task ValidWebSocket(ApiHttpClient http)
    {
        var svc = http.Current;
        if (svc == null) return;

        // 使用过滤器内部token，因为它有过期刷新机制
        var token = http.Token;
        if (http.Filter is NewLife.Http.TokenHttpFilter thf) token = thf.Token?.AccessToken;

        var span = DefaultSpan.Current;
        span?.AppendTag($"svc={svc.Address} Token=[{token?.Length}]");

        if (token.IsNullOrEmpty()) return;

        if (_websocket != null && _websocket.State == WebSocketState.Open)
        {
            try
            {
                // 在websocket链路上定时发送心跳，避免长连接被断开
                var str = "Ping";
                await _websocket.SendAsync(new ArraySegment<Byte>(str.GetBytes()), WebSocketMessageType.Text, true, default);
            }
            catch (Exception ex)
            {
                span?.SetError(ex, null);
                _client.WriteLog("{0}", ex);

                _websocket.TryDispose();
                _websocket = null;
            }
        }

        if (_websocket == null || _websocket.State != WebSocketState.Open)
        {
            var url = svc.Address.ToString().Replace("http://", "ws://").Replace("https://", "wss://");
            var uri = new Uri(new Uri(url), _client.Actions[Features.Notify]);

            using var span2 = _client.Tracer?.NewSpan("WebSocketConnect", uri + "");

            var client = new ClientWebSocket();
            client.Options.SetRequestHeader("Authorization", "Bearer " + token);

            span?.AppendTag($"WebSocket.Connect {uri}");
            await client.ConnectAsync(uri, default);

            _websocket = client;

            _source = new CancellationTokenSource();
            _ = Task.Run(() => DoPull(client, _source));
        }
    }

    private WebSocket? _websocket;
    private CancellationTokenSource? _source;
    private async Task DoPull(WebSocket socket, CancellationTokenSource source)
    {
        DefaultSpan.Current = null;
        try
        {
            var buf = new Byte[64 * 1024];
            while (!source.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var data = await socket.ReceiveAsync(new ArraySegment<Byte>(buf), source.Token);
                if (data.MessageType == WebSocketMessageType.Close) break;
                if (data.MessageType == WebSocketMessageType.Text)
                {
                    var txt = buf.ToStr(null, 0, data.Count);
                    await OnReceive(txt);
                }
            }

            if (!source.IsCancellationRequested) source.Cancel();

            if (socket.State == WebSocketState.Open)
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "finish", default);
        }
        catch (TaskCanceledException) { }
        catch (OperationCanceledException) { }
        catch (WebSocketException ex)
        {
            _client.Log?.Error("WebSocket异常 {0}", ex.Message);
        }
        finally
        {
            source.Cancel();
        }
    }

    /// <summary>收到服务端主动下发消息。默认转为CommandModel命令处理</summary>
    /// <param name="message"></param>
    /// <returns></returns>
    private async Task OnReceive(String message)
    {
        if (message.StartsWithIgnoreCase("Pong"))
        {
        }
        else
        {
            var model = _client.JsonHost.Read(message, typeof(CommandModel)) as CommandModel;
            if (model != null) await _client.ReceiveCommand(model, "WebSocket");
        }
    }

    private void StopWebSocket()
    {
        _source?.Cancel();
        try
        {
            if (_websocket != null && _websocket.State == WebSocketState.Open)
                _websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "finish", default);
        }
        catch { }

        _websocket = null;
    }
}
#endif