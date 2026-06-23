using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest.Integration;

/// <summary>traceparent 传播与 Headers 传递集成测试</summary>
public class TraceParentIntegrationTests : DisposeBase
{
    private readonly ApiServer _Server;
    private readonly Int32 _Port;

    public TraceParentIntegrationTests()
    {
        _Server = new ApiServer(0)
        {
            Log = XTrace.Log,
            ShowError = true,
        };
        _Server.Register<TraceTestController>();
        _Server.Start();

        _Port = _Server.Port;
    }

    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        _Server.TryDispose();
    }

    [Fact(DisplayName = "traceparent_Attach自动注入到参数字典")]
    public async Task TraceParentAttachTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");
        client.Open();

        // 启用 Tracer，其 AttachParameter 默认为 "traceparent"
        var tracer = new DefaultTracer { AttachParameter = "traceparent", Log = XTrace.Log };
        client.Tracer = tracer;

        var rs = await client.InvokeAsync<IDictionary<String, Object?>>("TraceTest/GetParameters", new { name = "test" });
        Assert.NotNull(rs);

        // span.Attach 应将 traceparent 注入参数字典
        Assert.True(rs.ContainsKey("traceparent"), "参数字典应包含 traceparent key");
        Assert.False(((String?)rs["traceparent"]).IsNullOrEmpty(), "traceparent 值不应为空");
    }

    [Fact(DisplayName = "Headers_自定义元数据透传")]
    public async Task HeadersCustomMetadataTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");
        client.Open();

        client.Headers["X-TenantId"] = "tenant-01";
        client.Headers["X-Region"] = "cn-east";

        var rs = await client.InvokeAsync<IDictionary<String, Object?>>("TraceTest/GetParameters", new { name = "test" });
        Assert.NotNull(rs);

        Assert.True(rs.ContainsKey("X-TenantId"), "应包含 X-TenantId");
        Assert.Equal("tenant-01", rs["X-TenantId"] as String);
        Assert.True(rs.ContainsKey("X-Region"), "应包含 X-Region");
        Assert.Equal("cn-east", rs["X-Region"] as String);
    }

    [Fact(DisplayName = "Headers_无Headers时不影响正常调用")]
    public async Task HeadersEmptyTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");
        client.Open();

        // Headers 为空时，调用应正常工作
        var rs = await client.InvokeAsync<String>("TraceTest/Echo", new { name = "hello" });
        Assert.Equal("hello", rs);
    }

    #region 测试控制器
    class TraceTestController
    {
        /// <summary>返回收到的参数字典，用于验证 traceparent/Headers 注入</summary>
        public IDictionary<String, Object?> GetParameters(IDictionary<String, Object?> args) => args ?? new Dictionary<String, Object?>();

        /// <summary>回显名称</summary>
        public String Echo(String name) => name;
    }
    #endregion
}
