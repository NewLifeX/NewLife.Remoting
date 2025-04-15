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

    /// <summary>处理事件消息，通过WebSocket向下发送</summary>
    /// <param name="command"></param>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public override Task HandleAsync(CommandModel command, String message, CancellationToken cancellationToken)
    {
        message ??= command.ToJson();
        return socket.SendAsync(message.GetBytes(), WebSocketMessageType.Text, true, cancellationToken);
    }

    /// <summary>阻塞WebSocket，等待连接结束</summary>  
    /// <param name="context"></param>  
    /// <param name="cancellationToken"></param>  
    /// <returns></returns>  
    public virtual async Task WaitAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var ip = context.GetUserHost();
        var sid = Rand.Next();
        var connection = context.Connection;
        var address = connection.RemoteIpAddress ?? IPAddress.Loopback;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var remote = new IPEndPoint(address, connection.RemotePort);
        WriteLog?.Invoke("WebSocket连接", true, $"State={socket.State} sid={sid} Remote={remote}");

        // 长连接上线  
        SetOnline?.Invoke(true);

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
                    var txt = buf.ToStr(null, 0, data.Count);
                    if (txt == "Ping")
                    {
                        await socket.SendAsync("Pong".GetBytes(), WebSocketMessageType.Text, true, source.Token).ConfigureAwait(false);

                        // 长连接上线。可能客户端心跳已经停了，WS还在，这里重新上线  
                        SetOnline?.Invoke(true);
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
            WriteLog?.Invoke("WebSocket异常", false, ex.Message);
        }
        finally
        {
            source.Cancel();
            WriteLog?.Invoke("WebSocket断开", true, $"State={socket.State} CloseStatus={socket.CloseStatus} sid={sid} Remote={remote}");

            // 长连接下线  
            SetOnline?.Invoke(false);
        }
    }
}
