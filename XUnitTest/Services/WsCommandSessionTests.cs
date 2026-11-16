using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NewLife;
using NewLife.Data;
using NewLife.Messaging;
using NewLife.Remoting.Extensions.Services;
using NewLife.Remoting.Models;
using Xunit;
using WebSocketMessageType = System.Net.WebSockets.WebSocketMessageType;

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

    #region 分片消息
    /// <summary>按预设帧序列返回的 WebSocket 子类。用于模拟一帧消息被拆成多片</summary>
    private class FragmentWebSocket : WebSocket
    {
        private readonly List<(Byte[] Data, Boolean EndOfMessage, WebSocketMessageType Type)> _frames;
        private Int32 _index;

        public FragmentWebSocket(params (Byte[] Data, Boolean EndOfMessage, WebSocketMessageType Type)[] frames) => _frames = [.. frames];

        public override WebSocketCloseStatus? CloseStatus => WebSocketCloseStatus.NormalClosure;
        public override String? CloseStatusDescription => "Test";
        public override WebSocketState State => WebSocketState.Open;
        public override String? SubProtocol => null;

        public override void Abort() { }
        public override Task CloseAsync(WebSocketCloseStatus closeStatus, String? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, String? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override void Dispose() { }
        public override Task SendAsync(ArraySegment<Byte> buffer, WebSocketMessageType messageType, Boolean endOfMessage, CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<Byte> buffer, CancellationToken cancellationToken)
        {
            if (_index >= _frames.Count) return Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));

            var (data, eom, type) = _frames[_index++];
            Array.Copy(data, 0, buffer.Array!, buffer.Offset, data.Length);

            return Task.FromResult(new WebSocketReceiveResult(data.Length, type, eom));
        }
    }

    /// <summary>捕获收到的消息内容，避免依赖 EventHub</summary>
    private class CaptureSession(WebSocket socket) : WsCommandSession(socket)
    {
        public String? Body { get; private set; }

        protected override Task OnReceive(IPacket data, CancellationToken cancellationToken)
        {
            Body = data.ToStr();

            return Task.CompletedTask;
        }
    }

    /// <summary>检测并发发送的 WebSocket。真实实现遇到并发 SendAsync 会抛异常</summary>
    private class SerialDetectWebSocket : WebSocket
    {
        private Int32 _busy;

        /// <summary>是否出现两次发送重叠</summary>
        public Boolean Overlapped { get; private set; }

        public override WebSocketCloseStatus? CloseStatus => WebSocketCloseStatus.NormalClosure;
        public override String? CloseStatusDescription => "Test";
        public override WebSocketState State => WebSocketState.Open;
        public override String? SubProtocol => null;

        public override void Abort() { }
        public override Task CloseAsync(WebSocketCloseStatus closeStatus, String? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, String? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override void Dispose() { }
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<Byte> buffer, CancellationToken cancellationToken)
            => Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));

        public override async Task SendAsync(ArraySegment<Byte> buffer, WebSocketMessageType messageType, Boolean endOfMessage, CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref _busy, 1) == 1) Overlapped = true;

            // 模拟真实网络发送耗时，给并发发送留出重叠窗口
            await Task.Delay(20, cancellationToken).ConfigureAwait(false);
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    [Fact]
    [DisplayName("并发发送被串行化")]
    public async Task SendAsync_Concurrent_Serialized()
    {
        using var ws = new SerialDetectWebSocket();
        using var session = new WsCommandSession(ws);

        await Task.WhenAll(
            session.SendAsync("msg-1"),
            session.SendAsync("msg-2"),
            session.SendAsync("msg-3"));

        Assert.False(ws.Overlapped, "同一连接不允许并发 SendAsync");
    }

    [Fact(DisplayName = "事件广播与命令发送共用串行闸门")]
    public async Task HandleAsync_ConcurrentWithSend_Serialized()
    {
        using var ws = new SerialDetectWebSocket();
        using var session = new WsCommandSession(ws);
        var data = new ArrayPacket("event#topic#client#{}".GetBytes());

        await Task.WhenAll(
            session.SendAsync("msg-1"),
            session.HandleAsync(data, null, default),
            session.SendAsync("msg-2"));

        Assert.False(ws.Overlapped, "事件广播不得绕过发送闸门");
    }

    [Fact]
    [DisplayName("分片消息拼接后再交给处理器")]
    public async Task Receive_FragmentedMessage_Aggregated()
    {
        var text = "fragmented-message-payload";
        var bytes = text.GetBytes();
        using var ws = new FragmentWebSocket(
            (bytes[..7], false, WebSocketMessageType.Text),
            (bytes[7..], true, WebSocketMessageType.Text));
        using var session = new CaptureSession(ws);

        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Loopback;
        ctx.Connection.RemotePort = 12345;
        var dc = new DeviceContext { ["HttpContext"] = ctx };

        using var cts = new CancellationTokenSource(3_000);
        await session.WaitAsync(dc, cts.Token);

        Assert.Equal(text, session.Body);
    }

    /// <summary>捕获分发给事件总线的 Raw 数据包</summary>
    private class RawCaptureDispatcher : IEventHandler<IPacket>
    {
        public List<IPacket> Items { get; } = [];

        public Task HandleAsync(IPacket data, IEventContext? context, CancellationToken cancellationToken)
        {
            if (context is IExtend ext && ext["Raw"] is IPacket raw) Items.Add(raw);

            return Task.CompletedTask;
        }
    }

    [Fact]
    [DisplayName("Raw数据包与接收缓冲解耦")]
    public async Task Receive_RawPacket_IndependentFromBuffer()
    {
        using var ws = new FragmentWebSocket(
            ("first-message".GetBytes(), true, WebSocketMessageType.Text),
            ("second-message-long".GetBytes(), true, WebSocketMessageType.Text));
        var dispatcher = new RawCaptureDispatcher();
        using var session = new WsCommandSession(ws) { Dispatcher = dispatcher };

        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Loopback;
        ctx.Connection.RemotePort = 12345;
        var dc = new DeviceContext { ["HttpContext"] = ctx };

        using var cts = new CancellationTokenSource(3_000);
        await session.WaitAsync(dc, cts.Token);

        Assert.Equal(2, dispatcher.Items.Count);
        Assert.Equal("first-message", dispatcher.Items[0].ToStr());
        Assert.Equal("second-message-long", dispatcher.Items[1].ToStr());
    }

    [Fact]
    [DisplayName("分片消息超过上限时断开而不是无限聚合")]
    public async Task Receive_FragmentedMessage_OverLimit_Dropped()
    {
        var bytes = new Byte[16];
        using var ws = new FragmentWebSocket(
            (bytes[..8], false, WebSocketMessageType.Binary),
            (bytes[8..], true, WebSocketMessageType.Binary));
        using var session = new CaptureSession(ws) { MaxMessageSize = 10 };

        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Loopback;
        ctx.Connection.RemotePort = 12345;
        var dc = new DeviceContext { ["HttpContext"] = ctx };

        using var cts = new CancellationTokenSource(3_000);
        await session.WaitAsync(dc, cts.Token);

        Assert.Null(session.Body);
    }
    #endregion
}
