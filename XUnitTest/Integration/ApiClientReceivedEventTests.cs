using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using NewLife.Messaging;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest.Integration;

/// <summary>
/// ApiClient 接收事件回归测试。
/// 语义约定：ApiClient.Received 面向数据监视，与底层 socket Received 一致——请求与响应
/// （含被 SendMessageAsync 匹配消费的响应）一律触发；同时解码产物 ApiMessage.Data 独立克隆，
/// 保证与 SendMessageAsync 等待方共享同一消息负载时，并发解码不产生 ArrayPool 脏读。
/// 修复对象：ApiServer搜索 等靠 Received 事件收集应答的场景（曾因拦截 Reply 收不到任何应答）。
/// </summary>
public class ApiClientReceivedEventTests : DisposeBase
{
    private readonly ApiServer _Server;
    private readonly Int32 _Port;

    public ApiClientReceivedEventTests()
    {
        _Server = new ApiServer(TestPort.PickDual())
        {
            Log = XTrace.Log,
            ShowError = true,
            ReuseAddress = true,
        };
        _Server.Start();

        _Port = _Server.Port;
    }

    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        _Server.TryDispose();
    }

    private ApiClient CreateClient()
    {
        var client = new ApiClient($"tcp://127.0.0.1:{_Port}");
        client.Open();
        return client;
    }

    [Fact(DisplayName = "单播 InvokeAsync 的响应同时经 Received 事件可见且数据完整")]
    public async Task MatchedReply_VisibleInReceived()
    {
        using var client = CreateClient();

        var got = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);
        String? action = null;
        Object? decoded = null;
        var count = 0;
        client.Received += (s, e) =>
        {
            Interlocked.Increment(ref count);
            if (e.Message is IMessage imsg && imsg.Reply)
            {
                action = e.ApiMessage?.Action;
                // ApiMessage 仅在回调内有效，须在回调内同步解码，验证共享负载未被提前归还
                if (e.ApiMessage?.Data != null)
                    decoded = client.Encoder.DecodeResult(action!, e.ApiMessage.Data, e.Message, typeof(Object));

                got.TrySetResult(true);
            }
        };

        var rs = await client.InvokeAsync<Object>("Api/Info");
        Assert.NotNull(rs);

        var completed = await Task.WhenAny(got.Task, Task.Delay(3_000));
        Assert.True(completed == got.Task, "被匹配消费的响应未上抛 Received 事件");

        Assert.Equal("Api/Info", action);
        Assert.NotNull(decoded);
        Assert.Equal(1, Volatile.Read(ref count));
    }

    [Fact(DisplayName = "只发不等的请求，应答作为未匹配响应经 Received 事件可见")]
    public async Task UnmatchedReply_VisibleInReceived()
    {
        using var client = CreateClient();

        var got = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);
        String? action = null;
        client.Received += (s, e) =>
        {
            if (e.Message is IMessage imsg && imsg.Reply)
            {
                action = e.ApiMessage?.Action;
                got.TrySetResult(true);
            }
        };

        // 只发不等：不注册挂起请求（模拟 ApiServer搜索 的广播探测），服务端应答即为未匹配响应
        using var req = client.Encoder.CreateRequest("Api/Info", null);
        var socket = client.Cluster.Get();
        try
        {
            socket.SendMessage(req);
        }
        finally
        {
            client.Cluster.Return(socket);
        }

        var completed = await Task.WhenAny(got.Task, Task.Delay(3_000));
        Assert.True(completed == got.Task, "未收到未匹配的服务端应答");
        Assert.Equal("Api/Info", action);
    }

    [Fact(DisplayName = "并发多请求的响应均经 Received 事件可见且无脏读")]
    public async Task ConcurrentReplies_VisibleInReceived()
    {
        using var client = CreateClient();

        var got = new ConcurrentQueue<String>();
        var done = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Received += (s, e) =>
        {
            if (e.Message is IMessage imsg && imsg.Reply)
            {
                // 回调内同步解码，验证共享负载完整可用
                if (e.ApiMessage?.Data != null)
                    client.Encoder.DecodeResult(e.ApiMessage.Action, e.ApiMessage.Data, e.Message, typeof(Object));

                got.Enqueue(e.ApiMessage?.Action ?? "");
                if (got.Count >= 10) done.TrySetResult(true);
            }
        };

        // 10 个并发请求复用同一连接，按序列号匹配各自响应
        var tasks = new List<Task<Object?>>();
        for (var i = 0; i < 10; i++)
            tasks.Add(client.InvokeAsync<Object>("Api/Info"));
        await Task.WhenAll(tasks);

        var completed = await Task.WhenAny(done.Task, Task.Delay(5_000));
        Assert.True(completed == done.Task, "并发响应未全部经 Received 事件可见");

        Assert.Equal(10, got.Count);
        Assert.All(got, action => Assert.Equal("Api/Info", action));
    }
}
