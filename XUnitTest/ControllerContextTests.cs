using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest;

public class ControllerContextTests
{
    [Fact]
    [DisplayName("默认构造函数")]
    public void DefaultConstructor()
    {
        var ctx = new ControllerContext();

        Assert.Null(ctx.Controller);
        Assert.Null(ctx.Action);
        Assert.Null(ctx.ActionName);
        Assert.Null(ctx.Session);
        Assert.Null(ctx.Request);
        Assert.Null(ctx.Parameters);
        Assert.Null(ctx.ActionParameters);
        Assert.Null(ctx.Result);
        Assert.Null(ctx.Exception);
        Assert.False(ctx.ExceptionHandled);
    }

    [Fact]
    [DisplayName("属性读写")]
    public void Properties_SetAndGet()
    {
        var ctx = new ControllerContext
        {
            Controller = new Object(),
            ActionName = "Test/Action",
            Request = "some request",
            Parameters = new Dictionary<String, Object?> { ["key"] = "value" },
            ActionParameters = new Dictionary<String, Object?> { ["p1"] = 42 },
            Result = "result data",
            Exception = new InvalidOperationException("test"),
            ExceptionHandled = true
        };

        Assert.NotNull(ctx.Controller);
        Assert.Equal("Test/Action", ctx.ActionName);
        Assert.Equal("some request", ctx.Request);
        Assert.Single(ctx.Parameters!);
        Assert.Equal(42, ctx.ActionParameters!["p1"]);
        Assert.Equal("result data", ctx.Result);
        Assert.IsType<InvalidOperationException>(ctx.Exception);
        Assert.True(ctx.ExceptionHandled);
    }

    [Fact]
    [DisplayName("Reset清空所有属性")]
    public void Reset_ClearsAllProperties()
    {
        var ctx = new ControllerContext
        {
            Controller = new Object(),
            ActionName = "Test/Action",
            Request = "req",
            Parameters = new Dictionary<String, Object?> { ["k"] = "v" },
            ActionParameters = new Dictionary<String, Object?> { ["p"] = 1 },
            Result = "res",
            Exception = new Exception("test"),
            ExceptionHandled = true
        };

        ctx.Reset();

        Assert.Null(ctx.Controller);
        Assert.Null(ctx.Action);
        Assert.Null(ctx.ActionName);
        Assert.Null(ctx.Session);
        Assert.Null(ctx.Request);
        Assert.Null(ctx.Parameters);
        Assert.Null(ctx.ActionParameters);
        Assert.Null(ctx.Result);
        Assert.Null(ctx.Exception);
        Assert.False(ctx.ExceptionHandled);
    }

    [Fact]
    [DisplayName("Current线程静态")]
    public void Current_ThreadStatic()
    {
        ControllerContext.Current = null;
        Assert.Null(ControllerContext.Current);

        var ctx = new ControllerContext { ActionName = "test" };
        ControllerContext.Current = ctx;

        Assert.NotNull(ControllerContext.Current);
        Assert.Equal("test", ControllerContext.Current!.ActionName);

        // 清理
        ControllerContext.Current = null;
    }

    [Fact]
    [DisplayName("多次Reset")]
    public void Reset_MultipleTimes()
    {
        var ctx = new ControllerContext
        {
            ActionName = "test",
            Result = "result"
        };

        ctx.Reset();
        ctx.Reset();

        Assert.Null(ctx.ActionName);
        Assert.Null(ctx.Result);
    }
}
