using NewLife.Log;
using NewLife.Net;
using NewLife.Remoting.Models;
using NewLife.Serialization;

namespace NewLife.Remoting.Clients;

/// <summary>WebSocket</summary>
class WsChannel : DisposeBase
{
    private readonly ClientBase _client;

    public WsChannel(ClientBase client) => _client = client;

    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        StopWebSocket();
    }

    public virtual async Task ValidWebSocket(ApiHttpClient http)
    {
        var svc = http.Current;
        if (svc == null) return;

        // 使用过滤器内部token，因为它有过期刷新机制
        var token = http.Token;
        if (http.Filter is NewLife.Http.TokenHttpFilter thf) token = thf.Token?.AccessToken;

        var span = DefaultSpan.Current;
        span?.AppendTag($"svc={svc.Address} Token=[{token?.Length}]");

        if (token.IsNullOrEmpty()) return;

        if (_websocket != null && !_websocket.Disposed)
        {
            try
            {
                // 在websocket链路上定时发送心跳，避免长连接被断开
                await _websocket.SendTextAsync("Ping");
            }
            catch (Exception ex)
            {
                span?.SetError(ex, null);
                _client.WriteLog("{0}", ex);
            }
        }

        if (_websocket == null || _websocket.Disposed)
        {
            var url = svc.Address.ToString().Replace("http://", "ws://").Replace("https://", "wss://");
            var uri = new Uri(new Uri(url), "/Device/Notify");

            using var span2 = _client.Tracer?.NewSpan("WebSocketConnect", uri + "");

            var client = uri.CreateRemote() as WebSocketClient;
            //todo 设置令牌
            //client.Options.SetRequestHeader("Authorization", "Bearer " + token);

            span?.AppendTag($"WebSocket.Connect {uri}");
            //await client.ConnectAsync(uri, default);
            client.Open();

            _websocket = client;

            _source = new CancellationTokenSource();
            _ = Task.Run(() => DoPull(client, _source.Token));
        }
    }

    private WebSocketClient? _websocket;
    private CancellationTokenSource? _source;
    private async Task DoPull(WebSocketClient socket, CancellationToken cancellationToken)
    {
        DefaultSpan.Current = null;
        try
        {
            var buf = new Byte[4 * 1024];
            while (!cancellationToken.IsCancellationRequested && !socket.Disposed)
            {
                var rs = await socket.ReceiveMessageAsync(cancellationToken);
                var txt = rs.Payload.ToStr();
                await OnReceive(txt);
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }

        if (!socket.Disposed) await socket.CloseAsync(1000, "finish", default);
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
            var model = message.ToJsonEntity<CommandModel>();
            if (model != null) await _client.ReceiveCommand(model, "WebSocket");
        }
    }

    private void StopWebSocket()
    {
#if NETCOREAPP
        _source?.Cancel();
        try
        {
            if (_websocket != null && !_websocket.Disposed)
                _websocket.CloseAsync(1000, "finish", default);
        }
        catch { }

        _websocket = null;
#endif
    }
}