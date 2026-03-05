using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest.Remoting;

/// <summary>Session状态与生命周期集成测试</summary>
/// <remarks>
/// 验证 IApiSession 的 Items 跨请求持久化、Token 传播、
/// 多会话隔离以及服务端 Stop 后会话清理等行为。
/// </remarks>
public class SessionLifecycleIntegrationTests : DisposeBase
{
    private readonly ApiServer _Server;
    private readonly Int32 _Port;

    public SessionLifecycleIntegrationTests()
    {
        _Server = new ApiServer(0)
        {
            Log = XTrace.Log,
            ShowError = true,
        };
        _Server.Register<SessionTestController>();
        _Server.Start();

        _Port = _Server.Port;
    }

    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        _Server.TryDispose();
    }

    #region Session.Items跨请求持久化
    [Fact(DisplayName = "Session_Items跨请求持久化")]
    public async Task SessionItemsPersistAcrossRequestsTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        // 设置会话数据
        await client.InvokeAsync<String>("SessionTest/SetData", new { key = "UserName", value = "TestUser" });

        // 同一连接再次读取
        var result = await client.InvokeAsync<String>("SessionTest/GetData", new { key = "UserName" });
        Assert.Equal("TestUser", result);
    }

    [Fact(DisplayName = "Session_多次设置覆盖值")]
    public async Task SessionItemsOverwriteTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        await client.InvokeAsync<String>("SessionTest/SetData", new { key = "Counter", value = "1" });
        await client.InvokeAsync<String>("SessionTest/SetData", new { key = "Counter", value = "2" });

        var result = await client.InvokeAsync<String>("SessionTest/GetData", new { key = "Counter" });
        Assert.Equal("2", result);
    }

    [Fact(DisplayName = "Session_不存在的Key返回空")]
    public async Task SessionItemsNotFoundTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var result = await client.InvokeAsync<String>("SessionTest/GetData", new { key = "NonExistent" });
        Assert.True(result.IsNullOrEmpty());
    }
    #endregion

    #region 多会话隔离
    [Fact(DisplayName = "Session_不同客户端数据隔离")]
    public async Task SessionIsolationTest()
    {
        using var client1 = new ApiClient($"tcp://127.0.0.1:{_Port}");
        using var client2 = new ApiClient($"tcp://127.0.0.1:{_Port}");

        // 客户端1设置数据
        await client1.InvokeAsync<String>("SessionTest/SetData", new { key = "Role", value = "Admin" });

        // 客户端2设置不同数据
        await client2.InvokeAsync<String>("SessionTest/SetData", new { key = "Role", value = "User" });

        // 各自读取自己的数据
        var role1 = await client1.InvokeAsync<String>("SessionTest/GetData", new { key = "Role" });
        var role2 = await client2.InvokeAsync<String>("SessionTest/GetData", new { key = "Role" });

        Assert.Equal("Admin", role1);
        Assert.Equal("User", role2);
    }

    [Fact(DisplayName = "Session_并发客户端数据不串扰")]
    public async Task SessionConcurrentIsolationTest()
    {
        var clients = new List<ApiClient>();
        var clientCount = 5;

        try
        {
            for (var i = 0; i < clientCount; i++)
            {
                var client = new ApiClient($"tcp://127.0.0.1:{_Port}");
                clients.Add(client);

                await client.InvokeAsync<String>("SessionTest/SetData", new { key = "Id", value = i.ToString() });
            }

            // 并发读取各自数据
            var tasks = clients.Select((c, i) =>
                c.InvokeAsync<String>("SessionTest/GetData", new { key = "Id" })
            ).ToList();

            var results = await Task.WhenAll(tasks);

            for (var i = 0; i < clientCount; i++)
            {
                Assert.Equal(i.ToString(), results[i]);
            }
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

    #region Token传播
    [Fact(DisplayName = "Session_Token在会话中持久")]
    public async Task SessionTokenPersistenceTest()
    {
        using var server = new ApiServer(0) { Log = XTrace.Log, ShowError = true };
        server.Handler = new TokenApiHandler { Host = server };
        server.Start();

        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}") { Token = "MyToken" };

        // 第一次调用设置Token
        await client.InvokeAsync<IDictionary<String, Object>>("Api/Info", new { state = "t1" });

        // 第二次调用Token仍在
        var infs = await client.InvokeAsync<IDictionary<String, Object>>("Api/Info", new { state = "t2" });
        Assert.Equal("MyToken", infs["token"]?.ToString());
    }
    #endregion

    #region 会话计数与清理
    [Fact(DisplayName = "Session_连接数随客户端增减")]
    public async Task SessionCountTrackingTest()
    {
        Assert.Empty(_Server.Server.AllSessions);

        var clients = new List<ApiClient>();
        try
        {
            // 逐步增加
            for (var i = 0; i < 3; i++)
            {
                var client = new ApiClient($"tcp://127.0.0.1:{_Port}");
                await client.InvokeAsync<String[]>("Api/All");
                clients.Add(client);
            }
            Assert.Equal(3, _Server.Server.AllSessions.Length);

            // 关闭一个
            clients[0].Close("测试关闭");
            clients[0].Dispose();
            clients.RemoveAt(0);

            await Task.Delay(300);
            Assert.Equal(2, _Server.Server.AllSessions.Length);
        }
        finally
        {
            foreach (var client in clients)
            {
                client.TryDispose();
            }
        }
    }

    [Fact(DisplayName = "Session_服务器Stop后会话全部清理")]
    public async Task ServerStopCleansAllSessionsTest()
    {
        using var server = new ApiServer(0) { Log = XTrace.Log, ShowError = true };
        server.Register<SessionTestController>();
        server.Start();

        var clients = new List<ApiClient>();
        for (var i = 0; i < 3; i++)
        {
            var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");
            await client.InvokeAsync<String[]>("Api/All");
            clients.Add(client);
        }

        Assert.Equal(3, server.Server.AllSessions.Length);

        // 停止服务器
        server.Stop("测试停止");

        await Task.Delay(300);
        Assert.Empty(server.Server.AllSessions);

        foreach (var client in clients)
        {
            client.TryDispose();
        }
    }
    #endregion

    #region AllSessions可见性
    [Fact(DisplayName = "Session_IApi可访问AllSessions")]
    public async Task AllSessionsAccessibleTest()
    {
        using var client1 = new ApiClient($"tcp://127.0.0.1:{_Port}");
        using var client2 = new ApiClient($"tcp://127.0.0.1:{_Port}");

        await client1.InvokeAsync<String[]>("Api/All");
        await client2.InvokeAsync<String[]>("Api/All");

        // 通过控制器查询会话总数
        var count = await client1.InvokeAsync<Int32>("SessionTest/GetSessionCount");
        Assert.Equal(2, count);
    }
    #endregion

    #region 辅助类
    class SessionTestController : IApi
    {
        public IApiSession Session { get; set; } = null!;

        public String SetData(String key, String value)
        {
            Session[key] = value;
            return "OK";
        }

        public String? GetData(String key) => Session[key]?.ToString();

        public Int32 GetSessionCount() => Session.AllSessions.Length;
    }
    #endregion
}
