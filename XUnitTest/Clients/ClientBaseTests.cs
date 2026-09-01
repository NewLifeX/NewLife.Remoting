using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Clients;
using NewLife.Remoting.Models;
using Xunit;

namespace XUnitTest.Clients;

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

    /// <summary>登录必然失败的测试客户端，模拟服务端不可达或登录被拒绝</summary>
    private class FailLoginClient : TestClientBase
    {
        public FailLoginClient() : base("tcp://127.0.0.1:1")
        {
            Features = Features.Login;
        }

        protected override Task<ILoginResponse?> LoginAsync(ILoginRequest request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("模拟登录失败");
    }

    [Fact]
    [DisplayName("登录失败自动启动重试定时器")]
    public async Task Login_Failure_AutoStartRetryTimer()
    {
        using var client = new FailLoginClient();

        // 首次登录失败，应抛出异常
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.Login("test"));
        Assert.Equal("模拟登录失败", ex.Message);
        Assert.Equal(LoginStatus.Ready, client.Status);

        // 失败后应自动启动重试定时器，配合滚动登录超时设计
        var timer = typeof(ClientBase).GetField("_timerLogin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(client);
        Assert.NotNull(timer);

        // 稍等片刻，确认定时器确实在触发重试（日志出现 TryConnectServer 重试）
        await Task.Delay(200);
    }

    [Fact]
    [DisplayName("调用方取消登录不启动重试定时器")]
    public async Task Login_Canceled_NoRetryTimer()
    {
        using var client = new FailLoginClient();

        using var source = new CancellationTokenSource();
        source.Cancel();

        // 调用方已取消，登录应失败
        await Assert.ThrowsAnyAsync<Exception>(() => client.Login("test", source.Token));

        // 取消场景不应自动启动重试定时器
        var timer = typeof(ClientBase).GetField("_timerLogin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(client);
        Assert.Null(timer);
    }
}
