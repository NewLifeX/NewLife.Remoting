using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Caching;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest.Remoting;

/// <summary>ApiServer单元测试</summary>
public class ApiServerTests
{
    #region 基础启停测试
    [Fact(DisplayName = "动态端口测试")]
    public void DynamicPortTest()
    {
        // Port=0 让系统自动分配可用端口
        using var server = new ApiServer(0)
        {
            Log = XTrace.Log,
        };

        // 启动前端口为0
        Assert.Equal(0, server.Port);

        server.Start();

        // 启动后端口应大于0
        Assert.True(server.Port > 0, $"启动后端口应大于0，实际值：{server.Port}");
        Assert.True(server.Active);

        XTrace.WriteLine($"系统分配的端口：{server.Port}");

        server.Stop("测试完成");
        Assert.False(server.Active);
    }

    [Fact(DisplayName = "指定端口测试")]
    public void SpecifiedPortTest()
    {
        // 先用动态端口获取一个可用端口，避免 CI 环境端口冲突
        using var tempServer = new ApiServer(0);
        tempServer.Start();
        var port = tempServer.Port;
        tempServer.Stop("获取端口");
        tempServer.Dispose();

        using var server = new ApiServer(port)
        {
            Log = XTrace.Log,
        };

        Assert.Equal(port, server.Port);

        server.Start();

        // 指定端口时，端口号应保持不变
        Assert.Equal(port, server.Port);
        Assert.True(server.Active);

        server.Stop("测试完成");
    }

    [Fact(DisplayName = "多次动态端口测试")]
    public void MultipleDynamicPortTest()
    {
        // 创建两个动态端口服务器，验证端口不冲突
        using var server1 = new ApiServer(0) { Log = XTrace.Log };
        using var server2 = new ApiServer(0) { Log = XTrace.Log };

        server1.Start();
        server2.Start();

        Assert.True(server1.Port > 0);
        Assert.True(server2.Port > 0);
        Assert.NotEqual(server1.Port, server2.Port);

        XTrace.WriteLine($"服务器1端口：{server1.Port}，服务器2端口：{server2.Port}");

        server1.Stop("测试完成");
        server2.Stop("测试完成");
    }

    [Fact(DisplayName = "重复启动停止测试")]
    public void RepeatStartStopTest()
    {
        using var server = new ApiServer(0) { Log = XTrace.Log };

        // 首次启动
        server.Start();
        Assert.True(server.Active);
        var port1 = server.Port;

        // 重复启动应无副作用
        server.Start();
        Assert.True(server.Active);
        Assert.Equal(port1, server.Port);

        // 停止
        server.Stop("测试");
        Assert.False(server.Active);

        // 重复停止应无副作用
        server.Stop("测试");
        Assert.False(server.Active);
    }
    #endregion

    #region 服务注册测试
    [Fact(DisplayName = "注册控制器类型")]
    public void RegisterControllerTypeTest()
    {
        using var server = new ApiServer(0) { Log = XTrace.Log };
        server.Register<TestController>();
        server.Start();

        // 验证服务已注册
        var services = server.Manager.Services;
        Assert.True(services.ContainsKey("Test/Hello"));
        Assert.True(services.ContainsKey("Test/Add"));
    }

    [Fact(DisplayName = "注册控制器实例")]
    public void RegisterControllerInstanceTest()
    {
        using var server = new ApiServer(0) { Log = XTrace.Log };
        var controller = new TestController();
        server.Register(controller, null);
        server.Start();

        // 验证服务已注册
        var services = server.Manager.Services;
        Assert.True(services.ContainsKey("Test/Hello"));
    }

    [Fact(DisplayName = "注册单个方法")]
    public void RegisterSingleMethodTest()
    {
        using var server = new ApiServer(0) { Log = XTrace.Log };
        var controller = new TestController();
        server.Register(controller, "Hello");
        server.Start();

        // 只注册了Hello方法
        var services = server.Manager.Services;
        Assert.True(services.ContainsKey("Test/Hello"));
        Assert.False(services.ContainsKey("Test/Add"));
    }

    [Fact(DisplayName = "默认Api控制器")]
    public async Task DefaultApiControllerTest()
    {
        using var server = new ApiServer(0) { Log = XTrace.Log };
        server.Start();

        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");

        // 默认注册了ApiController
        var apis = await client.InvokeAsync<String[]>("Api/All");
        Assert.NotNull(apis);
        Assert.True(apis.Length >= 2);
    }
    #endregion

    #region 服务端配置测试
    [Fact(DisplayName = "ShowError配置测试")]
    public void ShowErrorConfigTest()
    {
        using var server = new ApiServer(0)
        {
            Log = XTrace.Log,
            ShowError = true,
        };

        Assert.True(server.ShowError);

        server.ShowError = false;
        Assert.False(server.ShowError);
    }

    [Fact(DisplayName = "Multiplex配置测试")]
    public void MultiplexConfigTest()
    {
        using var server = new ApiServer(0) { Log = XTrace.Log };

        // 默认启用连接复用
        Assert.True(server.Multiplex);

        server.Multiplex = false;
        Assert.False(server.Multiplex);
    }

    [Fact(DisplayName = "ReuseAddress配置测试")]
    public void ReuseAddressConfigTest()
    {
        using var server = new ApiServer(0) { Log = XTrace.Log };

        // 默认不启用地址重用
        Assert.False(server.ReuseAddress);

        server.ReuseAddress = true;
        Assert.True(server.ReuseAddress);
    }

    [Fact(DisplayName = "StatPeriod配置测试")]
    public void StatPeriodConfigTest()
    {
        using var server = new ApiServer(0) { Log = XTrace.Log };

        // 默认600秒
        Assert.Equal(600, server.StatPeriod);

        server.StatPeriod = 0;
        server.Start();

        // StatPeriod=0时不启动统计定时器
        Assert.Null(server.StatProcess);
    }

    [Fact(DisplayName = "UseHttpStatus配置测试")]
    public void UseHttpStatusConfigTest()
    {
        using var server = new ApiServer(0) { Log = XTrace.Log };

        // 默认false
        Assert.False(server.UseHttpStatus);

        server.UseHttpStatus = true;
        Assert.True(server.UseHttpStatus);
    }
    #endregion

    #region 会话管理测试
    [Fact(DisplayName = "会话连接测试")]
    public async Task SessionConnectionTest()
    {
        using var server = new ApiServer(0) { Log = XTrace.Log };
        server.Start();

        // 连接前无会话
        Assert.Empty(server.Server.AllSessions);

        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");
        await client.InvokeAsync<String[]>("Api/All");

        // 连接后有会话
        Assert.Single(server.Server.AllSessions);
    }

    [Fact(DisplayName = "多客户端会话测试")]
    public async Task MultiClientSessionTest()
    {
        using var server = new ApiServer(0) { Log = XTrace.Log };
        server.Start();

        var clients = new List<ApiClient>();
        try
        {
            // 创建多个客户端
            for (var i = 0; i < 3; i++)
            {
                var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");
                await client.InvokeAsync<String[]>("Api/All");
                clients.Add(client);
            }

            // 应有3个会话
            Assert.Equal(3, server.Server.AllSessions.Length);
        }
        finally
        {
            foreach (var client in clients)
            {
                client.TryDispose();
            }
        }
    }
    #endregion

    #region 广播测试
    [Fact(DisplayName = "InvokeAll广播测试")]
    public async Task InvokeAllTest()
    {
        using var server = new ApiServer(0) { Log = XTrace.Log };
        server.Start();

        var broadcastReceived = 0;
        var clients = new List<TestApiClient>();

        try
        {
            // 创建多个客户端
            for (var i = 0; i < 3; i++)
            {
                var client = new TestApiClient($"tcp://127.0.0.1:{server.Port}");
                client.OnBroadcastReceived += () => Interlocked.Increment(ref broadcastReceived);
                await client.InvokeAsync<String[]>("Api/All");
                clients.Add(client);
            }

            // 广播消息
            var count = server.InvokeAll("Broadcast", new { message = "Hello" });
            Assert.Equal(3, count);

            // 等待消息送达
            await Task.Delay(300);

            Assert.Equal(3, broadcastReceived);
        }
        finally
        {
            foreach (var client in clients)
            {
                client.TryDispose();
            }
        }
    }
    #endregion

    #region 依赖注入测试
    [Fact(DisplayName = "ServiceProvider依赖注入测试")]
    public async Task ServiceProviderTest()
    {
        var cache = new MemoryCache();
        var ioc = ObjectContainer.Current;
        ioc.AddSingleton<ICache>(cache);
        ioc.AddTransient<DIService>();

        using var server = new ApiServer(0)
        {
            Log = XTrace.Log,
            ServiceProvider = ioc.BuildServiceProvider(),
        };
        server.Register<DIController>();
        server.Start();

        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");
        var rs = await client.InvokeAsync<Int64>("DI/Increment", new { key = "test", value = 100 });

        Assert.Equal(100, rs);

        // 再次调用应累加
        rs = await client.InvokeAsync<Int64>("DI/Increment", new { key = "test", value = 50 });
        Assert.Equal(150, rs);
    }
    #endregion

    #region 异常处理测试
    [Fact(DisplayName = "服务不存在异常")]
    public async Task ServiceNotFoundTest()
    {
        using var server = new ApiServer(0) { Log = XTrace.Log };
        server.Start();

        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => client.InvokeAsync<Object>("NotExist/Method"));

        Assert.Equal(404, ex.Code);
    }

    [Fact(DisplayName = "服务内部异常")]
    public async Task ServiceInternalErrorTest()
    {
        using var server = new ApiServer(0)
        {
            Log = XTrace.Log,
            ShowError = true,
        };
        server.Register<ErrorController>();
        server.Start();

        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => client.InvokeAsync<Object>("Error/Throw"));

        Assert.Equal(500, ex.Code);
    }

    [Fact(DisplayName = "自定义ApiException")]
    public async Task CustomApiExceptionTest()
    {
        using var server = new ApiServer(0) { Log = XTrace.Log };
        server.Register<ErrorController>();
        server.Start();

        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => client.InvokeAsync<Object>("Error/CustomError"));

        Assert.Equal(1001, ex.Code);
        Assert.Equal("自定义错误", ex.Message);
    }
    #endregion

    #region Received事件测试
    [Fact(DisplayName = "Received事件触发测试")]
    public async Task ReceivedEventTest()
    {
        using var server = new ApiServer(0) { Log = XTrace.Log };

        var receivedActions = new List<String>();
        server.Received += (s, e) =>
        {
            if (e.ApiMessage != null)
                receivedActions.Add(e.ApiMessage.Action);
        };

        server.Start();

        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");
        await client.InvokeAsync<String[]>("Api/All");
        await client.InvokeAsync<Object>("Api/Info", new { state = "test" });

        Assert.Contains("Api/All", receivedActions);
        Assert.Contains("Api/Info", receivedActions);
    }
    #endregion

    #region 测试辅助类
    class TestController
    {
        public String Hello(String name) => $"Hello, {name}!";

        public Int32 Add(Int32 a, Int32 b) => a + b;
    }

    class DIController
    {
        private readonly DIService _service;

        public DIController(DIService service) => _service = service;

        public Int64 Increment(String key, Int64 value) => _service.Increment(key, value);
    }

    class DIService
    {
        private readonly ICache _cache;

        public DIService(ICache cache) => _cache = cache;

        public Int64 Increment(String key, Int64 value) => _cache.Increment(key, value);
    }

    class ErrorController
    {
        public void Throw() => throw new InvalidOperationException("测试异常");

        public void CustomError() => throw new ApiException(1001, "自定义错误");
    }

    class TestApiClient : ApiClient
    {
        public event Action? OnBroadcastReceived;

        public TestApiClient(String uri) : base(uri) { }

        protected override void OnReceive(NewLife.Messaging.IMessage message, ApiReceivedEventArgs e)
        {
            base.OnReceive(message, e);
            // 只计数广播消息，过滤响应消息
            if (e.ApiMessage?.Action == "Broadcast")
                OnBroadcastReceived?.Invoke();
        }
    }
    #endregion
}
