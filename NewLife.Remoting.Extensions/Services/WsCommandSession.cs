using System.Net;
using System.Net.WebSockets;
using NewLife.Log;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Security;
using NewLife.Serialization;

namespace NewLife.Remoting.Extensions.Services;

/// <summary>WebSocket设备会话</summary>
public class WsCommandSession(WebSocket socket) : CommandSession
{
    /// <summary>是否活动中</summary>
    public override Boolean Active => socket != null && socket.State == WebSocketState.Open;

    /// <summary>销毁</summary>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        try
        {
            if (socket != null && socket.State == WebSocketState.Open)
                socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Dispose", default);
        }
        catch { }
        finally
        {
            socket?.Dispose();
        }
    }

    /// <summary>处理事件消息，通过WebSocket向下发送</summary>
    /// <param name="command">命令模型</param>
    /// <param name="message">原始命令消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public override Task HandleAsync(CommandModel command, String? message, CancellationToken cancellationToken)
    {
        message ??= command.ToJson();
        return socket.SendAsync(message.GetBytes(), WebSocketMessageType.Text, true, cancellationToken);
    }

    /// <summary>阻塞WebSocket，等待连接结束</summary>  
    /// <param name="context">上下文</param>
    /// <param name="span">埋点</param>  
    /// <param name="cancellationToken">取消令牌</param>  
    /// <returns></returns>  
    public virtual async Task WaitAsync(HttpContext context, ISpan? span, CancellationToken cancellationToken)
    {
        var ip = context.GetUserHost();
        var sid = Rand.Next();
        var connection = context.Connection;
        var address = connection.RemoteIpAddress ?? IPAddress.Loopback;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var remote = new IPEndPoint(address, connection.RemotePort);
        Log?.WriteLog("WebSocket连接", true, $"State={socket.State} sid={sid} Remote={remote}");

        // 长连接上线  
        SetOnline?.Invoke(true);

        // 即将进入阻塞等待，结束埋点
        span?.TryDispose();

        // 链接取消令牌。当客户端断开时，触发取消，结束长连接  
        using var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            var buf = new Byte[64];
            while (!source.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var data = await socket.ReceiveAsync(new ArraySegment<Byte>(buf), source.Token).ConfigureAwait(false);
                if (data.MessageType == WebSocketMessageType.Close) break;
                if (data.MessageType is WebSocketMessageType.Text or WebSocketMessageType.Binary)
                {
                    using var span2 = Tracer?.NewSpan("cmd:Ws.Receive", $"[{data.MessageType}]{remote}", data.Count);

                    var txt = buf.ToStr(null, 0, data.Count);
                    span2?.AppendTag(txt);
                    if (txt == "Ping")
                    {
                        // 长连接上线。可能客户端心跳已经停了，WS还在，这里重新上线  
                        SetOnline?.Invoke(true);

                        await socket.SendAsync("Pong".GetBytes(), WebSocketMessageType.Text, true, source.Token).ConfigureAwait(false);
                    }
                }
            }

            if (!source.IsCancellationRequested) source.Cancel();

            if (socket.State == WebSocketState.Open)
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "finish", default).ConfigureAwait(false);
        }
        catch (TaskCanceledException) { }
        catch (OperationCanceledException) { }
        catch (WebSocketException ex)
        {
            Log?.WriteLog("WebSocket异常", false, ex.Message);
        }
        finally
        {
            source.Cancel();
            Log?.WriteLog("WebSocket断开", true, $"State={socket.State} CloseStatus={socket.CloseStatus} sid={sid} Remote={remote}");

            // 长连接下线  
            SetOnline?.Invoke(false);
        }
    }
}
