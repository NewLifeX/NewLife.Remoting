using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Extensions;
using NewLife.Remoting.Extensions.Services;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Web;
using Xunit;
using MvcControllerContext = Microsoft.AspNetCore.Mvc.ControllerContext;

namespace XUnitTest.Extensions.Controllers;

/// <summary>BaseDeviceController 单元测试</summary>
/// <remarks>
/// 测试控制器 HTTP 端点的逻辑（参数校验、服务委托、异常处理），
/// 不涉及 ASP.NET Core 管道（路由、模型绑定、鉴权过滤器）。
/// 底层 DeviceService/TokenService/SessionManager 逻辑已在对应 Tests 类中覆盖。
/// </remarks>
public class BaseDeviceControllerTests
{
    #region 辅助
    /// <summary>测试用控制器子类</summary>
    private class TestDeviceController : BaseDeviceController
    {
        public TestDeviceController(IServiceProvider serviceProvider)
            : base(
                serviceProvider.GetService<IDeviceService>(),
                serviceProvider.GetRequiredService<ITokenService>(),
                serviceProvider.GetRequiredService<ISessionManager>(),
                serviceProvider) { }

        public TestDeviceController(IDeviceService? deviceService, ITokenService? tokenService, ISessionManager? sessionManager, IServiceProvider serviceProvider)
            : base(deviceService, tokenService, sessionManager, serviceProvider) { }

        /// <summary>公开 OnAuthorize 用于测试</summary>
        public new Boolean OnAuthorize(String token, DeviceContext context) => base.OnAuthorize(token, context);
    }

    /// <summary>创建模拟服务提供者</summary>
    private static (IServiceProvider sp, Mock<IDeviceService> mockDevice, Mock<ITokenService> mockToken, Mock<ISessionManager> mockSession) CreateMockServices()
    {
        var mockDevice = new Mock<IDeviceService>();
        var mockToken = new Mock<ITokenService>();
        var mockSession = new Mock<ISessionManager>();
        var mockTracer = new Mock<ITracer>();

        var services = new ServiceCollection();
        services.AddSingleton(mockDevice.Object);
        services.AddSingleton(mockToken.Object);
        services.AddSingleton(mockSession.Object);
        services.AddSingleton(mockTracer.Object);
        var sp = services.BuildServiceProvider();

        return (sp, mockDevice, mockToken, mockSession);
    }

    /// <summary>设置控制器上下文</summary>
    private static void SetupControllerContext(TestDeviceController controller, DeviceContext? ctx = null)
    {
        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new MvcControllerContext
        {
            HttpContext = httpContext,
        };

        controller.Context = ctx ?? new DeviceContext
        {
            Code = "test-device",
            Token = "test-token",
        };
    }

    /// <summary>创建模拟设备</summary>
    private static Mock<IDeviceModel> CreateMockDevice(String code = "test-device", Boolean enable = true)
    {
        var mock = new Mock<IDeviceModel>();
        mock.Setup(e => e.Code).Returns(code);
        mock.Setup(e => e.Enable).Returns(enable);
        mock.Setup(e => e.Name).Returns("测试设备");
        return mock;
    }
    #endregion

    #region 构造
    [Fact]
    [DisplayName("BaseDeviceController_构造_服务解析成功")]
    public void Constructor_ResolvesServices()
    {
        var (sp, _, _, _) = CreateMockServices();

        var controller = new TestDeviceController(sp);

        Assert.NotNull(controller);
        // 构造成功即表示服务解析通过
    }

    [Fact]
    [DisplayName("BaseDeviceController_构造_显式注入服务")]
    public void Constructor_ExplicitServices()
    {
        var (sp, mockDevice, mockToken, mockSession) = CreateMockServices();

        var controller = new TestDeviceController(mockDevice.Object, mockToken.Object, mockSession.Object, sp);

        Assert.NotNull(controller);
    }

    [Fact]
    [DisplayName("BaseDeviceController_构造_缺少Token服务抛异常")]
    public void Constructor_MissingTokenService_Throws()
    {
        var mockDevice = new Mock<IDeviceService>();
        var mockSession = new Mock<ISessionManager>();

        var services = new ServiceCollection();
        services.AddSingleton(mockDevice.Object);
        services.AddSingleton(mockSession.Object);
        var sp = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => new TestDeviceController(sp));
    }
    #endregion

    #region Login
    [Fact]
    [DisplayName("Login_登录成功_返回令牌")]
    public async Task Login_Success_ReturnsToken()
    {
        var (sp, mockDevice, mockToken, _) = CreateMockServices();
        var device = CreateMockDevice("dev-001");
        mockDevice.Setup(e => e.QueryDevice("dev-001")).Returns(device.Object);
        mockDevice.Setup(e => e.Login(It.IsAny<DeviceContext>(), It.IsAny<ILoginRequest>(), "Http"))
            .Returns<DeviceContext, ILoginRequest, String>((ctx, req, src) =>
            {
                ctx.Device = device.Object;
                return new LoginResponse { Name = "测试设备" };
            });
        mockToken.Setup(e => e.IssueToken("dev-001", It.IsAny<String>()))
            .Returns(new TokenModel { AccessToken = "jwt-token", TokenType = "JWT", ExpireIn = 3600 });

        var controller = new TestDeviceController(sp);
        SetupControllerContext(controller, new DeviceContext { Code = "dev-001" });

        var request = new LoginRequest { Code = "dev-001", Secret = "pwd", ClientId = "client-001" };
        var result = controller.Login(request);

        Assert.NotNull(result);
        Assert.Equal("jwt-token", result.Token);
        Assert.Equal(3600, result.Expire);
    }

    [Fact]
    [DisplayName("Login_空请求_抛出异常")]
    public void Login_NullRequest_Throws()
    {
        var (sp, _, _, _) = CreateMockServices();
        var controller = new TestDeviceController(sp);
        SetupControllerContext(controller);

        var ex = Assert.Throws<ArgumentNullException>(() => controller.Login(null!));
        Assert.Contains("request", ex.Message);
    }

    [Fact]
    [DisplayName("Login_设备不存在_抛出异常")]
    public void Login_DeviceNotFound_Throws()
    {
        var (sp, mockDevice, _, _) = CreateMockServices();
        mockDevice.Setup(e => e.QueryDevice("dev-001")).Returns((IDeviceModel?)null);
        mockDevice.Setup(e => e.Login(It.IsAny<DeviceContext>(), It.IsAny<ILoginRequest>(), "Http"))
            .Returns<DeviceContext, ILoginRequest, String>((ctx, req, src) =>
            {
                ctx.Device = null;
                return new LoginResponse();
            });

        var controller = new TestDeviceController(sp);
        SetupControllerContext(controller);

        var request = new LoginRequest { Code = "dev-001", Secret = "pwd" };
        var ex = Assert.Throws<ApiException>(() => controller.Login(request));
        Assert.Equal(ApiCode.Forbidden, ex.Code);
    }
    #endregion

    #region Logout
    [Fact]
    [DisplayName("Logout_注销成功_返回响应")]
    public void Logout_Success()
    {
        var (sp, mockDevice, _, _) = CreateMockServices();
        var device = CreateMockDevice("dev-001");
        mockDevice.Setup(e => e.Logout(It.IsAny<DeviceContext>(), It.IsAny<String?>(), "Http"))
            .Returns((DeviceContext ctx, String? reason, String src) => null);

        var controller = new TestDeviceController(sp);
        SetupControllerContext(controller, new DeviceContext
        {
            Code = "dev-001",
            Token = "token",
            Device = device.Object,
        });

        var result = controller.Logout("手动注销");

        Assert.NotNull(result);
        Assert.Null(result.Token);
    }

    [Fact]
    [DisplayName("Logout_空原因_不抛异常")]
    public void Logout_NullReason_NoThrow()
    {
        var (sp, mockDevice, _, _) = CreateMockServices();
        mockDevice.Setup(e => e.Logout(It.IsAny<DeviceContext>(), It.IsAny<String?>(), "Http"))
            .Returns((DeviceContext ctx, String? reason, String src) => null);

        var controller = new TestDeviceController(sp);
        SetupControllerContext(controller);

        var result = controller.Logout(null);

        Assert.NotNull(result);
    }
    #endregion

    #region Ping
    [Fact]
    [DisplayName("Ping_心跳成功_返回响应")]
    public void Ping_Success()
    {
        var (sp, mockDevice, mockToken, _) = CreateMockServices();
        mockDevice.Setup(e => e.Ping(It.IsAny<DeviceContext>(), It.IsAny<IPingRequest>(), null))
            .Returns<DeviceContext, IPingRequest, IPingResponse?>((ctx, req, rsp) =>
                new PingResponse { Period = 60, ServerTime = DateTime.UtcNow.ToLong() });

        var controller = new TestDeviceController(sp);
        SetupControllerContext(controller);

        var request = new PingRequest { Memory = 1024 };
        var result = controller.Ping(request);

        Assert.NotNull(result);
        Assert.Equal(60, result.Period);
    }

    [Fact]
    [DisplayName("Ping_带设备且令牌过期_刷新令牌")]
    public void Ping_TokenExpired_RefreshToken()
    {
        var (sp, mockDevice, mockToken, _) = CreateMockServices();
        var device = CreateMockDevice("dev-001");

        mockDevice.Setup(e => e.Ping(It.IsAny<DeviceContext>(), It.IsAny<IPingRequest>(), null))
            .Returns<DeviceContext, IPingRequest, IPingResponse?>((ctx, req, rsp) =>
                new PingResponse { Period = 60 });

        // 模拟一个即将过期的令牌
        var jwt = new JwtBuilder
        {
            Subject = "dev-001",
            Id = "client-001",
            Secret = "test-secret",
            Algorithm = "HS256",
            Expire = DateTime.Now.AddMinutes(5), // 5分钟后过期，触发刷新
            IssuedAt = DateTime.Now.AddHours(-1),
        };
        var tokenStr = jwt.Encode(null!);

        mockToken.Setup(e => e.DecodeToken(tokenStr))
            .Returns((jwt, (Exception?)null!));
        mockToken.Setup(e => e.IssueToken("dev-001", "client-001"))
            .Returns(new TokenModel { AccessToken = "new-jwt", TokenType = "JWT", ExpireIn = 3600 });

        var controller = new TestDeviceController(sp);
        SetupControllerContext(controller, new DeviceContext
        {
            Code = "dev-001",
            Token = tokenStr,
            Device = device.Object,
        });

        var request = new PingRequest { Memory = 1024 };
        var result = controller.Ping(request);

        Assert.NotNull(result);
        // 令牌即将过期，应刷新
        Assert.Equal("new-jwt", result.Token);
    }
    #endregion

    #region Upgrade
    [Fact]
    [DisplayName("Upgrade_未登录_抛出异常")]
    public void Upgrade_NotLoggedIn_Throws()
    {
        var (sp, _, _, _) = CreateMockServices();
        var controller = new TestDeviceController(sp);
        SetupControllerContext(controller, new DeviceContext
        {
            Code = "dev-001",
            Token = "token",
            Device = null, // 未登录
        });

        var ex = Assert.Throws<ApiException>(() => controller.Upgrade(null));
        Assert.Equal(ApiCode.Unauthorized, ex.Code);
    }

    [Fact]
    [DisplayName("Upgrade_已登录_返回升级信息")]
    public void Upgrade_LoggedIn_ReturnsInfo()
    {
        var (sp, mockDevice, _, _) = CreateMockServices();
        var device = CreateMockDevice("dev-001");

        var upgradeInfo = new Mock<IUpgradeInfo>();
        upgradeInfo.Setup(e => e.Source).Returns("/update/package.zip");
        upgradeInfo.Setup(e => e.Version).Returns("2.0.0");

        mockDevice.Setup(e => e.Upgrade(It.IsAny<DeviceContext>(), It.IsAny<String?>()))
            .Returns(upgradeInfo.Object);

        var controller = new TestDeviceController(sp);
        SetupControllerContext(controller, new DeviceContext
        {
            Code = "dev-001",
            Token = "token",
            Device = device.Object,
        });

        // 设置请求 URL 用于绝对路径解析
        controller.ControllerContext.HttpContext.Request.Scheme = "http";
        controller.ControllerContext.HttpContext.Request.Host = new HostString("localhost", 8080);
        controller.ControllerContext.HttpContext.Request.Path = "/Device/Upgrade";

        var result = controller.Upgrade("stable");

        Assert.NotNull(result);
        Assert.Equal("2.0.0", result.Version);
        // 注：绝对路径解析依赖 GetRawUrl() 扩展方法，在完整 HTTP 管道中测试
    }
    #endregion

    #region CommandReply
    [Fact]
    [DisplayName("CommandReply_正常响应_返回处理结果")]
    public void CommandReply_Success()
    {
        var (sp, mockDevice, _, _) = CreateMockServices();
        mockDevice.Setup(e => e.CommandReply(It.IsAny<DeviceContext>(), It.IsAny<CommandReplyModel>()))
            .Returns(1);

        var controller = new TestDeviceController(sp);
        SetupControllerContext(controller);

        var model = new CommandReplyModel { Id = 123, Status = CommandStatus.已完成 };
        var result = controller.CommandReply(model);

        Assert.Equal(1, result);
    }

    [Fact]
    [DisplayName("CommandReply_空模型_抛出异常")]
    public void CommandReply_NullModel_Throws()
    {
        var (sp, _, _, _) = CreateMockServices();
        var controller = new TestDeviceController(sp);
        SetupControllerContext(controller);

        Assert.Throws<ArgumentNullException>(() => controller.CommandReply(null!));
    }

    [Fact]
    [DisplayName("CommandReply_委托给DeviceService")]
    public void CommandReply_DelegatesToService()
    {
        var (sp, mockDevice, _, _) = CreateMockServices();
        mockDevice.Setup(e => e.CommandReply(It.IsAny<DeviceContext>(), It.IsAny<CommandReplyModel>()))
            .Returns(1);

        var controller = new TestDeviceController(sp);
        SetupControllerContext(controller);

        var model = new CommandReplyModel { Id = 456, Status = CommandStatus.已完成, Data = "OK" };
        controller.CommandReply(model);

        mockDevice.Verify(e => e.CommandReply(It.IsAny<DeviceContext>(), model), Times.Once);
    }
    #endregion

    #region SendCommand
    [Fact]
    [DisplayName("SendCommand_正常发送_返回响应")]
    public async Task SendCommand_Success()
    {
        var (sp, mockDevice, _, _) = CreateMockServices();
        mockDevice.Setup(e => e.SendCommand(It.IsAny<DeviceContext>(), It.IsAny<CommandInModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandReplyModel { Id = 1, Status = CommandStatus.已完成, Data = "Done" });

        var controller = new TestDeviceController(sp);
        SetupControllerContext(controller);

        var model = new CommandInModel { Code = "dev-001", Command = "restart", Argument = "-f" };
        var result = await controller.SendCommand(model);

        Assert.NotNull(result);
        Assert.Equal(CommandStatus.已完成, result!.Status);
        Assert.Equal("Done", result.Data);
    }

    [Fact]
    [DisplayName("SendCommand_空模型_抛出异常")]
    public async Task SendCommand_NullModel_Throws()
    {
        var (sp, _, _, _) = CreateMockServices();
        var controller = new TestDeviceController(sp);
        SetupControllerContext(controller);

        await Assert.ThrowsAsync<ArgumentNullException>(() => controller.SendCommand(null!));
    }

    [Fact]
    [DisplayName("SendCommand_空编码_抛出异常")]
    public async Task SendCommand_EmptyCode_Throws()
    {
        var (sp, _, _, _) = CreateMockServices();
        var controller = new TestDeviceController(sp);
        SetupControllerContext(controller);

        var model = new CommandInModel { Code = "", Command = "restart" };
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => controller.SendCommand(model));
        Assert.Contains("编码", ex.Message);
    }

    [Fact]
    [DisplayName("SendCommand_空命令_抛出异常")]
    public async Task SendCommand_EmptyCommand_Throws()
    {
        var (sp, _, _, _) = CreateMockServices();
        var controller = new TestDeviceController(sp);
        SetupControllerContext(controller);

        var model = new CommandInModel { Code = "dev-001", Command = "" };
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => controller.SendCommand(model));
        Assert.Contains("命令", ex.Message);
    }
    #endregion

    #region PostEvents
    [Fact]
    [DisplayName("PostEvents_上报事件_返回数量")]
    public void PostEvents_Success()
    {
        var (sp, mockDevice, _, _) = CreateMockServices();
        mockDevice.Setup(e => e.PostEvents(It.IsAny<DeviceContext>(), It.IsAny<EventModel[]>()))
            .Returns(2);

        var controller = new TestDeviceController(sp);
        SetupControllerContext(controller);

        var events = new[]
        {
            new EventModel { Name = "cpu_high", Remark = "95" },
            new EventModel { Name = "disk_full", Remark = "98" },
        };

        var result = controller.PostEvents(events);

        Assert.Equal(2, result);
    }

    [Fact]
    [DisplayName("PostEvents_空数组_返回0")]
    public void PostEvents_EmptyArray()
    {
        var (sp, mockDevice, _, _) = CreateMockServices();
        mockDevice.Setup(e => e.PostEvents(It.IsAny<DeviceContext>(), It.IsAny<EventModel[]>()))
            .Returns(0);

        var controller = new TestDeviceController(sp);
        SetupControllerContext(controller);

        var result = controller.PostEvents([]);

        Assert.Equal(0, result);
    }
    #endregion

    #region Notify
    [Fact]
    [DisplayName("Notify_非WebSocket请求_返回BadRequest")]
    public async Task Notify_NonWebSocket_ReturnsBadRequest()
    {
        var (sp, _, _, _) = CreateMockServices();
        var controller = new TestDeviceController(sp);
        SetupControllerContext(controller);

        var result = await controller.Notify();

        Assert.IsType<BadRequestObjectResult>(result);
        var badRequest = (BadRequestObjectResult)result;
        Assert.Equal("不是WebSocket请求", badRequest.Value);
    }
    #endregion
}
