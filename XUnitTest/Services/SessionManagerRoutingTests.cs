using System;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;
using NewLife.Remoting.Services;
using Xunit;

namespace XUnitTest.Services;

/// <summary>SessionManager 集群路由测试</summary>
[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class SessionManagerRoutingTests
{
    private static IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    [Fact(DisplayName = "Get获取已添加的会话")]
    public void Get_ReturnsAddedSession()
    {
        var sp = CreateServiceProvider();
        using var sm = new SessionManager(sp);

        var session = new CommandSession { Code = "dev-route-01" };
        sm.Add(session);

        var result = sm.Get("dev-route-01");
        Assert.NotNull(result);
        Assert.Same(session, result);
    }

    [Fact(DisplayName = "Get获取不存在的会话返回null")]
    public void Get_NonExistentCode_ReturnsNull()
    {
        var sp = CreateServiceProvider();
        using var sm = new SessionManager(sp);

        Assert.Null(sm.Get("nonexistent"));
    }

    [Fact(DisplayName = "Get空字符串返回null")]
    public void Get_EmptyString_ReturnsNull()
    {
        var sp = CreateServiceProvider();
        using var sm = new SessionManager(sp);

        Assert.Null(sm.Get(""));
        Assert.Null(sm.Get(null!));
    }

    [Fact(DisplayName = "Remove不存在的会话不会崩溃")]
    public void Remove_NonExistentSession_NoCrash()
    {
        var sp = CreateServiceProvider();
        using var sm = new SessionManager(sp);

        var session = new CommandSession { Code = "dev-ghost" };
        sm.Remove(session);
        Assert.True(true);
    }

    [Fact(DisplayName = "Remove_null会话不会崩溃")]
    public void Remove_NullSession_NoCrash()
    {
        var sp = CreateServiceProvider();
        using var sm = new SessionManager(sp);

        sm.Remove(null!);
        Assert.True(true);
    }
}
