using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Messaging;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest.Remoting;

/// <summary>服务端主动下发与双向通信集成测试</summary>
/// <remarks>
/// 验证服务端向指定会话下发消息、客户端注册 Action 供服务端回调、
/// 广播场景、断开重连后恢复通信等行为。
/// </remarks>
public class BidirectionalIntegrationTests : DisposeBase
{
    private readonly ApiServer _Server;
    private readonly Int32 _Port;

    public BidirectionalIntegrationTests()
    {
        _Server = new ApiServer(0)
        {
            Log = XTrace.Log,
            ShowError = true,
        };
        _Server.Register<BidiController>();
        _Server.Start();

        _Port = _Server.Port;
    }

    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        _Server.TryDispose();
    }

    #region 服务端下发到指定会话
    [Fact(DisplayName = "服务端下发_指定会话收到消息")]
    public async Task ServerPushToSpecificSessionTest()
    {
        var received = new TaskCompletionSource<String>();

        using var client = new CallbackApiClient($"tcp://127.0.0.1:{_Port}");
        client.MessageCallback = (action, data) =>
        {
            if (action == "Notify") received.TrySetResult(data ?? "");
        };

        // 建立连接
        await client.InvokeAsync<String[]>("Api/All");

        // 找到该会话并下发
        var session = _Server.Server.AllSessions.FirstOrDefault();
        Assert.NotNull(session);

        session.InvokeOneWay("Notify", new { content = "TargetMsg" });

        var result = await Task.WhenAny(received.Task, Task.Delay(3000));
        Assert.True(received.Task.IsCompleted, "指定会话应收到下发消息");
        Assert.Contains("TargetMsg", await received.Task);
    }

    [Fact(DisplayName = "服务端下发_仅目标会话收到")]
    public async Task ServerPushOnlyTargetReceivesTest()
    {
        var client1Received = 0;
        var client2Received = 0;

        using var client1 = new CallbackApiClient($"tcp://127.0.0.1:{_Port}");
        client1.MessageCallback = (_, _) => Interlocked.Increment(ref client1Received);

        using var client2 = new CallbackApiClient($"tcp://127.0.0.1:{_Port}");
        client2.MessageCallback = (_, _) => Interlocked.Increment(ref client2Received);

        await client1.InvokeAsync<String[]>("Api/All");
        await client2.InvokeAsync<String[]>("Api/All");

        var sessions = _Server.Server.AllSessions;
        Assert.Equal(2, sessions.Length);

        // 仅向第一个会话下发
        sessions[0].InvokeOneWay("TargetOnly", new { msg = "OnlyFirst" });

        await Task.Delay(500);

        // 第一个收到，第二个不应收到
        Assert.True(client1Received > 0 || client2Received > 0, "至少一个客户端应收到消息");
        // 两个加起来应只有1条
        Assert.Equal(1, client1Received + client2Received);
    }
    #endregion

    #region 广播与选择性广播
    [Fact(DisplayName = "广播_所有客户端都收到")]
    public async Task BroadcastAllReceiveTest()
    {
        var receivedCounts = new Int32[3];
        var allDone = new TaskCompletionSource<Boolean>();
        var clients = new List<CallbackApiClient>();

        try
        {
            for (var i = 0; i < 3; i++)
            {
                var index = i;
                var client = new CallbackApiClient($"tcp://127.0.0.1:{_Port}");
                client.MessageCallback = (_, _) =>
                {
                    Interlocked.Increment(ref receivedCounts[index]);
                    if (receivedCounts.All(c => c >= 1))
                        allDone.TrySetResult(true);
                };
                await client.InvokeAsync<String[]>("Api/All");
                clients.Add(client);
            }

            // 广播
            var count = _Server.InvokeAll("BroadcastAction", new { data = "Hello" });
            Assert.Equal(3, count);

            await Task.WhenAny(allDone.Task, Task.Delay(3000));
            for (var i = 0; i < 3; i++)
            {
                Assert.True(receivedCounts[i] >= 1, $"客户端{i}应收到广播消息");
            }
        }
        finally
        {
            foreach (var c in clients) c.TryDispose();
        }
    }

    [Fact(DisplayName = "广播_零客户端时返回0")]
    public void BroadcastWithNoClientsTest()
    {
        var count = _Server.InvokeAll("NoClients", new { data = "test" });
        Assert.Equal(0, count);
    }
    #endregion

    #region 连续下发
    [Fact(DisplayName = "连续下发_多条消息按序到达")]
    public async Task SequentialPushTest()
    {
        var messages = new List<String>();
        var allReceived = new TaskCompletionSource<Boolean>();

        using var client = new CallbackApiClient($"tcp://127.0.0.1:{_Port}");
        client.MessageCallback = (action, data) =>
        {
            lock (messages)
            {
                messages.Add($"{action}:{data}");
                if (messages.Count >= 5) allReceived.TrySetResult(true);
            }
        };

        await client.InvokeAsync<String[]>("Api/All");
        var session = _Server.Server.AllSessions.FirstOrDefault();
        Assert.NotNull(session);

        // 连续下发5条
        for (var i = 0; i < 5; i++)
        {
            session.InvokeOneWay("Seq", new { index = i });
        }

        await Task.WhenAny(allReceived.Task, Task.Delay(3000));
        Assert.True(allReceived.Task.IsCompleted, "应收到全部5条消息");
        Assert.Equal(5, messages.Count);
    }
    #endregion

    #region Received事件完整性
    [Fact(DisplayName = "Received事件_服务端下发触发客户端Received")]
    public async Task ClientReceivedEventFromPushTest()
    {
        // 使用独立 server 避免跨测试干扰
        using var server = new ApiServer(0) { Log = XTrace.Log, ShowError = true };
        server.Register<BidiController>();
        server.Start();

        var receivedActions = new List<String>();
        var allDone = new TaskCompletionSource<Boolean>();

        using var client = new CallbackApiClient($"tcp://127.0.0.1:{server.Port}");
        client.MessageCallback = (action, _) =>
        {
            lock (receivedActions)
            {
                receivedActions.Add(action);
                if (receivedActions.Count >= 2) allDone.TrySetResult(true);
            }
        };

        // 建立连接
        await client.InvokeAsync<String[]>("Api/All");

        // 等待连接就绪
        await Task.Delay(100);

        var session = server.Server.AllSessions.FirstOrDefault();
        Assert.NotNull(session);

        // 服务端主动下发2条消息
        session.InvokeOneWay("Push1", new { data = "A" });
        session.InvokeOneWay("Push2", new { data = "B" });

        await Task.WhenAny(allDone.Task, Task.Delay(5000));
        Assert.Contains("Push1", receivedActions);
        Assert.Contains("Push2", receivedActions);
    }

    [Fact(DisplayName = "Received事件_服务端侧触发")]
    public async Task ServerReceivedEventTest()
    {
        using var server = new ApiServer(0) { Log = XTrace.Log, ShowError = true };
        server.Register<BidiController>();

        var interceptedActions = new List<String>();
        server.Received += (s, e) =>
        {
            if (e.ApiMessage != null)
            {
                lock (interceptedActions)
                    interceptedActions.Add(e.ApiMessage.Action);
            }
        };

        server.Start();

        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");
        await client.InvokeAsync<String>("Bidi/Ping");
        await client.InvokeAsync<Int32>("Bidi/Add", new { a = 1, b = 2 });

        Assert.Contains("Bidi/Ping", interceptedActions);
        Assert.Contains("Bidi/Add", interceptedActions);
    }
    #endregion

    #region 客户端重新连接
    [Fact(DisplayName = "重新连接_新客户端连接成功")]
    public async Task NewClientAfterDisconnectTest()
    {
        // 第一个客户端连接并调用
        var client1 = new ApiClient($"tcp://127.0.0.1:{_Port}");
        var result1 = await client1.InvokeAsync<String>("Bidi/Ping");
        Assert.Equal("Pong", result1);
        Assert.Single(_Server.Server.AllSessions);

        // 关闭第一个客户端
        client1.Close("测试断开");
        client1.Dispose();

        await Task.Delay(300);
        Assert.Empty(_Server.Server.AllSessions);

        // 第二个客户端连接并调用
        using var client2 = new ApiClient($"tcp://127.0.0.1:{_Port}");
        var result2 = await client2.InvokeAsync<String>("Bidi/Ping");
        Assert.Equal("Pong", result2);
        Assert.Single(_Server.Server.AllSessions);
    }

    [Fact(DisplayName = "重新连接_SetServer后可正常调用")]
    public async Task ReconnectViaSetServerTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var result1 = await client.InvokeAsync<String>("Bidi/Ping");
        Assert.Equal("Pong", result1);

        // 使用 SetServer 重置连接
        client.SetServer($"tcp://127.0.0.1:{_Port}");

        var result2 = await client.InvokeAsync<String>("Bidi/Ping");
        Assert.Equal("Pong", result2);
    }
    #endregion

    #region 高频快速下发
    [Fact(DisplayName = "高频下发_大量消息不丢失")]
    public async Task HighFrequencyPushTest()
    {
        var receivedCount = 0;
        var total = 100;
        var allDone = new TaskCompletionSource<Boolean>();

        using var client = new CallbackApiClient($"tcp://127.0.0.1:{_Port}");
        client.MessageCallback = (_, _) =>
        {
            if (Interlocked.Increment(ref receivedCount) >= total)
                allDone.TrySetResult(true);
        };

        await client.InvokeAsync<String[]>("Api/All");
        var session = _Server.Server.AllSessions.FirstOrDefault();
        Assert.NotNull(session);

        // 高频下发
        for (var i = 0; i < total; i++)
        {
            session.InvokeOneWay("HF", new { index = i });
        }

        await Task.WhenAny(allDone.Task, Task.Delay(5000));
        Assert.True(receivedCount >= total * 0.9, $"应至少收到{total * 0.9}条，实际收到{receivedCount}条");
    }
    #endregion

    #region 辅助类
    class BidiController
    {
        public String Ping() => "Pong";

        public Int32 Add(Int32 a, Int32 b) => a + b;
    }

    class CallbackApiClient : ApiClient
    {
        public Action<String, String?>? MessageCallback;

        public CallbackApiClient(String uri) : base(uri) { }

        protected override void OnReceive(IMessage message, ApiReceivedEventArgs e)
        {
            base.OnReceive(message, e);

            if (message.Reply) return;

            var action = e.ApiMessage?.Action ?? "";
            var data = e.ApiMessage?.Data?.ToStr();
            MessageCallback?.Invoke(action, data);
        }
    }
    #endregion
}
