using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Extensions;
using NewLife.Remoting.Extensions.Models;
using NewLife.Remoting.Extensions.Services;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Serialization;
using NewLife.Web;
using Xunit;
using MvcControllerContext = Microsoft.AspNetCore.Mvc.ControllerContext;

namespace XUnitTest.Extensions.Controllers;

/// <summary>BaseOAuthController 单元测试</summary>
/// <remarks>
/// 覆盖 OAuth 令牌端点的密码模式、自动注册、刷新令牌与访问令牌误用拒绝，
/// 以及 Info 端点对令牌主体与应用的解析。
/// </remarks>
public class BaseOAuthControllerTests
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

    /// <summary>测试用应用</summary>
    private class TestApp : IAppModel
    {
        public String Name { get; set; } = null!;
        public Boolean Enable { get; set; } = true;
        public String? Secret { get; set; }

        /// <summary>写日志次数</summary>
        public Int32 LogCount { get; private set; }

        public Boolean Authorize(String? password, String? ip = null) => Secret.IsNullOrEmpty() || password == Secret;

        public void WriteLog(String action, Boolean success, String message, String? ip, String? clientId) => LogCount++;
    }

    /// <summary>测试用 OAuth 控制器</summary>
    private class TestOAuthController : BaseOAuthController
    {
        private readonly Dictionary<String, IAppModel> _apps = [];

        /// <summary>已注册的应用名</summary>
        public List<String> Registered { get; } = [];

        /// <summary>令牌设置。测试中可动态调整自动注册开关</summary>
        public ITokenSetting Setting { get; }

        public TestOAuthController(ITokenService tokenService, ITokenSetting setting) : base(tokenService, setting) => Setting = setting;

        /// <summary>预置应用</summary>
        /// <param name="app">应用</param>
        public void AddApp(IAppModel app) => _apps[app.Name] = app;

        protected override IAppModel FindByName(String username) => !username.IsNullOrEmpty() && _apps.TryGetValue(username, out var app) ? app : null!;

        protected override IAppModel Register(String username, String? password, Boolean autoRegister, String? ip)
        {
            // 与星尘/IoTZero 一致：未开启自动注册时返回空，上层给出 404
            if (!autoRegister) return null!;

            var app = new TestApp { Name = username, Secret = password };
            _apps[username] = app;
            Registered.Add(username);

            return app;
        }
    }

    private const String Secret = "HS256:BaseOAuthControllerTests_secret_1234";

    /// <summary>创建控制器与令牌服务</summary>
    private static (TestOAuthController ctrl, ITokenService token) Create()
    {
        var setting = new TestTokenSetting { TokenSecret = Secret };
        var token = new TokenService(setting, new Mock<ITracer>().Object);

        var ctrl = new TestOAuthController(token, setting)
        {
            ControllerContext = new MvcControllerContext { HttpContext = new DefaultHttpContext() },
        };

        return (ctrl, token);
    }
    #endregion

    [Fact, DisplayName("密码模式颁发访问令牌与刷新令牌")]
    public void Token_Password_IssuesToken()
    {
        var (ctrl, _) = Create();
        ctrl.AddApp(new TestApp { Name = "app1", Secret = "pwd" });

        var rs = ctrl.Token(new TokenInModel { UserName = "app1", Password = "pwd" });

        Assert.False(rs.AccessToken.IsNullOrEmpty());
        Assert.False(rs.RefreshToken.IsNullOrEmpty());
        Assert.True(rs.ExpireIn > 0);
    }

    [Fact, DisplayName("密码错误返回401")]
    public void Token_WrongPassword_Throws401()
    {
        var (ctrl, _) = Create();
        ctrl.AddApp(new TestApp { Name = "app1", Secret = "pwd" });

        var ex = Assert.Throws<ApiException>(() => ctrl.Token(new TokenInModel { UserName = "app1", Password = "bad" }));
        Assert.Equal(ApiCode.Unauthorized, ex.Code);
    }

    [Fact, DisplayName("应用不存在时按自动注册创建")]
    public void Token_AutoRegister_Registers()
    {
        var (ctrl, _) = Create();

        var rs = ctrl.Token(new TokenInModel { UserName = "app2", Password = "pwd" });

        Assert.False(rs.AccessToken.IsNullOrEmpty());
        Assert.Contains("app2", ctrl.Registered);
    }

    [Fact, DisplayName("关闭自动注册时应用不存在返回404")]
    public void Token_WithoutAutoRegister_Throws404()
    {
        var (ctrl, _) = Create();
        ((TestTokenSetting)ctrl.Setting).AutoRegister = false;

        var ex = Assert.Throws<ApiException>(() => ctrl.Token(new TokenInModel { UserName = "app3", Password = "pwd" }));
        Assert.Equal(ApiCode.NotFound, ex.Code);
    }

    [Fact, DisplayName("刷新令牌换取新的访问令牌")]
    public void Token_RefreshToken_IssuesNew()
    {
        var (ctrl, token) = Create();
        ctrl.AddApp(new TestApp { Name = "app1" });
        var tm = token.IssueToken("app1", "client1");

        var rs = ctrl.Token(new TokenInModel { grant_type = "refresh_token", refresh_token = tm.RefreshToken });

        Assert.False(rs.AccessToken.IsNullOrEmpty());
        Assert.NotEqual(tm.RefreshToken, rs.AccessToken);
    }

    [Fact, DisplayName("访问令牌不能作为刷新令牌")]
    public void Token_AccessTokenAsRefresh_Rejected()
    {
        var (ctrl, token) = Create();
        ctrl.AddApp(new TestApp { Name = "app1" });
        var tm = token.IssueToken("app1", "client1");

        var ex = Assert.Throws<ApiException>(() => ctrl.Token(new TokenInModel { grant_type = "refresh_token", refresh_token = tm.AccessToken }));
        Assert.Equal(ApiCode.Unauthorized, ex.Code);
        Assert.Contains("访问令牌不能作为刷新令牌使用", ex.Message);
    }

    [Fact, DisplayName("未支持的授权类型抛出NotSupported")]
    public void Token_UnsupportedGrantType_Throws()
    {
        var (ctrl, _) = Create();

        Assert.Throws<NotSupportedException>(() => ctrl.Token(new TokenInModel { grant_type = "unknown" }));
    }

    [Fact, DisplayName("缺少用户名抛出参数异常")]
    public void Token_WithoutUserName_Throws()
    {
        var (ctrl, _) = Create();

        Assert.Throws<ArgumentNullException>(() => ctrl.Token(new TokenInModel { grant_type = "password" }));
    }

    [Fact, DisplayName("Info按令牌主体返回应用")]
    public void Info_ValidToken_ReturnsApp()
    {
        var (ctrl, token) = Create();
        ctrl.AddApp(new TestApp { Name = "app1" });
        var tm = token.IssueToken("app1", "client1");

        var rs = ctrl.Info(tm.AccessToken!);

        Assert.Contains("app1", rs.ToJson());
    }

    [Fact, DisplayName("Info遇到非法令牌返回401")]
    public void Info_InvalidToken_Throws()
    {
        var (ctrl, _) = Create();

        var ex = Assert.Throws<ApiException>(() => ctrl.Info("not-a-token"));
        Assert.Equal(ApiCode.Unauthorized, ex.Code);
    }
}
