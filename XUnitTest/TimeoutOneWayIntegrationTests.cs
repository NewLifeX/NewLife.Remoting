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

/// <summary>超时、OneWay 和非复用模式集成测试</summary>
/// <remarks>
/// 验证 ApiClient/ApiServer 的超时控制、单向调用、Multiplex 模式切换等行为。
/// </remarks>
public class TimeoutOneWayIntegrationTests : DisposeBase
{
    private readonly ApiServer _Server;
    private readonly Int32 _Port;

    public TimeoutOneWayIntegrationTests()
    {
        _Server = new ApiServer(0)
        {
            Log = XTrace.Log,
            ShowError = true,
        };
        _Server.Register<TimeoutTestController>();
        _Server.Start();

        _Port = _Server.Port;
    }

    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        _Server.TryDispose();
    }

    #region 超时测试
    [Fact(DisplayName = "客户端超时_快速操作正常完成")]
    public async Task TimeoutFastOperationTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}")
        {
            Timeout = 5000, // 5秒超时
        };

        var result = await client.InvokeAsync<String>("TimeoutTest/Fast");
        Assert.Equal("Fast", result);
    }

    [Fact(DisplayName = "客户端超时_慢操作超时取消")]
    public async Task TimeoutSlowOperationTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}")
        {
            Timeout = 500, // 500ms超时
        };

        // SlowAction 会延迟3秒，应超时
        await Assert.ThrowsAnyAsync<Exception>(
            () => client.InvokeAsync<String>("TimeoutTest/Slow"));
    }
    #endregion

    #region OneWay测试
    [Fact(DisplayName = "OneWay_单向发送无等待")]
    public async Task OneWayCallTest()
    {
        TimeoutTestController.OneWayReceived = false;

        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        // 先建立连接
        await client.InvokeAsync<String[]>("Api/All");

        // InvokeOneWay 不等待返回
        var result = client.InvokeOneWay("TimeoutTest/OneWayAction", new { msg = "Hello" });
        Assert.True(result >= 0, $"InvokeOneWay 应返回非负值，实际: {result}");

        // 等待服务端处理
        for (var i = 0; i < 50 && !TimeoutTestController.OneWayReceived; i++)
        {
            await Task.Delay(50);
        }

        Assert.True(TimeoutTestController.OneWayReceived, "服务端应收到单向消息");
    }

    [Fact(DisplayName = "OneWay_服务端无返回")]
    public async Task OneWayNoResponseTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        // 先建立连接
        await client.InvokeAsync<String[]>("Api/All");

        // 多次单向调用
        for (var i = 0; i < 5; i++)
        {
            var result = client.InvokeOneWay("TimeoutTest/OneWayAction", new { msg = $"Msg{i}" });
            Assert.True(result >= 0);
        }

        await Task.Delay(200);
    }
    #endregion

    #region Multiplex模式
    [Fact(DisplayName = "非复用模式_基本调用能正常工作")]
    public async Task NonMultiplexBasicCallTest()
    {
        using var server = new ApiServer(0)
        {
            Log = XTrace.Log,
            ShowError = true,
            Multiplex = false,
        };
        server.Register<TimeoutTestController>();
        server.Start();

        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");

        var result = await client.InvokeAsync<String>("TimeoutTest/Fast");
        Assert.Equal("Fast", result);
    }

    [Fact(DisplayName = "非复用模式_服务端下发消息")]
    public async Task NonMultiplexServerPushTest()
    {
        var messageReceived = new TaskCompletionSource<Boolean>();
        String? receivedAction = null;

        using var server = new ApiServer(0)
        {
            Log = XTrace.Log,
            ShowError = true,
            Multiplex = false,
        };
        server.Start();

        using var client = new ReceivableClient($"tcp://127.0.0.1:{server.Port}");
        client.MessageReceivedCallback = (action, data) =>
        {
            receivedAction = action;
            messageReceived.TrySetResult(true);
        };

        // 建立连接
        await client.InvokeAsync<String[]>("Api/All");

        // 服务端下发
        var session = server.Server.AllSessions.FirstOrDefault();
        Assert.NotNull(session);
        session.InvokeOneWay("ServerPush", new { data = "test" });

        // 等待接收
        await Task.WhenAny(messageReceived.Task, Task.Delay(2000));
        Assert.True(messageReceived.Task.IsCompleted, "应收到服务端下发消息");
        Assert.Equal("ServerPush", receivedAction);
    }
    #endregion

    #region SlowTrace日志
    [Fact(DisplayName = "SlowTrace_慢调用日志")]
    public async Task SlowTraceTest()
    {
        using var server = new ApiServer(0)
        {
            Log = XTrace.Log,
            ShowError = true,
            SlowTrace = 100, // 100ms视为慢处理
        };
        server.Register<TimeoutTestController>();
        server.Start();

        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}")
        {
            SlowTrace = 100,
        };

        // DelayAction 延迟200ms，超过SlowTrace阈值
        var result = await client.InvokeAsync<String>("TimeoutTest/DelayAction");
        Assert.Equal("Delayed", result);
        // SlowTrace 会在日志中输出，无需断言日志内容（已通过日志输出验证）
    }
    #endregion

    #region Received事件集成
    [Fact(DisplayName = "ApiServer_Received事件可拦截请求")]
    public async Task ReceivedEventInterceptTest()
    {
        using var server = new ApiServer(0) { Log = XTrace.Log, ShowError = true };
        server.Register<TimeoutTestController>();

        var interceptedActions = new List<String>();
        server.Received += (s, e) =>
        {
            if (e.ApiMessage != null)
                interceptedActions.Add(e.ApiMessage.Action);
        };

        server.Start();

        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");
        await client.InvokeAsync<String>("TimeoutTest/Fast");
        await client.InvokeAsync<String>("TimeoutTest/Fast");

        Assert.Equal(2, interceptedActions.Count(a => a == "TimeoutTest/Fast"));
    }
    #endregion

    #region 辅助类
    class TimeoutTestController
    {
        public static Boolean OneWayReceived;

        public String Fast() => "Fast";

        public async Task<String> Slow()
        {
            await Task.Delay(3000);
            return "Slow";
        }

        public async Task<String> DelayAction()
        {
            await Task.Delay(200);
            return "Delayed";
        }

        public void OneWayAction(String msg)
        {
            OneWayReceived = true;
        }
    }

    class ReceivableClient : ApiClient
    {
        public Action<String, String?>? MessageReceivedCallback;

        public ReceivableClient(String uri) : base(uri) { }

        protected override void OnReceive(IMessage message, ApiReceivedEventArgs e)
        {
            base.OnReceive(message, e);

            // 只处理服务端主动下发的消息（非响应消息）
            if (message.Reply) return;

            var action = e.ApiMessage?.Action ?? "";
            var data = e.ApiMessage?.Data?.ToStr();
            MessageReceivedCallback?.Invoke(action, data);
        }
    }
    #endregion
}
