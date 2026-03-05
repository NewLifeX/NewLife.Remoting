using System;
using System.ComponentModel;
using System.Threading.Tasks;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using Xunit;

namespace XUnitTest;

/// <summary>CommandSession单元测试</summary>
public class CommandSessionTests
{
    [Fact]
    [DisplayName("默认属性")]
    public void DefaultProperties()
    {
        using var session = new CommandSession();

        Assert.Null(session.Code);
        Assert.True(session.Active);
        Assert.Null(session.Log);
        Assert.Null(session.SetOnline);
        Assert.Null(session.Tracer);
    }

    [Fact]
    [DisplayName("属性读写")]
    public void Properties_SetAndGet()
    {
        using var session = new CommandSession
        {
            Code = "device001",
            Tracer = DefaultTracer.Instance
        };

        Assert.Equal("device001", session.Code);
        Assert.Equal(DefaultTracer.Instance, session.Tracer);
    }

    [Fact]
    [DisplayName("HandleAsync默认不做任何事")]
    public async Task HandleAsync_Default()
    {
        using var session = new CommandSession { Code = "test" };

        var cmd = new CommandModel
        {
            Id = 1,
            Command = "restart"
        };

        // 默认实现应完成而不抛异常
        await session.HandleAsync(cmd, null, default);
    }

    [Fact]
    [DisplayName("SetOnline回调")]
    public void SetOnline_Callback()
    {
        var onlineCalled = false;
        var isOnline = false;

        using var session = new CommandSession
        {
            Code = "test",
            SetOnline = online =>
            {
                onlineCalled = true;
                isOnline = online;
            }
        };

        session.SetOnline!(true);
        Assert.True(onlineCalled);
        Assert.True(isOnline);

        session.SetOnline!(false);
        Assert.False(isOnline);
    }
}
