using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using NewLife.Remoting;
using NewLife.Remoting.Extensions;
using Xunit;

namespace XUnitTest.Filters;

/// <summary>ApiFilterAttribute单元测试</summary>
public class ApiFilterAttributeTests
{
    [Fact]
    [DisplayName("GetToken从Query获取")]
    public void GetToken_FromQuery()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?Token=test_token_123");

        var token = ApiFilterAttribute.GetToken(context);

        Assert.Equal("test_token_123", token);
    }

    [Fact]
    [DisplayName("GetToken从Authorization头获取")]
    public void GetToken_FromAuthorizationHeader()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["Authorization"] = "Bearer jwt_token_456";

        var token = ApiFilterAttribute.GetToken(context);

        Assert.Equal("jwt_token_456", token);
    }

    [Fact]
    [DisplayName("GetToken从X-Token头获取")]
    public void GetToken_FromXTokenHeader()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Token"] = "x_token_789";

        var token = ApiFilterAttribute.GetToken(context);

        Assert.Equal("x_token_789", token);
    }

    [Fact]
    [DisplayName("GetToken从Cookie获取")]
    public void GetToken_FromCookie()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["Cookie"] = "Token=cookie_token_abc";

        var token = ApiFilterAttribute.GetToken(context);

        Assert.Equal("cookie_token_abc", token);
    }

    [Fact]
    [DisplayName("GetToken优先级Query最高")]
    public void GetToken_QueryTakesPrecedence()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?Token=from_query");
        context.Request.Headers["Authorization"] = "Bearer from_header";

        var token = ApiFilterAttribute.GetToken(context);

        Assert.Equal("from_query", token);
    }

    [Fact]
    [DisplayName("GetToken全空返回空字符串")]
    public void GetToken_AllEmpty_ReturnsEmpty()
    {
        var context = new DefaultHttpContext();

        var token = ApiFilterAttribute.GetToken(context);

        Assert.Equal("", token);
    }

    [Fact]
    [DisplayName("GetToken去除Bearer前缀")]
    public void GetToken_StripsBearerPrefix()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["Authorization"] = "Bearer my_actual_token";

        var token = ApiFilterAttribute.GetToken(context);

        Assert.Equal("my_actual_token", token);
    }

    #region 过滤器行为
    [Fact(DisplayName = "OnActionExecuting注入Token到上下文与参数")]
    public void OnActionExecuting_InjectsToken()
    {
        var http = new DefaultHttpContext();
        http.Request.QueryString = new QueryString("?Token=abc");
        var ctx = CreateExecutingContext(http, new Dictionary<String, Object?> { ["arg"] = 1 });

        new ApiFilterAttribute().OnActionExecuting(ctx);

        Assert.Equal("abc", http.Items["Token"]);
        Assert.Equal("abc", ctx.ActionArguments["token"]);
        Assert.Equal(1, ctx.ActionArguments["arg"]);
    }

    [Fact(DisplayName = "OnActionExecuting不覆盖已有token参数")]
    public void OnActionExecuting_KeepsExplicitToken()
    {
        var http = new DefaultHttpContext();
        http.Request.QueryString = new QueryString("?Token=from_query");
        var ctx = CreateExecutingContext(http, new Dictionary<String, Object?> { ["token"] = "explicit" });

        new ApiFilterAttribute().OnActionExecuting(ctx);

        Assert.Equal("explicit", ctx.ActionArguments["token"]);
    }

    [Fact(DisplayName = "OnActionExecuted包装ObjectResult")]
    public void OnActionExecuted_WrapsObjectResult()
    {
        var ctx = CreateExecutedContext(new DefaultHttpContext());
        ctx.Result = new ObjectResult("data") { StatusCode = 200 };

        new ApiFilterAttribute().OnActionExecuted(ctx);

        var json = Assert.IsType<JsonResult>(ctx.Result);
        Assert.Equal(200, GetProperty(json.Value, "code"));
        Assert.Equal("data", GetProperty(json.Value, "data"));
    }

    [Fact(DisplayName = "OnActionExecuted包装EmptyResult")]
    public void OnActionExecuted_WrapsEmptyResult()
    {
        var ctx = CreateExecutedContext(new DefaultHttpContext());
        ctx.Result = new EmptyResult();

        new ApiFilterAttribute().OnActionExecuted(ctx);

        var json = Assert.IsType<JsonResult>(ctx.Result);
        Assert.Equal(0, GetProperty(json.Value, "code"));
        Assert.NotNull(GetProperty(json.Value, "data"));
    }

    [Fact(DisplayName = "OnActionExecuted将ApiException转换为错误码")]
    public void OnActionExecuted_ApiException_UsesCode()
    {
        var ctx = CreateExecutedContext(new DefaultHttpContext());
        ctx.Exception = new ApiException(401, "未授权");

        new ApiFilterAttribute().OnActionExecuted(ctx);

        var json = Assert.IsType<JsonResult>(ctx.Result);
        Assert.Equal(401, GetProperty(json.Value, "code"));
        Assert.Equal("未授权", GetProperty(json.Value, "message"));
        Assert.True(ctx.ExceptionHandled);
    }

    [Fact(DisplayName = "OnActionExecuted将普通异常转换为500")]
    public void OnActionExecuted_GenericException_Uses500()
    {
        var ctx = CreateExecutedContext(new DefaultHttpContext());
        ctx.Exception = new InvalidOperationException("boom");

        new ApiFilterAttribute().OnActionExecuted(ctx);

        var json = Assert.IsType<JsonResult>(ctx.Result);
        Assert.Equal(500, GetProperty(json.Value, "code"));
        Assert.Equal("boom", GetProperty(json.Value, "message"));
        Assert.True(ctx.ExceptionHandled);
    }

    [Fact(DisplayName = "OnActionExecuted跳过WebSocket请求")]
    public void OnActionExecuted_WebSocket_SkipsWrapping()
    {
        var http = new DefaultHttpContext();
        var ws = new Moq.Mock<IHttpWebSocketFeature>();
        ws.SetupGet(e => e.IsWebSocketRequest).Returns(true);
        http.Features.Set(ws.Object);

        var ctx = CreateExecutedContext(http);
        var result = new ObjectResult("data");
        ctx.Result = result;

        new ApiFilterAttribute().OnActionExecuted(ctx);

        Assert.Same(result, ctx.Result);
    }

    private static ActionExecutingContext CreateExecutingContext(HttpContext http, IDictionary<String, Object?>? args = null)
    {
        var actionContext = new ActionContext(http, new RouteData(), new ControllerActionDescriptor());
        return new ActionExecutingContext(actionContext, [], args ?? new Dictionary<String, Object?>(), new Object());
    }

    private static ActionExecutedContext CreateExecutedContext(HttpContext http)
    {
        var actionContext = new ActionContext(http, new RouteData(), new ControllerActionDescriptor());
        return new ActionExecutedContext(actionContext, [], new Object());
    }

    private static Object? GetProperty(Object? instance, String name) => instance?.GetType().GetProperty(name)?.GetValue(instance);
    #endregion
}
