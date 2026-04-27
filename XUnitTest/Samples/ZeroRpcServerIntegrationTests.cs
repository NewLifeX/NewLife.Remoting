using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Security;
using XCode.Membership;
using Xunit;

namespace XUnitTest.Samples;

/// <summary>ZeroRpcServer 多协议集成测试。覆盖 TCP / UDP / WebSocket / HTTP 四种协议</summary>
/// <remarks>
/// 四个测试方法共享同一 ApiServer 实例（IClassFixture），减少启动开销。
/// 每个协议独立建立连接，Session 状态各自隔离，互不干扰。
/// </remarks>
[Collection("ZeroRpcServer")]
[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class ZeroRpcServerIntegrationTests : IClassFixture<ZeroRpcServerFixture>
{
    private readonly ZeroRpcServerFixture _fixture;

    public ZeroRpcServerIntegrationTests(ZeroRpcServerFixture fixture) => _fixture = fixture;

    #region 辅助：通用断言逻辑
    /// <summary>
    /// 对给定客户端执行一整套 RPC 断言流程：
    /// api/all、My/Add、My/RC4、User/FindByID（首次成功 + 第二次 429）
    /// </summary>
    private static async Task AssertRpcCallsAsync(IApiClient client, Int32 userId)
    {
        // 1. api/all —— 返回接口列表，包含我们注册的接口
        var apis = await client.InvokeAsync<String[]>("Api/All");
        Assert.NotNull(apis);
        Assert.True(apis.Length >= 5, $"接口数量应 ≥ 5，实际：{apis.Length}");
        Assert.Contains(apis, a => a.Contains("My/Add"));
        Assert.Contains(apis, a => a.Contains("My/RC4"));
        Assert.Contains(apis, a => a.Contains("User/FindByID"));

        // 2. api/info —— 返回服务端信息，含 MachineName
        var state = Rand.NextString(8);
        var state2 = Rand.NextString(8);
        var info = await client.InvokeAsync<IDictionary<String, Object>>("Api/Info", new { state, state2 });
        Assert.NotNull(info);
        Assert.True(info.ContainsKey("MachineName"), "info 应包含 MachineName");
        Assert.True(info.ContainsKey("State"), "info 应包含 State");
        Assert.Equal(state, info["State"]?.ToString());

        // 3. My/Add —— 整数加法，精确验证返回值
        var sum = await client.InvokeAsync<Int32>("My/Add", new { x = 13, y = 7 });
        Assert.Equal(20, sum);

        var sum2 = await client.InvokeAsync<Int32>("My/Add", new { x = -5, y = 5 });
        Assert.Equal(0, sum2);

        // 4. My/RC4 —— 二进制往返：原文加密后再次加密应还原
        // 注： RC4 服务返回 IPacket 二进制，必须用 InvokeAsync<IPacket>（ApiClient 直接返回 message.Data，不过 JSON 解码）
        var original = "Hello NewLife RC4".GetBytes();
        var pk1 = (ArrayPacket)original;
        var encrypted = await client.InvokeAsync<IPacket>("My/RC4", pk1);
        Assert.NotNull(encrypted);
        var encryptedBytes = encrypted.ToArray();
        Assert.NotEqual(original, encryptedBytes); // 加密后应不同

        var pk2 = (ArrayPacket)encryptedBytes;
        var decrypted = await client.InvokeAsync<IPacket>("My/RC4", pk2);
        var decryptedBytes = decrypted?.ToArray();
        Assert.Equal(original, decryptedBytes);    // 解密后应还原

        // 5. User/FindByID（首次）—— 返回实体，校验字段
        var user = await client.InvokeAsync<IDictionary<String, Object>>("User/FindByID", new { id = userId });
        Assert.NotNull(user);
        Assert.True(user.ContainsKey("Name"), "User 响应应包含 Name 字段");
        Assert.False(user["Name"]?.ToString().IsNullOrEmpty(), "User.Name 不应为空");

        // 6. User/FindByID（第二次同 Session）—— 触发 429 TooManyRequests
        var ex = await Assert.ThrowsAsync<ApiException>(
            () => client.InvokeAsync<IDictionary<String, Object>>("User/FindByID", new { id = userId }));
        Assert.Equal(429, ex.Code);
        Assert.Contains("调用次数过多", ex.Message);
    }
    #endregion

    #region TCP 协议测试
    [Fact(DisplayName = "TCP协议_完整RPC调用链")]
    public async Task TcpProtocolTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_fixture.Port}")
        {
            Log = XTrace.Log,
        };

        await AssertRpcCallsAsync(client, _fixture.UserId);
    }
    #endregion

    #region UDP 协议测试
    [Fact(DisplayName = "UDP协议_完整RPC调用链")]
    public async Task UdpProtocolTest()
    {
        using var client = new ApiClient($"udp://127.0.0.1:{_fixture.Port}")
        {
            Log = XTrace.Log,
        };

        await AssertRpcCallsAsync(client, _fixture.UserId);
    }
    #endregion

    #region WebSocket 协议测试
    [Fact(DisplayName = "WebSocket协议_完整RPC调用链")]
    public async Task WebSocketProtocolTest()
    {
        using var client = new ApiClient($"ws://127.0.0.1:{_fixture.Port}")
        {
            Log = XTrace.Log,
        };

        await AssertRpcCallsAsync(client, _fixture.UserId);
    }
    #endregion

    #region HTTP 协议测试
    [Fact(DisplayName = "HTTP协议_完整RPC调用链")]
    public async Task HttpProtocolTest()
    {
        var client = new ApiHttpClient($"http://127.0.0.1:{_fixture.Port}")
        {
            Log = XTrace.Log,
        };

        // 1. api/all（GET）：返回完整签名格式 "ReturnType Route(params)"，用 Contains 子串匹配
        var apis = await client.GetAsync<String[]>("Api/All");
        Assert.NotNull(apis);
        Assert.True(apis.Length >= 5, $"接口数量应 ≥ 5，实际：{apis.Length}");
        Assert.Contains(apis, a => a.Contains("My/Add"));

        // 2. api/info（POST），校验 State 字段回显
        var state = Rand.NextString(8);
        var state2 = Rand.NextString(8);
        var info = await client.PostAsync<IDictionary<String, Object>>("Api/Info", new { state, state2 });
        Assert.NotNull(info);
        Assert.Equal(state, info["State"]?.ToString());

        // 3. My/Add（GET：名称含 Get 或参数为基础类型时自动转 GET）
        var sum = await client.InvokeAsync<Int32>("My/Add", new { x = 100, y = 200 }, default);
        Assert.Equal(300, sum);

        // 4. User/FindByID（GET）
        var user = await client.GetAsync<IDictionary<String, Object>>("User/FindByID", new { id = _fixture.UserId });
        Assert.NotNull(user);
        Assert.True(user.ContainsKey("Name"));
        Assert.False(user["Name"]?.ToString().IsNullOrEmpty());
    }
    #endregion
}
