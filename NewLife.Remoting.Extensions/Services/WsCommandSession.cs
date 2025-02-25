﻿using System.Net.WebSockets;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
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
}
