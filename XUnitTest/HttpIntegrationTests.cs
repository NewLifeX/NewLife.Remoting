using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NewLife;
using NewLife.Data;
using NewLife.Http;
using NewLife.Log;
using NewLife.Net;
using NewLife.Remoting;
using NewLife.Security;
using Xunit;

namespace XUnitTest.Remoting;

/// <summary>HTTP模式集成测试</summary>
/// <remarks>
/// 验证 ApiServer 使用 HTTP 协议时，ApiHttpClient 端到端通信能力。
/// 包括基础调用、参数传递、异常处理、令牌传递等场景。
/// </remarks>
public class HttpIntegrationTests : DisposeBase
{
    private readonly ApiServer _Server;
    private readonly String _Address;

    public HttpIntegrationTests()
    {
        _Server = new ApiServer(new NetUri(NetType.Http, "*", 0))
        {
            Log = XTrace.Log,
            ShowError = true,
        };
        _Server.Register<HttpTestController>();
        _Server.Start();

        _Address = $"http://127.0.0.1:{_Server.Port}";
    }

    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        _Server.TryDispose();
    }

    #region 基础HTTP调用
    [Fact(DisplayName = "HTTP模式_基础API调用")]
    public async Task HttpBasicApiCallTest()
    {
        IApiClient client = new ApiHttpClient(_Address);
        using var _ = client as IDisposable;

        var apis = await client.InvokeAsync<String[]>("Api/All");
        Assert.NotNull(apis);
        Assert.True(apis.Length > 0);
    }

    [Fact(DisplayName = "HTTP模式_带参数调用")]
    public async Task HttpParameterCallTest()
    {
        IApiClient client = new ApiHttpClient(_Address);
        using var _ = client as IDisposable;

        var result = await client.InvokeAsync<String>("HttpTest/Greet", new { name = "TestUser" });
        Assert.Equal("Hello, TestUser!", result);
    }

    [Fact(DisplayName = "HTTP模式_复杂对象返回")]
    public async Task HttpComplexReturnTest()
    {
        IApiClient client = new ApiHttpClient(_Address);
        using var _ = client as IDisposable;

        var result = await client.InvokeAsync<HttpTestResult>("HttpTest/GetInfo", new { id = 42, name = "TestItem" });
        Assert.NotNull(result);
        Assert.Equal(42, result.Id);
        Assert.Equal("TestItem", result.Name);
        Assert.True(result.Timestamp.Year >= 2020);
    }

    [Fact(DisplayName = "HTTP模式_计算操作")]
    public async Task HttpCalculateTest()
    {
        IApiClient client = new ApiHttpClient(_Address);
        using var _ = client as IDisposable;

        var result = await client.InvokeAsync<Int32>("HttpTest/Add", new { a = 100, b = 200 });
        Assert.Equal(300, result);
    }
    #endregion

    #region HTTP异常处理
    [Fact(DisplayName = "HTTP模式_服务不存在404")]
    public async Task HttpNotFoundTest()
    {
        IApiClient client = new ApiHttpClient(_Address);
        using var _ = client as IDisposable;

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => client.InvokeAsync<Object>("HttpTest/NonExistent"));

        Assert.Equal(404, ex.Code);
    }

    [Fact(DisplayName = "HTTP模式_服务端内部异常500")]
    public async Task HttpServerErrorTest()
    {
        IApiClient client = new ApiHttpClient(_Address);
        using var _ = client as IDisposable;

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => client.InvokeAsync<Object>("HttpTest/ThrowError"));

        Assert.Equal(500, ex.Code);
    }

    [Fact(DisplayName = "HTTP模式_自定义ApiException")]
    public async Task HttpCustomApiExceptionTest()
    {
        IApiClient client = new ApiHttpClient(_Address);
        using var _ = client as IDisposable;

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => client.InvokeAsync<Object>("HttpTest/CustomError"));

        Assert.Equal(1001, ex.Code);
        Assert.Equal("自定义错误", ex.Message);
    }
    #endregion

    #region HTTP令牌传递
    [Fact(DisplayName = "HTTP模式_令牌传递")]
    public async Task HttpTokenTest()
    {
        var token = Rand.NextString(16);

        IApiClient client = new ApiHttpClient(_Address) { Token = token };
        using var _ = client as IDisposable;

        var result = await client.InvokeAsync<IDictionary<String, Object>>("Api/Info", new { state = "test" });
        Assert.NotNull(result);
        // 令牌通过请求头传递
        Assert.Equal(token, result["token"]?.ToString());
    }
    #endregion

    #region HTTP多次调用
    [Fact(DisplayName = "HTTP模式_多次连续调用")]
    public async Task HttpMultipleCallsTest()
    {
        IApiClient client = new ApiHttpClient(_Address);
        using var _ = client as IDisposable;

        for (var i = 0; i < 10; i++)
        {
            var result = await client.InvokeAsync<Int32>("HttpTest/Add", new { a = i, b = 1 });
            Assert.Equal(i + 1, result);
        }
    }

    [Fact(DisplayName = "HTTP模式_并发调用")]
    public async Task HttpConcurrentCallsTest()
    {
        IApiClient client = new ApiHttpClient(_Address);
        using var _ = client as IDisposable;

        var tasks = new List<Task<Int32>>();
        for (var i = 0; i < 20; i++)
        {
            var index = i;
            tasks.Add(client.InvokeAsync<Int32>("HttpTest/Add", new { a = index, b = 1 }));
        }

        var results = await Task.WhenAll(tasks);
        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(i + 1, results[i]);
        }
    }
    #endregion

    #region 辅助类
    class HttpTestController
    {
        public String Greet(String name) => $"Hello, {name}!";

        public Int32 Add(Int32 a, Int32 b) => a + b;

        public HttpTestResult GetInfo(Int32 id, String name) => new()
        {
            Id = id,
            Name = name,
            Timestamp = DateTime.Now,
        };

        public void ThrowError() => throw new InvalidOperationException("服务端异常");

        public void CustomError() => throw new ApiException(1001, "自定义错误");
    }

    class HttpTestResult
    {
        public Int32 Id { get; set; }
        public String Name { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }
    #endregion
}
