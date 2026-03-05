using System;
using System.ComponentModel;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Clients;
using NewLife.Remoting.Models;
using Xunit;

namespace XUnitTest;

/// <summary>ClientBase单元测试</summary>
public class ClientBaseTests
{
    /// <summary>测试用ClientBase子类</summary>
    private class TestClientBase : ClientBase
    {
        public TestClientBase() : base()
        {
            Name = "Test";
        }

        public TestClientBase(String server) : this()
        {
            Server = server;
        }
    }

    [Fact]
    [DisplayName("默认属性")]
    public void DefaultProperties()
    {
        using var client = new TestClientBase();

        Assert.Equal("Test", client.Name);
        Assert.Null(client.Server);
        Assert.Null(client.Code);
        Assert.Null(client.Secret);
        Assert.Equal(15_000, client.Timeout);
        Assert.Null(client.PasswordProvider);
        Assert.Equal(LoginStatus.Ready, client.Status);
        Assert.False(client.Logined);
        Assert.Equal(0, client.Delay);
        Assert.Equal(TimeSpan.Zero, client.Span);
        Assert.Equal(1440, client.MaxFails);
        Assert.NotNull(client.Commands);
        Assert.Empty(client.Commands);
        Assert.True(client.Features.HasFlag(Features.Login));
        Assert.True(client.Features.HasFlag(Features.Logout));
        Assert.True(client.Features.HasFlag(Features.Ping));
    }

    [Fact]
    [DisplayName("属性读写")]
    public void Properties_SetAndGet()
    {
        using var client = new TestClientBase();

        client.Name = "MyDevice";
        client.Server = "http://localhost:8080";
        client.Code = "device001";
        client.Secret = "secret123";
        client.Timeout = 30_000;
        client.MaxFails = 100;
        client.Features = Features.Login | Features.Ping | Features.Upgrade;

        Assert.Equal("MyDevice", client.Name);
        Assert.Equal("http://localhost:8080", client.Server);
        Assert.Equal("device001", client.Code);
        Assert.Equal("secret123", client.Secret);
        Assert.Equal(30_000, client.Timeout);
        Assert.Equal(100, client.MaxFails);
        Assert.True(client.Features.HasFlag(Features.Upgrade));
    }

    [Fact]
    [DisplayName("LoginStatus初始为Ready")]
    public void Status_InitiallyReady()
    {
        using var client = new TestClientBase();

        Assert.Equal(LoginStatus.Ready, client.Status);
        Assert.False(client.Logined);
    }

    [Fact]
    [DisplayName("Status设置为LoggedIn")]
    public void Status_SetLoggedIn()
    {
        using var client = new TestClientBase();

        client.Status = LoginStatus.LoggedIn;

        Assert.Equal(LoginStatus.LoggedIn, client.Status);
        Assert.True(client.Logined);
    }

    [Fact]
    [DisplayName("Commands注册命令")]
    public void Commands_Register()
    {
        using var client = new TestClientBase();

        Func<String?, String?> handler = arg => "result";
        client.Commands["test"] = handler;

        Assert.Single(client.Commands);
        Assert.True(client.Commands.ContainsKey("test"));
    }

    [Fact]
    [DisplayName("Client设置IClientSetting")]
    public void ClientSetting_Constructor()
    {
        using var client = new TestClientBase
        {
            Server = "http://localhost:8080",
            Code = "app001",
            Secret = "secret"
        };

        Assert.Equal("http://localhost:8080", client.Server);
        Assert.Equal("app001", client.Code);
        Assert.Equal("secret", client.Secret);
    }

    [Fact]
    [DisplayName("日志属性")]
    public void Log_Properties()
    {
        using var client = new TestClientBase();

        Assert.NotNull(client.Log);

        client.Log = XTrace.Log;
        Assert.Equal(XTrace.Log, client.Log);
    }

    [Fact]
    [DisplayName("Tracer属性")]
    public void Tracer_Property()
    {
        using var client = new TestClientBase();

        client.Tracer = DefaultTracer.Instance;
        Assert.Equal(DefaultTracer.Instance, client.Tracer);
    }

    [Fact]
    [DisplayName("ServiceProvider属性")]
    public void ServiceProvider_Property()
    {
        using var client = new TestClientBase();

        Assert.Null(client.ServiceProvider);
    }

    [Fact]
    [DisplayName("OnLogined事件")]
    public void OnLogined_Event()
    {
        using var client = new TestClientBase();

        var eventFired = false;
        client.OnLogined += (s, e) => eventFired = true;

        // 仅验证事件是否可以被附加
        Assert.False(eventFired);
    }

    [Fact]
    [DisplayName("Received事件")]
    public void Received_Event()
    {
        using var client = new TestClientBase();

        var eventFired = false;
        client.Received += (s, e) => eventFired = true;

        Assert.False(eventFired);
    }
}
