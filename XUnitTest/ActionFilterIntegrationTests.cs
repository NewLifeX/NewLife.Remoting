using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest.Remoting;

/// <summary>ActionFilter管线集成测试</summary>
/// <remarks>
/// 验证 IActionFilter 在真实 RPC 调用管线中的执行：
/// OnActionExecuting / OnActionExecuted 调用顺序、异常拦截、结果替换等。
/// </remarks>
public class ActionFilterIntegrationTests : DisposeBase
{
    private readonly ApiServer _Server;
    private readonly Int32 _Port;

    public ActionFilterIntegrationTests()
    {
        _Server = new ApiServer(0)
        {
            Log = XTrace.Log,
            ShowError = true,
        };
        _Server.Register<FilterTestController>();
        _Server.Start();

        _Port = _Server.Port;
    }

    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        _Server.TryDispose();
    }

    #region 过滤器执行顺序
    [Fact(DisplayName = "ActionFilter_Executing和Executed按顺序调用")]
    public async Task FilterExecutionOrderTest()
    {
        FilterTestController.ExecutionLog.Clear();
        FilterTestController.CallCount = 0;

        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var result = await client.InvokeAsync<String>("FilterTest/Hello", new { name = "World" });
        Assert.Equal("Hello, World!", result);

        // 验证调用顺序：Executing → Action → Executed
        Assert.Equal(3, FilterTestController.ExecutionLog.Count);
        Assert.Equal("OnActionExecuting", FilterTestController.ExecutionLog[0]);
        Assert.Equal("Action:Hello", FilterTestController.ExecutionLog[1]);
        Assert.Equal("OnActionExecuted", FilterTestController.ExecutionLog[2]);
    }
    #endregion

    #region 过滤器拦截
    [Fact(DisplayName = "ActionFilter_Executing可以短路请求")]
    public async Task FilterShortCircuitTest()
    {
        FilterTestController.ExecutionLog.Clear();
        FilterTestController.ShouldShortCircuit = true;
        FilterTestController.CallCount = 0;

        try
        {
            using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

            var result = await client.InvokeAsync<String>("FilterTest/Hello", new { name = "World" });
            // 当 Executing 设置了 Result，动作不执行，Executed 仍被调用
            Assert.Equal("Blocked", result);
            Assert.Equal(0, FilterTestController.CallCount);

            Assert.Equal(2, FilterTestController.ExecutionLog.Count);
            Assert.Equal("OnActionExecuting:ShortCircuit", FilterTestController.ExecutionLog[0]);
            Assert.Equal("OnActionExecuted", FilterTestController.ExecutionLog[1]);
        }
        finally
        {
            FilterTestController.ShouldShortCircuit = false;
        }
    }
    #endregion

    #region 过滤器异常处理
    [Fact(DisplayName = "ActionFilter_Executed可以处理异常")]
    public async Task FilterHandleExceptionTest()
    {
        FilterTestController.ExecutionLog.Clear();
        FilterTestController.ShouldHandleException = true;
        FilterTestController.CallCount = 0;

        try
        {
            using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

            // ThrowError 会抛异常，但过滤器标记了 ExceptionHandled
            var result = await client.InvokeAsync<String>("FilterTest/ThrowError");
            Assert.Equal("ErrorHandled", result);
        }
        finally
        {
            FilterTestController.ShouldHandleException = false;
        }
    }

    [Fact(DisplayName = "ActionFilter_Executed不处理异常时抛出")]
    public async Task FilterNoHandleExceptionTest()
    {
        FilterTestController.ExecutionLog.Clear();
        FilterTestController.ShouldHandleException = false;

        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => client.InvokeAsync<String>("FilterTest/ThrowError"));

        Assert.Equal(500, ex.Code);
    }
    #endregion

    #region 过滤器修改结果
    [Fact(DisplayName = "ActionFilter_Executed可以修改返回结果")]
    public async Task FilterModifyResultTest()
    {
        FilterTestController.ExecutionLog.Clear();
        FilterTestController.ShouldModifyResult = true;

        try
        {
            using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

            var result = await client.InvokeAsync<String>("FilterTest/Hello", new { name = "World" });
            // Executed 过滤器将结果修改为前缀 "Modified:"
            Assert.Equal("Modified:Hello, World!", result);
        }
        finally
        {
            FilterTestController.ShouldModifyResult = false;
        }
    }
    #endregion

    #region 辅助类
    class FilterTestController : IActionFilter
    {
        public static List<String> ExecutionLog { get; } = new();
        public static Int32 CallCount;
        public static Boolean ShouldShortCircuit;
        public static Boolean ShouldHandleException;
        public static Boolean ShouldModifyResult;

        public String Hello(String name)
        {
            Interlocked.Increment(ref CallCount);
            ExecutionLog.Add("Action:Hello");
            return $"Hello, {name}!";
        }

        public String ThrowError()
        {
            ExecutionLog.Add("Action:ThrowError");
            throw new InvalidOperationException("测试异常");
        }

        public void OnActionExecuting(ControllerContext filterContext)
        {
            if (ShouldShortCircuit)
            {
                ExecutionLog.Add("OnActionExecuting:ShortCircuit");
                filterContext.Result = "Blocked";
                return;
            }

            ExecutionLog.Add("OnActionExecuting");
        }

        public void OnActionExecuted(ControllerContext filterContext)
        {
            ExecutionLog.Add("OnActionExecuted");

            if (filterContext.Exception != null && ShouldHandleException)
            {
                filterContext.ExceptionHandled = true;
                filterContext.Result = "ErrorHandled";
                filterContext.Exception = null;
            }

            if (ShouldModifyResult && filterContext.Result is String s)
            {
                filterContext.Result = "Modified:" + s;
            }
        }
    }
    #endregion
}
