using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Net;
using NewLife.Remoting;
using NewLife.Security;
using Xunit;

namespace XUnitTest.Remoting;

/// <summary>TCP与HTTP双协议混合集成测试</summary>
/// <remarks>
/// 验证同一控制器可同时通过 TCP/HTTP 访问，TCP 和 HTTP 客户端
/// 并发混合调用、UseHttpStatus 模式、ApiException 错误码穿透等行为。
/// </remarks>
public class DualProtocolIntegrationTests : DisposeBase
{
    private readonly ApiServer _TcpServer;
    private readonly ApiServer _HttpServer;
    private readonly Int32 _TcpPort;
    private readonly String _HttpAddress;

    public DualProtocolIntegrationTests()
    {
        // TCP 服务器
        _TcpServer = new ApiServer(0)
        {
            Log = XTrace.Log,
            ShowError = true,
        };
        _TcpServer.Register<DualController>();
        _TcpServer.Start();
        _TcpPort = _TcpServer.Port;

        // HTTP 服务器
        _HttpServer = new ApiServer(new NetUri(NetType.Http, "*", 0))
        {
            Log = XTrace.Log,
            ShowError = true,
        };
        _HttpServer.Register<DualController>();
        _HttpServer.Start();
        _HttpAddress = $"http://127.0.0.1:{_HttpServer.Port}";
    }

    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        _TcpServer.TryDispose();
        _HttpServer.TryDispose();
    }

    #region 同一控制器双协议访问
    [Fact(DisplayName = "双协议_TCP调用成功")]
    public async Task TcpCallTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_TcpPort}");

        var result = await client.InvokeAsync<String>("Dual/Hello", new { name = "TCP" });
        Assert.Equal("Hello, TCP!", result);
    }

    [Fact(DisplayName = "双协议_HTTP调用成功")]
    public async Task HttpCallTest()
    {
        IApiClient client = new ApiHttpClient(_HttpAddress);
        using var _ = client as IDisposable;

        var result = await client.InvokeAsync<String>("Dual/Hello", new { name = "HTTP" });
        Assert.Equal("Hello, HTTP!", result);
    }

    [Fact(DisplayName = "双协议_计算结果一致")]
    public async Task SameResultAcrossProtocolsTest()
    {
        using var tcpClient = new ApiClient($"tcp://127.0.0.1:{_TcpPort}");
        IApiClient httpClient = new ApiHttpClient(_HttpAddress);
        using var _ = httpClient as IDisposable;

        var tcpResult = await tcpClient.InvokeAsync<Int32>("Dual/Compute", new { a = 100, b = 200 });
        var httpResult = await httpClient.InvokeAsync<Int32>("Dual/Compute", new { a = 100, b = 200 });

        Assert.Equal(300, tcpResult);
        Assert.Equal(300, httpResult);
        Assert.Equal(tcpResult, httpResult);
    }

    [Fact(DisplayName = "双协议_复杂对象序列化一致")]
    public async Task ComplexObjectConsistencyTest()
    {
        using var tcpClient = new ApiClient($"tcp://127.0.0.1:{_TcpPort}");
        IApiClient httpClient = new ApiHttpClient(_HttpAddress);
        using var _ = httpClient as IDisposable;

        var tcpResult = await tcpClient.InvokeAsync<DualResult>("Dual/GetInfo", new { id = 42 });
        var httpResult = await httpClient.InvokeAsync<DualResult>("Dual/GetInfo", new { id = 42 });

        Assert.NotNull(tcpResult);
        Assert.NotNull(httpResult);
        Assert.Equal(tcpResult.Id, httpResult.Id);
        Assert.Equal(tcpResult.Name, httpResult.Name);
    }
    #endregion

    #region 异常传播
    [Fact(DisplayName = "双协议_TCP异常传播")]
    public async Task TcpExceptionPropagationTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_TcpPort}");

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => client.InvokeAsync<Object>("Dual/Fail"));
        Assert.Equal(500, ex.Code);
    }

    [Fact(DisplayName = "双协议_HTTP异常传播")]
    public async Task HttpExceptionPropagationTest()
    {
        IApiClient client = new ApiHttpClient(_HttpAddress);
        using var _ = client as IDisposable;

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => client.InvokeAsync<Object>("Dual/Fail"));
        Assert.Equal(500, ex.Code);
    }

    [Fact(DisplayName = "双协议_自定义ApiException穿透TCP")]
    public async Task TcpCustomApiExceptionTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_TcpPort}");

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => client.InvokeAsync<Object>("Dual/CustomFail"));
        Assert.Equal(1001, ex.Code);
        Assert.Equal("业务异常", ex.Message);
    }

    [Fact(DisplayName = "双协议_自定义ApiException穿透HTTP")]
    public async Task HttpCustomApiExceptionTest()
    {
        IApiClient client = new ApiHttpClient(_HttpAddress);
        using var _ = client as IDisposable;

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => client.InvokeAsync<Object>("Dual/CustomFail"));
        Assert.Equal(1001, ex.Code);
        Assert.Equal("业务异常", ex.Message);
    }
    #endregion

    #region TCP与HTTP并发混合
    [Fact(DisplayName = "双协议_混合并发调用")]
    public async Task MixedConcurrentCallsTest()
    {
        using var tcpClient = new ApiClient($"tcp://127.0.0.1:{_TcpPort}");
        IApiClient httpClient = new ApiHttpClient(_HttpAddress);
        using var _ = httpClient as IDisposable;

        var tasks = new List<Task<Int32>>();

        // TCP 和 HTTP 交替并发
        for (var i = 0; i < 20; i++)
        {
            var index = i;
            if (i % 2 == 0)
                tasks.Add(tcpClient.InvokeAsync<Int32>("Dual/Compute", new { a = index, b = 1 }));
            else
                tasks.Add(httpClient.InvokeAsync<Int32>("Dual/Compute", new { a = index, b = 1 }));
        }

        var results = await Task.WhenAll(tasks);

        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(i + 1, results[i]);
        }
    }
    #endregion

    #region UseHttpStatus模式
    [Fact(DisplayName = "UseHttpStatus_TCP正常工作")]
    public async Task UseHttpStatusTcpModeTest()
    {
        using var server = new ApiServer(0)
        {
            Log = XTrace.Log,
            ShowError = true,
            UseHttpStatus = true,
        };
        server.Register<DualController>();
        server.Start();

        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");

        // 正常调用应成功
        var result = await client.InvokeAsync<String>("Dual/Hello", new { name = "Status" });
        Assert.Equal("Hello, Status!", result);

        // 异常仍可传播
        var ex = await Assert.ThrowsAsync<ApiException>(
            () => client.InvokeAsync<Object>("Dual/Fail"));
        Assert.Equal(500, ex.Code);
    }
    #endregion

    #region API列表双协议
    [Fact(DisplayName = "双协议_TCP和HTTP获取一致的API列表")]
    public async Task ApiListConsistencyTest()
    {
        using var tcpClient = new ApiClient($"tcp://127.0.0.1:{_TcpPort}");
        IApiClient httpClient = new ApiHttpClient(_HttpAddress);
        using var _ = httpClient as IDisposable;

        var tcpApis = await tcpClient.InvokeAsync<String[]>("Api/All");
        var httpApis = await httpClient.InvokeAsync<String[]>("Api/All");

        Assert.NotNull(tcpApis);
        Assert.NotNull(httpApis);

        // 同一控制器注册到不同服务器，API 列表应包含相同的业务方法
        Assert.Contains(tcpApis, a => a.Contains("Dual/Hello"));
        Assert.Contains(httpApis, a => a.Contains("Dual/Hello"));
    }
    #endregion

    #region 大数据双协议
    [Fact(DisplayName = "双协议_TCP传输二进制数据")]
    public async Task TcpBinaryDataTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_TcpPort}");

        var data = Rand.NextBytes(4096);
        var result = await client.InvokeAsync<Packet>("Dual/Echo", data);

        Assert.NotNull(result);
        Assert.Equal(data.Length, result.Total);
        Assert.True(data.SequenceEqual(result.ToArray()));
    }
    #endregion

    #region 辅助类
    class DualController
    {
        public String Hello(String name) => $"Hello, {name}!";

        public Int32 Compute(Int32 a, Int32 b) => a + b;

        public DualResult GetInfo(Int32 id) => new()
        {
            Id = id,
            Name = $"Item_{id}",
            Created = DateTime.Now,
        };

        public void Fail() => throw new InvalidOperationException("服务端异常");

        public void CustomFail() => throw new ApiException(1001, "业务异常");

        public IPacket Echo(IPacket pk) => pk;
    }

    class DualResult
    {
        public Int32 Id { get; set; }
        public String Name { get; set; } = "";
        public DateTime Created { get; set; }
    }
    #endregion
}
