using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using NewLife.Net;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest.Remoting;

/// <summary>连接池/集群集成测试</summary>
/// <remarks>
/// 验证 ApiClient 的 UsePool 模式（ClientPoolCluster）和默认单连接模式（ClientSingleCluster）
/// 在实际 RPC 调用中的行为差异，包括连接复用、负载均衡和故障转移。
/// </remarks>
public class ClusterIntegrationTests : DisposeBase
{
    private readonly ApiServer _Server;
    private readonly Int32 _Port;

    public ClusterIntegrationTests()
    {
        _Server = new ApiServer(0)
        {
            Log = XTrace.Log,
            ShowError = true,
        };
        _Server.Register<ClusterTestController>();
        _Server.Start();

        _Port = _Server.Port;
    }

    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        _Server.TryDispose();
    }

    #region 单连接模式
    [Fact(DisplayName = "单连接模式_基本调用")]
    public async Task SingleClusterBasicCallTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}")
        {
            UsePool = false,
        };

        var result = await client.InvokeAsync<String>("ClusterTest/Echo", new { msg = "Hello" });
        Assert.Equal("Echo:Hello", result);
    }

    [Fact(DisplayName = "单连接模式_多次调用复用连接")]
    public async Task SingleClusterReuseConnectionTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}")
        {
            UsePool = false,
        };

        // 多次调用使用同一连接
        for (var i = 0; i < 10; i++)
        {
            var result = await client.InvokeAsync<Int32>("ClusterTest/Add", new { a = i, b = 1 });
            Assert.Equal(i + 1, result);
        }

        // 单连接模式应只有一个会话
        Assert.Single(_Server.Server.AllSessions);
    }

    [Fact(DisplayName = "单连接模式_并发调用")]
    public async Task SingleClusterConcurrentCallTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}")
        {
            UsePool = false,
        };

        var tasks = Enumerable.Range(0, 50)
            .Select(i => client.InvokeAsync<Int32>("ClusterTest/Add", new { a = i, b = 1 }))
            .ToList();

        var results = await Task.WhenAll(tasks);

        for (var i = 0; i < 50; i++)
        {
            Assert.Equal(i + 1, results[i]);
        }
    }
    #endregion

    #region 连接池模式
    [Fact(DisplayName = "连接池模式_基本调用")]
    public async Task PoolClusterBasicCallTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}")
        {
            UsePool = true,
        };

        var result = await client.InvokeAsync<String>("ClusterTest/Echo", new { msg = "Hello" });
        Assert.Equal("Echo:Hello", result);
    }

    [Fact(DisplayName = "连接池模式_并发调用可复用连接")]
    public async Task PoolClusterConcurrentCallTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}")
        {
            UsePool = true,
        };

        var tasks = Enumerable.Range(0, 20)
            .Select(i => client.InvokeAsync<Int32>("ClusterTest/Add", new { a = i, b = 1 }))
            .ToList();

        var results = await Task.WhenAll(tasks);

        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(i + 1, results[i]);
        }
    }
    #endregion

    #region 多服务器地址
    [Fact(DisplayName = "多服务器_地址列表")]
    public async Task MultiServerAddressListTest()
    {
        // 创建第二个服务器
        using var server2 = new ApiServer(0) { Log = XTrace.Log, ShowError = true };
        server2.Register<ClusterTestController>();
        server2.Start();

        // 两个服务器地址列表
        var uri1 = $"tcp://127.0.0.1:{_Port}";
        var uri2 = $"tcp://127.0.0.1:{server2.Port}";

        using var client = new ApiClient($"{uri1},{uri2}")
        {
            UsePool = false,
        };

        // 调用应成功（连接到任一服务器）
        var result = await client.InvokeAsync<String>("ClusterTest/Echo", new { msg = "Hello" });
        Assert.Equal("Echo:Hello", result);
    }

    [Fact(DisplayName = "单连接故障转移_不可用地址跳过")]
    public async Task SingleClusterFailoverTest()
    {
        // 第一个地址是不可用的，第二个是可用的
        var badUri = "tcp://127.0.0.1:1"; // 端口1通常不可用
        var goodUri = $"tcp://127.0.0.1:{_Port}";

        using var client = new ApiClient($"{badUri},{goodUri}")
        {
            UsePool = false,
        };

        // 故障转移：跳过不可用地址连接到可用地址
        var result = await client.InvokeAsync<String>("ClusterTest/Echo", new { msg = "Failover" });
        Assert.Equal("Echo:Failover", result);
    }
    #endregion

    #region SetServer动态切换
    [Fact(DisplayName = "SetServer_动态切换服务器地址")]
    public async Task SetServerDynamicSwitchTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}")
        {
            UsePool = false,
        };

        // 第一次调用
        var result = await client.InvokeAsync<String>("ClusterTest/Echo", new { msg = "First" });
        Assert.Equal("Echo:First", result);

        // 创建新服务器
        using var server2 = new ApiServer(0) { Log = XTrace.Log, ShowError = true };
        server2.Register<ClusterTestController>();
        server2.Start();

        // 动态切换
        client.SetServer($"tcp://127.0.0.1:{server2.Port}");

        // 再次调用应连接到新服务器
        result = await client.InvokeAsync<String>("ClusterTest/Echo", new { msg = "Second" });
        Assert.Equal("Echo:Second", result);
    }
    #endregion

    #region 辅助类
    class ClusterTestController
    {
        public String Echo(String msg) => $"Echo:{msg}";

        public Int32 Add(Int32 a, Int32 b) => a + b;
    }
    #endregion
}
