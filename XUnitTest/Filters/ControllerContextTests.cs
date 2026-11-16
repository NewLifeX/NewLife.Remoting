using System;
using System.ComponentModel;
using System.Threading.Tasks;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest.Filters;

/// <summary>控制器上下文的流动与隔离契约</summary>
public class ControllerContextTests
{
    [Fact]
    [DisplayName("上下文跟随异步流程流动，await 之后仍可读取")]
    public async Task Current_FlowsAcrossAwait()
    {
        var ctx = new ControllerContext();
        ControllerContext.Current = ctx;
        try
        {
            await Task.Yield();

            Assert.Same(ctx, ControllerContext.Current);
        }
        finally
        {
            ControllerContext.Current = null;
        }
    }

    [Fact]
    [DisplayName("并行请求的上下文互不可见")]
    public async Task Current_IsolatedBetweenFlows()
    {
        var tasks = new Task[20];
        for (var i = 0; i < tasks.Length; i++)
        {
            var index = i;
            tasks[i] = Task.Run(async () =>
            {
                var ctx = new ControllerContext { ActionName = "A" + index };
                ControllerContext.Current = ctx;

                // 切线程后再读，线程静态实现会在这里读到 null 或别的请求
                await Task.Delay(5);

                Assert.Same(ctx, ControllerContext.Current);
                Assert.Equal("A" + index, ControllerContext.Current!.ActionName);
                Assert.DoesNotContain("A", ControllerContext.Current.Items.Keys);
            });
        }

        await Task.WhenAll(tasks);
    }

    [Fact]
    [DisplayName("重置会清空扩展数据")]
    public void Reset_ClearsItems()
    {
        var ctx = new ControllerContext();
        ctx.Items["k"] = 1;

        ctx.Reset();

        Assert.Empty(ctx.Items);
        Assert.Null(ctx.ActionName);
    }
}
