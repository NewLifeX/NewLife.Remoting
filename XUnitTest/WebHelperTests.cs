using System;
using System.ComponentModel;
using System.Net;
using Microsoft.AspNetCore.Http;
using NewLife.Remoting.Extensions;
using Xunit;

namespace XUnitTest;

/// <summary>WebHelper单元测试</summary>
public class WebHelperTests
{
    [Fact]
    [DisplayName("GetUserHost从X-Real-IP获取")]
    public void GetUserHost_XRealIP()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Real-IP"] = "10.0.0.1";

        var host = context.GetUserHost();

        Assert.Equal("10.0.0.1", host);
    }

    [Fact]
    [DisplayName("GetUserHost从X-Forwarded-For获取")]
    public void GetUserHost_XForwardedFor()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Forwarded-For"] = "192.168.1.100";

        var host = context.GetUserHost();

        Assert.Equal("192.168.1.100", host);
    }

    [Fact]
    [DisplayName("GetUserHost从X-Remote-Ip获取")]
    public void GetUserHost_XRemoteIp()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Remote-Ip"] = "172.16.0.1";

        var host = context.GetUserHost();

        Assert.Equal("172.16.0.1", host);
    }

    [Fact]
    [DisplayName("GetUserHost优先X-Remote-Ip")]
    public void GetUserHost_Priority()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Remote-Ip"] = "10.0.0.1";
        context.Request.Headers["X-Real-IP"] = "10.0.0.2";
        context.Request.Headers["X-Forwarded-For"] = "10.0.0.3";

        var host = context.GetUserHost();

        Assert.Equal("10.0.0.1", host);
    }

    [Fact]
    [DisplayName("GetUserHost从RemoteIp")]
    public void GetUserHost_FromRemoteIp()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");

        var host = context.GetUserHost();

        Assert.Equal("127.0.0.1", host);
    }

    [Fact]
    [DisplayName("GetUserHost全空返回空字符串")]
    public void GetUserHost_AllEmpty()
    {
        var context = new DefaultHttpContext();

        var host = context.GetUserHost();

        Assert.Equal("", host);
    }

    [Fact]
    [DisplayName("GetUserHost_IPv6映射")]
    public void GetUserHost_IPv6Mapped()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:192.168.1.1");

        var host = context.GetUserHost();

        Assert.Equal("192.168.1.1", host);
    }

    [Fact]
    [DisplayName("GetRawUrl基础请求")]
    public void GetRawUrl_BasicRequest()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost", 8080);
        context.Request.Path = "/api/test";

        var uri = context.Request.GetRawUrl();

        Assert.Contains("localhost", uri.ToString());
        Assert.Contains("/api/test", uri.ToString());
    }

    [Fact]
    [DisplayName("GetRawUrl带Scheme转换")]
    public void GetRawUrl_WithSchemeOverride()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost");
        context.Request.Path = "/api/test";
        context.Request.Headers["X-Forwarded-Proto"] = "https";

        var uri = context.Request.GetRawUrl();

        Assert.StartsWith("https://", uri.ToString());
    }
}
