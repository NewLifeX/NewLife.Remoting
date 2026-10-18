using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Extensions;
using NewLife.Remoting.Extensions.Services;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Serialization;
using Xunit;
using MvcActionFilter = Microsoft.AspNetCore.Mvc.Filters.IActionFilter;
using MvcControllerContext = Microsoft.AspNetCore.Mvc.ControllerContext;

namespace XUnitTest.Extensions.Controllers;

/// <summary>BaseController 单元测试</summary>
/// <remarks>
/// 覆盖令牌验证（缺失/非法/刷新令牌误用/设备禁用）、身份声明映射，
/// 以及 MVC 过滤器前后置（鉴权失败信封、匿名放行、上下文回收）。
/// </remarks>
public class BaseControllerTests
{
    #region 辅助
    /// <summary>测试用令牌设置</summary>
    private class TestTokenSetting : ITokenSetting
    {
        public String TokenSecret { get; set; } = null!;
        public Int32 TokenExpire { get; set; } = 3600;
        public Int32 SessionTimeout { get; set; } = 600;
        public Boolean AutoRegister { get; set; } = true;
    }

    /// <summary>测试用控制器子类。公开受保护的鉴权入口</summary>
    private class TestController : BaseController
    {
        public TestController(IDeviceService? deviceService, ITokenService tokenService, IServiceProvider serviceProvider)
            : base(deviceService, tokenService, serviceProvider) { }

        /// <summary>公开 OnAuthorize 用于测试</summary>
        public new Boolean OnAuthorize(String token, DeviceContext context) => base.OnAuthorize(token, context);

        /// <summary>需要鉴权的动作</summary>
        public void Ping() { }

        /// <summary>匿名动作</summary>
        [AllowAnonymous]
        public void Login() { }
    }

    private const String DeviceCode = "dev-01";
    private const String Secret = "HS256:BaseControllerTests_secret_1234567890";

    /// <summary>创建控制器与令牌服务</summary>
    private static (TestController ctrl, ITokenService token) Create(Boolean hasService = true, Boolean deviceFound = true, Boolean enabled = true)
    {
        var token = new TokenService(new TestTokenSetting { TokenSecret = Secret }, new Mock<ITracer>().Object);

        var device = new Mock<IDeviceModel>();
        device.Setup(e => e.Code).Returns(DeviceCode);
        device.Setup(e => e.Enable).Returns(enabled);
        device.Setup(e => e.Name).Returns("测试设备");

        var deviceService = new Mock<IDeviceService>();
        deviceService.Setup(e => e.QueryDevice(It.IsAny<String>())).Returns(deviceFound ? device.Object : null);

        var services = new ServiceCollection();
        services.AddSingleton<ITokenService>(token);
        services.AddSingleton(new Mock<ITracer>().Object);

        var ctrl = new TestController(hasService ? deviceService.Object : null, token, services.BuildServiceProvider());
        ctrl.ControllerContext = new MvcControllerContext { HttpContext = new DefaultHttpContext() };

        return (ctrl, token);
    }

    /// <summary>创建动作执行上下文</summary>
    private static ActionExecutingContext CreateActionContext(TestController ctrl, String? token, Boolean allowAnonymous)
    {
        var http = ctrl.HttpContext;
        if (!token.IsNullOrEmpty()) http.Request.Headers.Authorization = "Bearer " + token;

        var method = allowAnonymous
            ? typeof(TestController).GetMethod(nameof(TestController.Login))!
            : typeof(TestController).GetMethod(nameof(TestController.Ping))!;

        var descriptor = new ControllerActionDescriptor
        {
            ControllerName = "Test",
            ActionName = method.Name,
            MethodInfo = method,
            ControllerTypeInfo = typeof(TestController).GetTypeInfo(),
            DisplayName = "Test." + method.Name,
        };

        var actionContext = new ActionContext(http, new RouteData(), descriptor);

        return new ActionExecutingContext(actionContext, [], new Dictionary<String, Object?>(), ctrl);
    }

    /// <summary>执行前置过滤器</summary>
    private static ActionExecutingContext ExecuteFilter(TestController ctrl, String? token, Boolean allowAnonymous = false)
    {
        var context = CreateActionContext(ctrl, token, allowAnonymous);
        ((MvcActionFilter)ctrl).OnActionExecuting(context);

        return context;
    }
    #endregion

    [Fact, DisplayName("令牌为空时鉴权不通过")]
    public void Authorize_EmptyToken_ReturnsFalse()
    {
        var (ctrl, _) = Create();

        Assert.False(ctrl.OnAuthorize("", new DeviceContext()));
    }

    [Fact, DisplayName("非法令牌鉴权不通过")]
    public void Authorize_InvalidToken_ReturnsFalse()
    {
        var (ctrl, _) = Create();

        // 令牌无法解码且主体为空，按不通过处理（不抛出解码异常），由过滤器统一收敛为401
        Assert.False(ctrl.OnAuthorize("not-a-token", new DeviceContext()));
    }

    [Fact, DisplayName("非法令牌被过滤器拦截为401")]
    public void Filter_InvalidToken_WritesUnauthorized()
    {
        var (ctrl, _) = Create();

        var context = ExecuteFilter(ctrl, "not-a-token");

        var json = Assert.IsType<JsonResult>(context.Result);
        Assert.Contains("\"code\":401", json.Value.ToJson());
        Assert.Contains("Authentication failed", json.Value.ToJson());
    }

    [Fact, DisplayName("刷新令牌不能作为访问令牌")]
    public void Authorize_RefreshToken_Throws401()
    {
        var (ctrl, token) = Create();
        var tm = token.IssueToken(DeviceCode, "client1");

        var ex = Assert.Throws<ApiException>(() => ctrl.OnAuthorize(tm.RefreshToken, new DeviceContext()));
        Assert.Equal(ApiCode.Unauthorized, ex.Code);
        Assert.Contains("A refresh token cannot be used as an access token", ex.Message);
    }

    [Fact, DisplayName("合法令牌装载设备并映射身份声明")]
    public void Authorize_AccessToken_LoadsDeviceAndClaims()
    {
        var (ctrl, token) = Create();
        var tm = token.IssueToken(DeviceCode, "client1");

        var ctx = new DeviceContext();
        Assert.True(ctrl.OnAuthorize(tm.AccessToken, ctx));

        Assert.Equal(DeviceCode, ctx.Code);
        Assert.Equal("client1", ctx.ClientId);
        Assert.NotNull(ctx.Device);
        Assert.NotNull(ctrl.Jwt);
        Assert.Equal(DeviceCode, ctrl.Jwt.Subject);

        // 身份映射为 ClaimsPrincipal，供 [Authorize]/策略授权使用
        var user = ctrl.HttpContext.User;
        Assert.NotNull(user);
        Assert.Equal(DeviceCode, user.FindFirst("code")?.Value);
        Assert.Equal("client1", user.FindFirst("client_id")?.Value);
    }

    [Fact, DisplayName("设备不存在返回403")]
    public void Authorize_DeviceNotFound_Throws403()
    {
        var (ctrl, token) = Create(deviceFound: false);
        var tm = token.IssueToken(DeviceCode);

        var ex = Assert.Throws<ApiException>(() => ctrl.OnAuthorize(tm.AccessToken, new DeviceContext()));
        Assert.Equal(ApiCode.Forbidden, ex.Code);
        Assert.Contains("Invalid client", ex.Message);
    }

    [Fact, DisplayName("设备被禁用返回403")]
    public void Authorize_DisabledDevice_Throws403()
    {
        var (ctrl, token) = Create(enabled: false);
        var tm = token.IssueToken(DeviceCode);

        var ex = Assert.Throws<ApiException>(() => ctrl.OnAuthorize(tm.AccessToken, new DeviceContext()));
        Assert.Equal(ApiCode.Forbidden, ex.Code);
    }

    [Fact, DisplayName("无令牌请求被过滤器拦截为401信封")]
    public void Filter_WithoutToken_WritesUnauthorized()
    {
        var (ctrl, _) = Create();

        var context = ExecuteFilter(ctrl, null);

        var json = Assert.IsType<JsonResult>(context.Result);
        var text = json.Value.ToJson();
        Assert.Contains("\"code\":401", text);
        Assert.Contains("Authentication failed", text);

        // 设备上下文已暴露到 Items，供下游日志中间件使用
        Assert.NotNull(ctrl.HttpContext.Items[nameof(DeviceContext)]);
    }

    [Fact, DisplayName("匿名标记的动作跳过鉴权")]
    public void Filter_AllowAnonymous_SkipsAuth()
    {
        var (ctrl, _) = Create();

        var context = ExecuteFilter(ctrl, null, true);

        Assert.Null(context.Result);
        Assert.NotNull(ctrl.Context);
    }

    [Fact, DisplayName("合法令牌通过过滤器并写入User")]
    public void Filter_WithToken_Passes()
    {
        var (ctrl, token) = Create();
        var tm = token.IssueToken(DeviceCode, "client1");

        var context = ExecuteFilter(ctrl, tm.AccessToken);

        Assert.Null(context.Result);
        Assert.Equal(DeviceCode, ctrl.Context.Code);
        Assert.Equal(DeviceCode, ctrl.HttpContext.User.FindFirst("code")?.Value);
    }

    [Fact, DisplayName("后置过滤器回收设备上下文")]
    public void Filter_AfterExecuted_ReturnsContext()
    {
        var (ctrl, token) = Create();
        var tm = token.IssueToken(DeviceCode, "client1");

        var executing = ExecuteFilter(ctrl, tm.AccessToken);
        Assert.NotNull(ctrl.Context);

        var executed = new ActionExecutedContext(executing, [], ctrl);
        ((MvcActionFilter)ctrl).OnActionExecuted(executed);

        // 上下文归还对象池：控制器不再持有，HttpContext.Items 也被清理
        Assert.Null(ctrl.Context);
        Assert.False(ctrl.HttpContext.Items.ContainsKey(nameof(DeviceContext)));
    }
}
