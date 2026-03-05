using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Http;
using NewLife.Log;
using NewLife.Net;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest.Remoting;

/// <summary>Token传递与登录流程集成测试</summary>
/// <remarks>
/// 验证 ApiClient 的 Token 注入、TokenApiHandler 的令牌会话管理，
/// 以及 OnLoginAsync / OnNewSession 自动登录机制。
/// </remarks>
public class TokenLoginIntegrationTests : DisposeBase
{
    private readonly ApiServer _Server;
    private readonly Int32 _Port;

    public TokenLoginIntegrationTests()
    {
        _Server = new ApiServer(0)
        {
            Log = XTrace.Log,
            ShowError = true,
        };
        _Server.Handler = new TokenApiHandler { Host = _Server };
        _Server.Start();

        _Port = _Server.Port;
    }

    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        _Server.TryDispose();
    }

    #region Token注入
    [Fact(DisplayName = "Token_自动注入到请求参数")]
    public async Task TokenAutoInjectTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}")
        {
            Token = "MyToken123",
        };

        // Token 通过 Api/Info 的返回值 token 字段验证（TokenApiHandler 从参数中提取 Token 并设置到会话）
        var infs = await client.InvokeAsync<IDictionary<String, Object>>("Api/Info", new { state = "test" });
        Assert.NotNull(infs);
        Assert.Equal("MyToken123", infs["token"]?.ToString());
    }

    [Fact(DisplayName = "Token_无Token时token字段为空")]
    public async Task NoTokenTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var infs = await client.InvokeAsync<IDictionary<String, Object>>("Api/Info", new { state = "test" });
        Assert.NotNull(infs);
        // 无 Token 时，token 字段应为空
        var token = infs.TryGetValue("token", out var t) ? t?.ToString() : null;
        Assert.True(token.IsNullOrEmpty());
    }

    [Fact(DisplayName = "Token_动态更改Token")]
    public async Task DynamicTokenChangeTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}")
        {
            Token = "Token1",
        };

        var infs = await client.InvokeAsync<IDictionary<String, Object>>("Api/Info", new { state = "test1" });
        Assert.NotNull(infs);
        Assert.Equal("Token1", infs["token"]?.ToString());

        // 更换 Token
        client.Token = "Token2";
        infs = await client.InvokeAsync<IDictionary<String, Object>>("Api/Info", new { state = "test2" });
        Assert.NotNull(infs);
        Assert.Equal("Token2", infs["token"]?.ToString());
    }
    #endregion

    #region 会话Token共享
    [Fact(DisplayName = "Token_会话Token正确设置")]
    public async Task SessionTokenSetTest()
    {
        var token = "TestToken_" + Guid.NewGuid().ToString("N").Substring(0, 8);

        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}") { Token = token };

        // 通过 Api/Info 验证 Token 正确设置
        var infs = await client.InvokeAsync<IDictionary<String, Object>>("Api/Info", new { state = "verify" });
        Assert.NotNull(infs);
        Assert.Equal(token, infs["token"]?.ToString());

        // 同一连接再次调用，Token 仍然保留
        infs = await client.InvokeAsync<IDictionary<String, Object>>("Api/Info", new { state = "verify2" });
        Assert.NotNull(infs);
        Assert.Equal(token, infs["token"]?.ToString());
    }

    [Fact(DisplayName = "Token_不同客户端Token隔离")]
    public async Task IsolatedTokenTest()
    {
        using var client1 = new ApiClient($"tcp://127.0.0.1:{_Port}") { Token = "TokenA_123" };
        using var client2 = new ApiClient($"tcp://127.0.0.1:{_Port}") { Token = "TokenB_456" };

        var infs1 = await client1.InvokeAsync<IDictionary<String, Object>>("Api/Info", new { state = "a" });
        var infs2 = await client2.InvokeAsync<IDictionary<String, Object>>("Api/Info", new { state = "b" });

        Assert.Equal("TokenA_123", infs1["token"]?.ToString());
        Assert.Equal("TokenB_456", infs2["token"]?.ToString());
    }
    #endregion

    #region 自动登录
    [Fact(DisplayName = "自动登录_OnNewSession触发登录")]
    public async Task AutoLoginOnNewSessionTest()
    {
        LoginClient.LoginCalledCount = 0;

        using var client = new LoginClient
        {
            Servers = [$"tcp://127.0.0.1:{_Port}"],
        };

        // 首次调用会触发连接，连接后触发 OnNewSession → OnLoginAsync
        var result = await client.InvokeAsync<String[]>("Api/All");
        Assert.NotNull(result);

        // 等待异步登录完成
        for (var i = 0; i < 50 && LoginClient.LoginCalledCount == 0; i++)
        {
            await Task.Delay(50);
        }

        Assert.True(LoginClient.LoginCalledCount >= 1, "OnLoginAsync 应被调用至少1次");
    }

    [Fact(DisplayName = "LoginAsync_手动触发登录")]
    public async Task ManualLoginAsyncTest()
    {
        LoginClient.LoginCalledCount = 0;

        using var client = new LoginClient
        {
            Servers = [$"tcp://127.0.0.1:{_Port}"],
        };

        // Open() 确保集群初始化，LoginAsync 内部通过 Cluster 获取连接
        client.Open();

        // 手动登录
        var result = await client.LoginAsync();
        Assert.True(LoginClient.LoginCalledCount >= 1, "手动 LoginAsync 应被调用");
    }
    #endregion

    #region HTTP模式Token
    [Fact(DisplayName = "HTTP模式_Token通过请求头传递")]
    public async Task HttpTokenViaHeaderTest()
    {
        // 使用HTTP模式创建服务器
        using var httpServer = new ApiServer(new NetUri(NetType.Http, "*", 0))
        {
            Log = XTrace.Log,
            ShowError = true,
        };
        httpServer.Handler = new TokenApiHandler { Host = httpServer };
        httpServer.Start();

        IApiClient client = new ApiHttpClient($"http://127.0.0.1:{httpServer.Port}")
        {
            Token = "HttpToken456",
        };
        using var _ = client as IDisposable;

        var result = await client.InvokeAsync<IDictionary<String, Object>>("Api/Info", new { state = "test" });
        Assert.NotNull(result);
        Assert.Equal("HttpToken456", result["token"]?.ToString());
    }
    #endregion

    #region 辅助类
    class LoginClient : ApiClient
    {
        public static Int32 LoginCalledCount;

        protected override Task<Object?> OnLoginAsync(ISocketClient client, Boolean force, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref LoginCalledCount);
            return Task.FromResult<Object?>("LoginResult");
        }
    }
    #endregion
}
