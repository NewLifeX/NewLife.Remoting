using System.Net.WebSockets;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;

namespace NewLife.Remoting.Extensions.Services;

/// <summary>WebSocket设备会话</summary>
public class WsDeviceSession(WebSocket socket) : DeviceSession
{
    /// <summary>是否活动中</summary>
    public override Boolean Active => socket != null && socket.State == WebSocketState.Open;

    /// <summary>处理事件消息，通过WebSocket向下发送</summary>
    /// <param name="command"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public override async Task HandleAsync(CommandModel command, String message)
    {
        await socket.SendAsync(message.GetBytes(), WebSocketMessageType.Text, true, default).ConfigureAwait(false);
    }
}
