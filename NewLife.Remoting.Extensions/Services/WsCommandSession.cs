using System.Net;
using System.Net.WebSockets;
using NewLife.Http;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Security;
using NewLife.Serialization;
using WebSocket = System.Net.WebSockets.WebSocket;
using WebSocketMessageType = System.Net.WebSockets.WebSocketMessageType;

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

        try
        {
            // 链接取消令牌。当客户端断开时，触发取消，结束长连接  
            using var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            await socket.WaitForClose(txt =>
            {
                if (txt == "Ping")
                {
                    socket.SendAsync("Pong".GetBytes(), WebSocketMessageType.Text, true, source.Token);

                    // 长连接上线。可能客户端心跳已经停了，WS还在，这里重新上线  
                    SetOnline?.Invoke(true);
                }
            }, source).ConfigureAwait(false);
        }
        finally
        {
            WriteLog?.Invoke("WebSocket断开", true, $"State={socket.State} CloseStatus={socket.CloseStatus} sid={sid} Remote={remote}");

            // 长连接下线  
            SetOnline?.Invoke(false);
        }
    }
}
