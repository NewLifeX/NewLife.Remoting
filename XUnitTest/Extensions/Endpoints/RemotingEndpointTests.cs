using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Extensions;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Serialization;
using Xunit;

namespace XUnitTest.Extensions.Endpoints;

/// <summary>最小 API 端点集成测试</summary>
/// <remarks>
/// 真实 Kestrel 承载最小 API 宿主（显式端口），客户端使用与设备端一致的 ApiHelper 拆信封，
/// 覆盖登录/心跳/升级/事件/命令回执/鉴权失败与 WebSocket 指令下发。
/// 底层设备服务用 Mock 替身，专注端点接入与协议封装本身。
/// </remarks>
public class RemotingEndpointTests : IAsyncLifetime
{
    private readonly Int32 _port = TestPort.PickDual();
    private readonly Mock<IDeviceService> _deviceService = new();
    private readonly Mock<IDeviceModel> _device = new();
    private readonly TestTokenSetting _setting = new() { TokenSecret = "HS256:NewLifeRemotingMinimalApiTest_123456" };

    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private String _token = "";

    #region 辅助
    /// <summary>测试用令牌设置</summary>
    private class TestTokenSetting : ITokenSetting
    {
        public String TokenSecret { get; set; } = null!;
        public Int32 TokenExpire { get; set; } = 3600;
        public Int32 SessionTimeout { get; set; } = 600;
        public Boolean AutoRegister { get; set; } = true;
    }

    /// <summary>启动真实 Kestrel 最小 API 宿主</summary>
    public async Task InitializeAsync()
    {
        _device.Setup(e => e.Code).Returns("dev-01");
        _device.Setup(e => e.Enable).Returns(true);
        _device.Setup(e => e.Name).Returns("测试设备");

        _deviceService.Setup(e => e.QueryDevice(It.IsAny<String>())).Returns(_device.Object);
        _deviceService.Setup(e => e.Login(It.IsAny<DeviceContext>(), It.IsAny<ILoginRequest>(), "Http"))
            .Returns(new LoginResponse { Name = "测试设备" });
        _deviceService.Setup(e => e.Ping(It.IsAny<DeviceContext>(), It.IsAny<IPingRequest>(), null))
            .Returns(new PingResponse { Period = 60 });
        _deviceService.Setup(e => e.PostEvents(It.IsAny<DeviceContext>(), It.IsAny<EventModel[]>()))
            .Returns((DeviceContext c, EventModel[] es) => es.Length);
        _deviceService.Setup(e => e.CommandReply(It.IsAny<DeviceContext>(), It.IsAny<CommandReplyModel>())).Returns(1);
        _deviceService.Setup(e => e.Upgrade(It.IsAny<DeviceContext>(), It.IsAny<String?>()))
            .Returns(new UpgradeInfo { Version = "1.0.0", Source = "/file/update.zip" });

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseUrls($"http://127.0.0.1:{_port}");
        builder.Logging.ClearProviders();

        var services = builder.Services;
        services.AddRemotingCore(_setting);
        services.AddSingleton(_deviceService.Object);
        services.AddSingleton(new Mock<ITracer>().Object);

        var app = builder.Build();
        app.UseRemoting();
        app.MapRemoting();

        await app.StartAsync().ConfigureAwait(false);

        _app = app;
        _client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_port}") };
    }

    /// <summary>停止宿主</summary>
    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_app != null) await _app.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>发送 Json 请求并拆解 code/data 信封</summary>
    private async Task<TResult?> PostAsync<TResult>(String action, Object? args)
    {
        var text = await PostTextAsync(action, args).ConfigureAwait(false);

        return ApiHelper.ProcessResponse<TResult>(text, null, "data");
    }

    /// <summary>发送 Json 请求，返回原始响应文本</summary>
    private async Task<String> PostTextAsync(String action, Object? args = null)
    {
        var json = JsonHelper.ToJson(args ?? new Dictionary<String, Object?>(), JsonHelper.Default.Options);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var msg = await _client.PostAsync(action, content).ConfigureAwait(false);

        return await msg.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    /// <summary>发送 GET 请求，返回原始响应文本</summary>
    private async Task<String> GetTextAsync(String action)
    {
        using var msg = await _client.GetAsync(action).ConfigureAwait(false);

        return await msg.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    /// <summary>登录并保存令牌</summary>
    private async Task<LoginResponse> LoginAsync()
    {
        var rs = await PostAsync<LoginResponse>("Login", new Dictionary<String, Object?>
        {
            ["Code"] = "dev-01",
            ["Secret"] = "abc",
        }).ConfigureAwait(false);

        Assert.NotNull(rs);
        Assert.False(rs!.Token.IsNullOrEmpty());

        _token = rs.Token!;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        return rs;
    }
    #endregion

    [Fact, DisplayName("最小API登录返回令牌且带code信封")]
    public async Task Login_ReturnsToken()
    {
        var text = await PostTextAsync("Login", new Dictionary<String, Object?> { ["Code"] = "dev-01" });

        // 与 MVC 的 ApiFilter 保持一致：{code:0,data:{...}}
        Assert.Contains("\"code\":0", text);

        var rs = await LoginAsync();
        Assert.True(rs.Expire > 0);
        Assert.True(rs.ServerTime > 0);
    }

    [Fact, DisplayName("最小API心跳按令牌装载设备")]
    public async Task Ping_ReturnsResponse()
    {
        await LoginAsync();

        var rs = await PostAsync<PingResponse>("Ping", new Dictionary<String, Object?> { ["State"] = "ok" });

        Assert.NotNull(rs);
        Assert.Equal(60, rs!.Period);
    }

    [Fact, DisplayName("最小API无令牌返回401业务码")]
    public async Task Unauthorized_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var text = await PostTextAsync("Ping");

        Assert.Contains("\"code\":401", text);
        Assert.Contains("认证失败", text);
    }

    [Fact, DisplayName("最小API拒绝把刷新令牌当作访问令牌")]
    public async Task RefreshToken_AsAccessToken_Rejected()
    {
        await LoginAsync();

        var tokenService = _app.Services.GetRequiredService<ITokenService>();
        var tm = tokenService.IssueToken("dev-01", "client1");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tm.RefreshToken);

        var text = await PostTextAsync("Ping");

        Assert.Contains("\"code\":401", text);
        Assert.Contains("刷新令牌不能作为访问令牌使用", text);
    }

    [Fact, DisplayName("最小API升级返回绝对更新地址")]
    public async Task Upgrade_ReturnsAbsoluteSource()
    {
        await LoginAsync();

        var rs = await PostAsync<UpgradeInfo>("Upgrade", null);

        Assert.NotNull(rs);
        Assert.Equal("1.0.0", rs!.Version);
        Assert.StartsWith($"http://127.0.0.1:{_port}/", rs.Source);
    }

    [Fact, DisplayName("最小API事件上报返回处理数量")]
    public async Task PostEvents_ReturnsCount()
    {
        await LoginAsync();

        var events = new Dictionary<String, Object?>[]
        {
            new() { ["Type"] = "info", ["Name"] = "LightOpen", ["Remark"] = "开灯" },
            new() { ["Type"] = "alert", ["Name"] = "HighTemp", ["Remark"] = "高温" },
        };

        var count = await PostAsync<Int32>("PostEvents", events);

        Assert.Equal(2, count);
    }

    [Fact, DisplayName("最小API命令回执返回处理数量")]
    public async Task CommandReply_ReturnsCount()
    {
        await LoginAsync();

        var count = await PostAsync<Int32>("CommandReply", new Dictionary<String, Object?>
        {
            ["Id"] = 123,
            ["Status"] = "已完成",
            ["Data"] = "OK",
        });

        Assert.Equal(1, count);

        _deviceService.Verify(e => e.CommandReply(It.IsAny<DeviceContext>(), It.Is<CommandReplyModel>(m => m.Id == 123)), Times.Once);
    }

    [Fact, DisplayName("最小API指令下发为匿名接口")]
    public async Task SendCommand_Anonymous()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var reply = new CommandReplyModel { Id = 1, Status = CommandStatus.已完成 };
        _deviceService.Setup(e => e.SendCommand(It.IsAny<DeviceContext>(), It.IsAny<CommandInModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(reply);

        var rs = await PostAsync<CommandReplyModel>("SendCommand", new Dictionary<String, Object?>
        {
            ["Code"] = "dev-01",
            ["Command"] = "Reboot",
        });

        Assert.NotNull(rs);
        Assert.Equal(1, rs!.Id);
    }

    [Fact, DisplayName("最小API指令下发缺少编码时报错")]
    public async Task SendCommand_WithoutCode_ReturnsError()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var text = await PostTextAsync("SendCommand", new Dictionary<String, Object?> { ["Command"] = "Reboot" });

        Assert.Contains("\"code\":", text);
        Assert.Contains("必须指定编码", text);
    }

    [Fact, DisplayName("最小API非WebSocket请求通知端点返回400")]
    public async Task Notify_WithoutWebSocket_Returns400()
    {
        await LoginAsync();

        var text = await GetTextAsync("Notify");

        Assert.Contains("\"code\":400", text);
        Assert.Contains("不是WebSocket请求", text);
    }

    [Fact, DisplayName("最小API通过WebSocket下发指令")]
    public async Task Notify_WebSocket_ReceivesCommand()
    {
        await LoginAsync();

        using var ws = new ClientWebSocket();
        using var source = new CancellationTokenSource(15_000);

        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{_port}/Notify?Token={_token}"), source.Token);

        // 等待会话注册到会话管理器
        var manager = _app.Services.GetRequiredService<ISessionManager>();
        for (var i = 0; i < 50 && manager.Get("dev-01") == null; i++)
        {
            await Task.Delay(100, source.Token);
        }
        Assert.NotNull(manager.Get("dev-01"));

        // 服务端下发命令，客户端应从 WebSocket 收到命令文本
        var command = new CommandModel { Id = 9, Command = "Restart", Argument = "svc" };
        await manager.PublishAsync("dev-01", command, null, 0, source.Token);

        var buf = new Byte[4096];
        var data = await ws.ReceiveAsync(new ArraySegment<Byte>(buf), source.Token);
        var text = buf.ToStr(null, 0, data.Count);

        Assert.Contains("Restart", text);

        // 服务端会话销毁时不等待关闭握手（WsCommandSession.Dispose 先 CloseAsync 再释放套接字），
        // 客户端因此不做优雅关闭，直接中断连接
        ws.Abort();
    }

    [Fact, DisplayName("最小API登录设备不存在时返回403")]
    public async Task Login_DeviceMissing_Returns403()
    {
        _deviceService.Setup(e => e.QueryDevice("dev-404")).Returns((IDeviceModel)null!);

        var text = await PostTextAsync("Login", new Dictionary<String, Object?> { ["Code"] = "dev-404" });

        // 与 MVC 的 Login_设备不存在_抛出异常 镜像
        Assert.Contains("\"code\":403", text);
        Assert.Contains("未找到设备", text);
    }

    [Fact, DisplayName("最小API注销后令牌立即失效")]
    public async Task Logout_RevokesToken()
    {
        await LoginAsync();

        var rs = await PostAsync<LogoutResponse>("Logout", null);
        Assert.NotNull(rs);

        // 与 MVC 的 Logout_注销后吊销当前令牌 镜像：注销后原令牌不可再用于业务接口
        var text = await PostTextAsync("Ping", new Dictionary<String, Object?> { ["State"] = "ok" });
        Assert.Contains("\"code\":401", text);
        Assert.Contains("令牌已吊销", text);
    }

    [Fact, DisplayName("最小API心跳在令牌临近过期时续签")]
    public async Task Ping_RenewsToken()
    {
        // 缩短令牌有效期，使剩余有效期不足 10 分钟（与 MVC 的 Ping_带设备且令牌过期_刷新令牌 镜像）
        _setting.TokenExpire = 300;

        var login = await LoginAsync();

        // 等过一秒，避免同一秒内颁发的令牌编码结果相同
        await Task.Delay(1100);

        var rs = await PostAsync<PingResponse>("Ping", new Dictionary<String, Object?> { ["State"] = "ok" });

        Assert.NotNull(rs);
        Assert.False(rs!.Token.IsNullOrEmpty());
        Assert.NotEqual(login.Token, rs.Token);
    }
}
