#if NETCOREAPP
using System.Net.WebSockets;
using NewLife.Data;
using NewLife.Log;
using WebSocket = System.Net.WebSockets.WebSocket;
using WebSocketMessageType = System.Net.WebSockets.WebSocketMessageType;

namespace NewLife.Remoting.Clients;

/// <summary>WebSocket通道（NetCore版本）</summary>
/// <remarks>
/// 基于System.Net.WebSockets.ClientWebSocket实现的长连接通道。
/// 仅用于NETCOREAPP平台，提供更好的性能和稳定性。
/// </remarks>
/// <param name="client">客户端基类实例</param>
class WsChannelCore(ClientBase client) : WsChannel(client)
{
    private readonly ClientBase _client = client;

    /// <summary>销毁资源</summary>
    /// <param name="disposing">是否释放托管资源</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        StopWebSocket();
    }

    /// <summary>验证并维护WebSocket连接</summary>
    /// <remarks>
    /// 检查WebSocket连接状态，若已连接则发送心跳保活。
    /// 若未连接或已断开，则重新建立连接并启动消息接收循环。
    /// </remarks>
    /// <param name="http">Http客户端</param>
    /// <returns></returns>
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
                await _websocket.SendAsync(new ArraySegment<Byte>(str.GetBytes()), WebSocketMessageType.Text, true, default).ConfigureAwait(false);
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

            var ws = new ClientWebSocket();
            ws.Options.SetRequestHeader("Authorization", "Bearer " + token);

            span?.AppendTag($"WebSocket.Connect {uri}");
            await ws.ConnectAsync(uri, default).ConfigureAwait(false);

            _websocket = ws;

            // 释放旧的CancellationTokenSource
            _source?.Cancel();
            _source.TryDispose();

            _source = new CancellationTokenSource();
            _ = Task.Factory.StartNew(() => DoPull(ws, _source), TaskCreationOptions.LongRunning);
        }
    }

    private WebSocket? _websocket;
    private CancellationTokenSource? _source;

    /// <summary>消息接收循环</summary>
    /// <param name="socket">WebSocket连接</param>
    /// <param name="source">取消令牌源</param>
    /// <returns></returns>
    private async Task DoPull(WebSocket socket, CancellationTokenSource source)
    {
        DefaultSpan.Current = null;
        var buf = new Byte[64 * 1024];
        while (!source.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            // try-catch 放在循环内，避免单次异常退出循环
            try
            {
                var data = await socket.ReceiveAsync(new ArraySegment<Byte>(buf), source.Token).ConfigureAwait(false);
                if (data.MessageType == WebSocketMessageType.Close) break;
                if (data.MessageType is WebSocketMessageType.Text or WebSocketMessageType.Binary)
                {
                    var pk = new ArrayPacket(buf, 0, data.Count);
                    await OnReceive(pk, source.Token).ConfigureAwait(false);
                }
            }
            catch (ThreadAbortException) { break; }
            catch (ThreadInterruptedException) { break; }
            catch (TaskCanceledException) { }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (source.IsCancellationRequested) break;

                if (ex is not WebSocketException || socket.State != WebSocketState.Aborted)
                    _client.Log?.Error("[{0}]WebSocket异常[{1}]: {2}", _client.Name, ex.GetType().Name, ex.Message);
                if (ex is WebSocketException) break;
            }
        }

        // 通知取消
        try
        {
            if (!source.IsCancellationRequested) source.Cancel();
        }
        catch (ObjectDisposedException) { }

        if (socket.State == WebSocketState.Open)
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "finish", default).ConfigureAwait(false);
    }

    /// <summary>发送文本消息</summary>
    /// <param name="data">数据包</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public override Task SendTextAsync(IPacket data, CancellationToken cancellationToken = default) => _websocket!.SendAsync(data.ToSegment(), WebSocketMessageType.Text, true, default);

    /// <summary>停止WebSocket连接</summary>
    private void StopWebSocket()
    {
        // 取消接收循环
        _source?.Cancel();
        try
        {
            // 等待关闭完成
            if (_websocket != null && _websocket.State == WebSocketState.Open)
                _websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "finish", default).Wait(1000);
        }
        catch { }

        // 释放资源
        _source.TryDispose();
        _source = null;
        _websocket.TryDispose();
        _websocket = null;
    }
}
#endif