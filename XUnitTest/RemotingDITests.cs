using System;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;
using NewLife.Remoting.Extensions;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using Xunit;

namespace XUnitTest;

/// <summary>RemotingExtensions DI注册测试</summary>
public class RemotingDITests
{
    private class TestTokenSetting : ITokenSetting
    {
        public String TokenSecret { get; set; } = "HS256:test_secret_key_123456";
        public Int32 TokenExpire { get; set; } = 3600;
        public Boolean AutoRegister { get; set; } = true;
        public Int32 SessionTimeout { get; set; } = 600;
    }

    [Fact]
    [DisplayName("AddRemoting无Setting注册基础服务")]
    public void AddRemoting_NoSetting()
    {
        var services = new ServiceCollection();
        services.AddRemoting();

        var sp = services.BuildServiceProvider();

        // 应能解析基础模型类型
        Assert.NotNull(sp.GetService<ILoginRequest>());
        Assert.NotNull(sp.GetService<ILoginResponse>());
        Assert.NotNull(sp.GetService<ILogoutResponse>());
        Assert.NotNull(sp.GetService<IPingRequest>());
        Assert.NotNull(sp.GetService<IPingResponse>());
    }

    [Fact]
    [DisplayName("AddRemoting有Setting注册TokenService")]
    public void AddRemoting_WithSetting()
    {
        var services = new ServiceCollection();
        var setting = new TestTokenSetting();
        services.AddRemoting(setting);
        services.AddSingleton<ITracer, DefaultTracer>();

        var sp = services.BuildServiceProvider();

        // 应能解析TokenService
        Assert.NotNull(sp.GetService<ITokenService>());
        Assert.NotNull(sp.GetService<ITokenSetting>());
    }

    [Fact]
    [DisplayName("AddRemoting注册密码提供者")]
    public void AddRemoting_RegistersPasswordProvider()
    {
        var services = new ServiceCollection();
        services.AddRemoting();

        var sp = services.BuildServiceProvider();

        var pp = sp.GetService<NewLife.Security.IPasswordProvider>();
        Assert.NotNull(pp);
    }

    [Fact]
    [DisplayName("AddRemoting注册SessionManager")]
    public void AddRemoting_RegistersSessionManager()
    {
        var services = new ServiceCollection();
        services.AddRemoting();

        var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetService<ISessionManager>());
    }

    [Fact]
    [DisplayName("AddRemoting注册缓存提供者")]
    public void AddRemoting_RegistersCacheProvider()
    {
        var services = new ServiceCollection();
        services.AddRemoting();

        var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetService<NewLife.Caching.ICacheProvider>());
    }

    [Fact]
    [DisplayName("AddRemoting模型类型可创建")]
    public void AddRemoting_ModelsCreateable()
    {
        var services = new ServiceCollection();
        services.AddRemoting();

        var sp = services.BuildServiceProvider();

        var loginReq = sp.GetRequiredService<ILoginRequest>();
        Assert.IsType<LoginRequest>(loginReq);

        var loginRes = sp.GetRequiredService<ILoginResponse>();
        Assert.IsType<LoginResponse>(loginRes);

        var pingReq = sp.GetRequiredService<IPingRequest>();
        Assert.IsType<PingRequest>(pingReq);

        var pingRes = sp.GetRequiredService<IPingResponse>();
        Assert.IsType<PingResponse>(pingRes);
    }

    [Fact]
    [DisplayName("TokenService通过DI颁发和解码")]
    public void TokenService_ViaServiceProvider()
    {
        var services = new ServiceCollection();
        var setting = new TestTokenSetting();
        services.AddRemoting(setting);
        services.AddSingleton<ITracer, DefaultTracer>();

        var sp = services.BuildServiceProvider();

        var tokenService = sp.GetRequiredService<ITokenService>();

        // 颁发令牌
        var token = tokenService.IssueToken("testDevice", "client001");
        Assert.NotNull(token);
        Assert.NotNull(token.AccessToken);
        Assert.NotNull(token.RefreshToken);
        Assert.Equal(3600, token.ExpireIn);

        // 解码令牌
        var (jwt, ex) = tokenService.DecodeToken(token.AccessToken);
        Assert.Null(ex);
        Assert.Equal("testDevice", jwt.Subject);
        Assert.Equal("client001", jwt.Id);
    }
}
