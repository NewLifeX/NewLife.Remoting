#if NETCOREAPP
using System.Net.Sockets;
using System.Net.WebSockets;
using NewLife;
using NewLife.Collections;
using NewLife.Data;
using NewLife.Log;
using WebSocket = System.Net.WebSockets.WebSocket;
using WebSocketMessageType = System.Net.WebSockets.WebSocketMessageType;

namespace NewLife.Remoting.Clients;

/// <summary>WebSocket通道（NetCore版本）</summary>
/// <remarks>
/// 基于System.Net.WebSockets.ClientWebSocket实现的长连接通道。
/// 仅用于NETCOREAPP平台，提供更好的性能和稳定性。
/// 由 ClientBase.OnPing 周期性驱动 ValidWebSocket 以维持连接和检测超时。
/// </remarks>
/// <param name="client">客户端基类实例</param>
class WsChannelCore(ClientBase client) : WsChannel(client)
{
    /// <summary>WebSocket接收缓冲区大小。默认64k</summary>
    public Int32 BufferSize { get; set; } = 64 * 1024;

    /// <summary>单条消息最大字节数。默认16M，超过后断开连接，避免分片聚合被用于耗尽内存；0 表示不限制</summary>
    public Int32 MaxMessageSize { get; set; } = 16 * 1024 * 1024;

    private readonly ClientBase _client = client;

    /// <summary>是否已连接</summary>
    public override Boolean Active => _websocket != null && _websocket.State == WebSocketState.Open;

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

                // 记录发送时间，用于超时兜底。_lastPongTime 只在收到 Pong 时更新，此处不碰它
                _lastPingTime = Runtime.TickCount64;

                // 首次发送 Ping 时初始化超时计时，确保即使服务端不回复也能在 3 周期后触发重连
                if (_lastPongTime == 0) _lastPongTime = _lastPingTime;
            }
            catch (Exception ex)
            {
                span?.SetError(ex, null);
                _client.WriteLog("{0}", ex.ToString());

                _websocket.TryDispose();
                _websocket = null;
            }
        }

        // 检查心跳响应——如果连续3个周期未收到Pong，判定为死连接，强制重连
        // 确保断连判断周期至少180秒，避免服务端 KeepAlive(60s) 与客户端心跳周期叠加导致频繁误判
        var pingPeriod = Math.Max(_client.PingPeriod, 60_000);
        if (_websocket != null && _websocket.State == WebSocketState.Open &&
            _lastPongTime > 0 &&
            Runtime.TickCount64 - _lastPongTime > pingPeriod * 3)
        {
            _client.Log?.InfoPrefixed("[" + _client.Name + "]", "WebSocket heartbeat timed out, forcing reconnect");

            _websocket.TryDispose();
            _websocket = null;
        }

        if (_websocket == null || _websocket.State != WebSocketState.Open)
        {
            // 确保创建新连接前彻底清理旧资源
            DisposeOldSocket();

            var url = svc.Address.ToString().Replace("http://", "ws://").Replace("https://", "wss://");
            var uri = new Uri(new Uri(url), _client.Actions[Features.Notify]);

            using var span2 = _client.Tracer?.NewSpan("WebSocketConnect", uri + "");

            var ws = new ClientWebSocket();
            ws.Options.SetRequestHeader("Authorization", "Bearer " + token);
            ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

            span?.AppendTag($"WebSocket.Connect {uri}");

            // 限制建连超时，避免服务端无响应时无限挂起
            using var cts = new CancellationTokenSource(_client.Timeout);
            await ws.ConnectAsync(uri, cts.Token).ConfigureAwait(false);

            _websocket = ws;

            // 新连接重置Pong计时，避免旧连接的残留时间导致误判心跳超时
            _lastPongTime = 0;

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
        var buf = Pool.Shared.Rent(BufferSize);

        // 捕获 Close 帧的状态码，用于回显
        WebSocketCloseStatus? closeStatus = null;
        String? closeReason = null;

        try
        {
            while (!source.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                // try-catch 放在循环内，避免单次异常退出循环
                try
                {
                    var data = await socket.ReceiveAsync(new ArraySegment<Byte>(buf), source.Token).ConfigureAwait(false);
                    if (data.MessageType == WebSocketMessageType.Close)
                    {
                        closeStatus = data.CloseStatus ?? WebSocketCloseStatus.NormalClosure;
                        closeReason = data.CloseStatusDescription ?? "finish";
                        break;
                    }
                    if (data.MessageType is WebSocketMessageType.Text or WebSocketMessageType.Binary)
                    {
                        // 一条消息可能被拆成多片，必须拼到 EndOfMessage 才能当完整消息解析
                        var pk = await ReadMessageAsync(socket, buf, data, source.Token).ConfigureAwait(false);
                        if (pk == null) break;

                        // 检查是否为WebSocket文本心跳 Ping/Pong
                        if (pk.Total == 4)
                        {
                            var msg = pk.ToStr();
                            if (msg == "Ping")
                            {
                                // 自动回复 Pong，维持 WebSocket 心跳
                                await SendTextAsync((ArrayPacket)"Pong".GetBytes(), source.Token).ConfigureAwait(false);

                                await OnPing(pk, source.Token).ConfigureAwait(false);
                                continue;
                            }
                            if (msg == "Pong")
                            {
                                await OnPing(pk, source.Token).ConfigureAwait(false);
                                continue;
                            }
                        }

                        await OnReceive(pk, source.Token).ConfigureAwait(false);
                    }
                }
                catch (ThreadAbortException) { break; }
                catch (ThreadInterruptedException) { break; }
                catch (TaskCanceledException) { }
                catch (OperationCanceledException) { }
                catch (ObjectDisposedException) { break; }
                catch (WebSocketException wex) when (
                    wex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely ||
                    socket.State == WebSocketState.Aborted)
                {
                    _client.Log?.WritePrefixed(LogLevel.Debug, "[" + _client.Name + "]", "WebSocket connection closed [{0}]", wex.WebSocketErrorCode);
                    break;
                }
                catch (SocketException sex) when (IsNormalDisconnect(sex.SocketErrorCode))
                {
                    _client.Log?.WritePrefixed(LogLevel.Debug, "[" + _client.Name + "]", "WebSocket connection closed [{0}]", sex.SocketErrorCode);
                    break;
                }
                catch (Exception ex)
                {
                    if (source.IsCancellationRequested) break;

                    if (ex is not WebSocketException || socket.State != WebSocketState.Aborted)
                        _client.Log?.ErrorPrefixed("[" + _client.Name + "]", "WebSocket error [{0}]: {1}", ex.GetType().Name, ex.Message);
                    if (ex is WebSocketException) break;
                }
            }
        }
        finally
        {
            Pool.Shared.Return(buf);
        }

        // 通知取消
        try
        {
            if (!source.IsCancellationRequested) source.Cancel();
        }
        catch (ObjectDisposedException) { }

        if (socket.State == WebSocketState.Open)
            await socket.CloseAsync(closeStatus ?? WebSocketCloseStatus.NormalClosure, closeReason ?? "finish", default).ConfigureAwait(false);
    }

    /// <summary>读取一条完整消息，必要时聚合分片</summary>
    /// <remarks>
    /// 一条逻辑消息可能被拆成多帧（对端缓冲小、中间设备分片、文本含多字节字符等），
    /// 只取第一帧会得到截断数据，反序列化随之失败；这里一直读到 EndOfMessage 为止。
    /// </remarks>
    private async Task<IPacket?> ReadMessageAsync(WebSocket socket, Byte[] buf, WebSocketReceiveResult first, CancellationToken cancellationToken)
    {
        if (first.MessageType == WebSocketMessageType.Close) return null;
        if (first.EndOfMessage) return new ArrayPacket(buf, 0, first.Count);

        using var ms = new MemoryStream(first.Count * 2);
        ms.Write(buf, 0, first.Count);

        var total = first.Count;
        var data = first;
        while (!data.EndOfMessage)
        {
            data = await socket.ReceiveAsync(new ArraySegment<Byte>(buf), cancellationToken).ConfigureAwait(false);
            if (data.MessageType == WebSocketMessageType.Close) return null;

            total += data.Count;
            if (MaxMessageSize > 0 && total > MaxMessageSize)
            {
                _client.Log?.WritePrefixed(LogLevel.Error, "[" + _client.Name + "]", "WS_MSG_TOO_LARGE {0}/{1}", total, MaxMessageSize);
                return null;
            }

            ms.Write(buf, 0, data.Count);
        }

        return new ArrayPacket(ms.ToArray());
    }

    /// <summary>发送文本消息</summary>
    /// <param name="data">数据包</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public override Task SendTextAsync(IPacket data, CancellationToken cancellationToken = default) => _websocket!.SendAsync(data.ToSegment(), WebSocketMessageType.Text, true, cancellationToken);

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