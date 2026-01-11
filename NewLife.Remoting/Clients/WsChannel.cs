using System.Net.Sockets;
using NewLife.Data;
using NewLife.Http;
using NewLife.Log;
using NewLife.Net;

namespace NewLife.Remoting.Clients;

/// <summary>WebSocket通道</summary>
/// <remarks>
/// 基于自研轻量级WebSocket客户端实现的长连接通道。
/// 用于非NETCOREAPP平台，支持接收服务端主动下发的消息。
/// </remarks>
/// <param name="client">客户端基类实例</param>
class WsChannel(ClientBase client) : DisposeBase
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
    public virtual async Task ValidWebSocket(ApiHttpClient http)
    {
        var svc = http.Current;
        if (svc == null) return;

        // 使用过滤器内部token，因为它有过期刷新机制
        var token = http.Token;
        if (http.Filter is TokenHttpFilter thf) token = thf.Token?.AccessToken;

        var span = DefaultSpan.Current;
        span?.AppendTag($"svc={svc.Address} Token=[{token?.Length}]");

        if (token.IsNullOrEmpty()) return;

        if (_websocket != null && !_websocket.Disposed)
        {
            try
            {
                // 在websocket链路上定时发送心跳，避免长连接被断开
                await _websocket.SendTextAsync("Ping").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                span?.SetError(ex, null);
                _client.WriteLog("{0}", ex);

                _websocket.TryDispose();
                _websocket = null;
            }
        }

        if (_websocket == null || _websocket.Disposed)
        {
            var url = svc.Address.ToString().Replace("http://", "ws://").Replace("https://", "wss://");
            var uri = new Uri(new Uri(url), _client.Actions[Features.Notify]);

            using var span2 = _client.Tracer?.NewSpan("WebSocketConnect", uri + "");

            var ws = (uri.CreateRemote() as WebSocketClient)!;
            ws.SetRequestHeader("Authorization", "Bearer " + token);

            span?.AppendTag($"WebSocket.Connect {uri}");
            ws.Open();

            _websocket = ws;

            // 释放旧的CancellationTokenSource
            _source?.Cancel();
            _source.TryDispose();

            _source = new CancellationTokenSource();
            _ = Task.Factory.StartNew(() => DoPull(ws, _source), TaskCreationOptions.LongRunning);
        }
    }

    private WebSocketClient? _websocket;
    private CancellationTokenSource? _source;

    /// <summary>消息接收循环</summary>
    /// <param name="socket">WebSocket客户端</param>
    /// <param name="source">取消令牌源</param>
    /// <returns></returns>
    private async Task DoPull(WebSocketClient socket, CancellationTokenSource source)
    {
        DefaultSpan.Current = null;
        while (!source.IsCancellationRequested && !socket.Disposed)
        {
            // try-catch 放在循环内，避免单次异常退出循环
            try
            {
                using var rs = await socket.ReceiveMessageAsync(source.Token).ConfigureAwait(false);
                if (rs == null) continue;

                if (rs.Type == WebSocketMessageType.Close) break;
                if (rs.Type is WebSocketMessageType.Text or WebSocketMessageType.Binary)
                {
                    if (rs.Payload != null)
                        await OnReceive(rs.Payload, source.Token).ConfigureAwait(false);
                }
            }
            catch (ThreadAbortException) { break; }
            catch (ThreadInterruptedException) { break; }
            catch (TaskCanceledException) { }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (source.IsCancellationRequested) break;

                _client.Log?.Error("[{0}]WebSocket异常[{1}]: {2}", _client.Name, ex.GetType().Name, ex.Message);
                if (ex is SocketException) break;
            }
        }

        // 通知取消
        try
        {
            if (!source.IsCancellationRequested) source.Cancel();
        }
        catch (ObjectDisposedException) { }

        if (!socket.Disposed) await socket.CloseAsync(1000, "finish", default).ConfigureAwait(false);
    }

    /// <summary>收到服务端主动下发消息。默认转为CommandModel命令处理</summary>
    /// <param name="data">数据包</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    protected virtual async Task OnReceive(IPacket data, CancellationToken cancellationToken)
    {
        // 处理心跳消息
        if (data.Total == 4)
        {
            var msg = data.ToStr();
            if (msg == "Pong") return;
            if (msg == "Ping")
            {
                await SendTextAsync((ArrayPacket)"Pong".GetBytes(), cancellationToken).ConfigureAwait(false);
                return;
            }
        }

        await _client.HandleAsync(data, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>发送文本消息</summary>
    /// <param name="data">数据包</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public virtual Task SendTextAsync(IPacket data, CancellationToken cancellationToken = default) => _websocket!.SendTextAsync(data, default);

    /// <summary>停止WebSocket连接</summary>
    private void StopWebSocket()
    {
        // 取消接收循环
        _source?.Cancel();
        try
        {
            if (_websocket != null && !_websocket.Disposed)
                _websocket.CloseAsync(1000, "finish", default).Wait(1000);
        }
        catch { }

        // 释放资源
        _source.TryDispose();
        _source = null;
        _websocket = null;
    }
}