using System;
using System.ComponentModel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using NewLife.Remoting.Extensions;
using Xunit;

namespace XUnitTest;

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
}
