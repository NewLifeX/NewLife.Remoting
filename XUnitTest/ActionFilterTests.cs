using System;
using System.ComponentModel;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest;

/// <summary>IActionFilter单元测试</summary>
public class ActionFilterTests
{
    /// <summary>测试用filter实现</summary>
    private class TestFilter : IActionFilter
    {
        public Boolean ExecutingCalled { get; set; }
        public Boolean ExecutedCalled { get; set; }
        public ControllerContext? LastContext { get; set; }

        public void OnActionExecuting(ControllerContext filterContext)
        {
            ExecutingCalled = true;
            LastContext = filterContext;
        }

        public void OnActionExecuted(ControllerContext filterContext)
        {
            ExecutedCalled = true;
            LastContext = filterContext;
        }
    }

    [Fact]
    [DisplayName("Filter执行前回调")]
    public void OnActionExecuting_Called()
    {
        var filter = new TestFilter();
        var ctx = new ControllerContext
        {
            ActionName = "Test/Hello"
        };

        filter.OnActionExecuting(ctx);

        Assert.True(filter.ExecutingCalled);
        Assert.Equal("Test/Hello", filter.LastContext!.ActionName);
    }

    [Fact]
    [DisplayName("Filter执行后回调")]
    public void OnActionExecuted_Called()
    {
        var filter = new TestFilter();
        var ctx = new ControllerContext
        {
            ActionName = "Test/Hello",
            Result = "result"
        };

        filter.OnActionExecuted(ctx);

        Assert.True(filter.ExecutedCalled);
        Assert.Equal("result", filter.LastContext!.Result);
    }

    [Fact]
    [DisplayName("Filter设置Result中断执行")]
    public void OnActionExecuting_SetResult()
    {
        var filter = new TestFilter();
        var ctx = new ControllerContext
        {
            ActionName = "Test/Hello"
        };

        // 模拟在执行前设置Result来中断执行
        filter.OnActionExecuting(ctx);
        ctx.Result = "intercepted";

        Assert.Equal("intercepted", ctx.Result);
    }
}
