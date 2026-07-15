using System;
using System.ComponentModel;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using NewLife.Remoting.Extensions.Services;
using Xunit;

namespace XUnitTest.Services;

/// <summary>WsCommandSession 单元测试</summary>
public class WsCommandSessionTests
{
    /// <summary>测试用 WebSocket 子类。模拟 WebSocket 行为，避免依赖真实连接</summary>
    private class TestWebSocket : WebSocket
    {
        public override WebSocketCloseStatus? CloseStatus => WebSocketCloseStatus.NormalClosure;
        public override String? CloseStatusDescription => "Test";
        public override WebSocketState State { get; }
        public override String? SubProtocol => null;

        public TestWebSocket(WebSocketState state = WebSocketState.Open) => State = state;

        public override void Abort() { }
        public override Task CloseAsync(WebSocketCloseStatus closeStatus, String? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, String? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override void Dispose() { }
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<Byte> buffer, CancellationToken cancellationToken)
            => Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));
        public override Task SendAsync(ArraySegment<Byte> buffer, WebSocketMessageType messageType, Boolean endOfMessage, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Fact]
    [DisplayName("WsCommandSession基本属性")]
    public void BasicProperties()
    {
        using var ws = new TestWebSocket(WebSocketState.Open);
        using var session = new WsCommandSession(ws);

        Assert.Null(session.Code);
        Assert.True(session.Active);
    }

    [Fact]
    [DisplayName("WsCommandSession断开后Active为false")]
    public void Disconnected_ActiveFalse()
    {
        using var ws = new TestWebSocket(WebSocketState.Closed);
        using var session = new WsCommandSession(ws);

        Assert.False(session.Active);
    }

    [Fact]
    [DisplayName("WsCommandSession_Code设置")]
    public void Code_SetAndGet()
    {
        using var ws = new TestWebSocket(WebSocketState.Open);
        using var session = new WsCommandSession(ws)
        {
            Code = "test-device-001"
        };

        Assert.Equal("test-device-001", session.Code);
    }

    [Fact]
    [DisplayName("WsCommandSession_HandleAsync_SendText")]
    public async Task HandleAsync_SendsText()
    {
        using var ws = new TestWebSocket(WebSocketState.Open);
        using var session = new WsCommandSession(ws);

        // HandleAsync with null message and null command should not throw
        await session.HandleAsync(null!, null, CancellationToken.None);
    }

    [Fact]
    [DisplayName("WsCommandSession_Dispose_NoThrow")]
    public void Dispose_NoThrow()
    {
        using var ws = new TestWebSocket(WebSocketState.Open);
        var session = new WsCommandSession(ws);

        // Dispose should not throw even if called multiple times
        session.Dispose();
        session.Dispose();
    }

    [Fact]
    [DisplayName("WsCommandSession_SendAsync_WhenNotActive_ReturnsCompleted")]
    public async Task SendAsync_NotActive_ReturnsCompleted()
    {
        using var ws = new TestWebSocket(WebSocketState.Closed);
        using var session = new WsCommandSession(ws);

        // Should return completed task without throwing
        await session.SendAsync("test message");
    }
}
