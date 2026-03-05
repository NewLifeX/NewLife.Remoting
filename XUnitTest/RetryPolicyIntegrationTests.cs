using System;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest.Remoting;

/// <summary>重试策略集成测试</summary>
/// <remarks>
/// 验证 ApiClient.RetryPolicy 在实际 RPC 调用中的行为，
/// 包括重试次数、延迟、更换连接、不重试等场景。
/// </remarks>
public class RetryPolicyIntegrationTests : DisposeBase
{
    private readonly ApiServer _Server;
    private readonly Int32 _Port;

    public RetryPolicyIntegrationTests()
    {
        _Server = new ApiServer(0)
        {
            Log = XTrace.Log,
            ShowError = true,
        };
        _Server.Register<RetryTestController>();
        _Server.Start();

        _Port = _Server.Port;
    }

    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        _Server.TryDispose();
    }

    #region 重试成功
    [Fact(DisplayName = "重试策略_间歇性失败后成功")]
    public async Task RetrySuccessAfterTransientFailureTest()
    {
        // 控制器前2次调用抛异常，第3次成功
        RetryTestController.FailCount = 2;
        RetryTestController.CallCount = 0;

        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}")
        {
            RetryPolicy = new SimpleRetryPolicy(),
            MaxRetries = 3,
        };

        var result = await client.InvokeAsync<String>("RetryTest/TransientAction");
        Assert.Equal("Success", result);
        Assert.Equal(3, RetryTestController.CallCount);
    }

    [Fact(DisplayName = "重试策略_首次成功不触发重试")]
    public async Task NoRetryWhenSuccessTest()
    {
        RetryTestController.FailCount = 0;
        RetryTestController.CallCount = 0;

        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}")
        {
            RetryPolicy = new SimpleRetryPolicy(),
            MaxRetries = 3,
        };

        var result = await client.InvokeAsync<String>("RetryTest/TransientAction");
        Assert.Equal("Success", result);
        Assert.Equal(1, RetryTestController.CallCount);
    }
    #endregion

    #region 重试耗尽
    [Fact(DisplayName = "重试策略_重试次数耗尽抛出异常")]
    public async Task RetryExhaustedTest()
    {
        RetryTestController.FailCount = 100; // 永远失败
        RetryTestController.CallCount = 0;

        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}")
        {
            RetryPolicy = new SimpleRetryPolicy(),
            MaxRetries = 2,
        };

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => client.InvokeAsync<String>("RetryTest/TransientAction"));

        Assert.Equal(500, ex.Code);
        // 首次 + 2次重试 = 3次
        Assert.Equal(3, RetryTestController.CallCount);
    }
    #endregion

    #region 不重试
    [Fact(DisplayName = "重试策略_MaxRetries为0不重试")]
    public async Task NoRetryWhenMaxRetriesZeroTest()
    {
        RetryTestController.FailCount = 100;
        RetryTestController.CallCount = 0;

        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}")
        {
            RetryPolicy = new SimpleRetryPolicy(),
            MaxRetries = 0, // 不重试
        };

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => client.InvokeAsync<String>("RetryTest/TransientAction"));

        Assert.Equal(1, RetryTestController.CallCount);
    }

    [Fact(DisplayName = "重试策略_无策略不重试")]
    public async Task NoRetryWhenNoPolicyTest()
    {
        RetryTestController.FailCount = 100;
        RetryTestController.CallCount = 0;

        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}")
        {
            RetryPolicy = null,
            MaxRetries = 3,
        };

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => client.InvokeAsync<String>("RetryTest/TransientAction"));

        Assert.Equal(1, RetryTestController.CallCount);
    }
    #endregion

    #region 策略选择性重试
    [Fact(DisplayName = "重试策略_只对特定异常重试")]
    public async Task SelectiveRetryTest()
    {
        RetryTestController.FailCount = 100;
        RetryTestController.CallCount = 0;

        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}")
        {
            RetryPolicy = new SelectiveRetryPolicy(), // 不重试ApiException
            MaxRetries = 3,
        };

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => client.InvokeAsync<String>("RetryTest/TransientAction"));

        // 策略拒绝重试，所以只调用1次
        Assert.Equal(1, RetryTestController.CallCount);
    }
    #endregion

    #region 辅助类
    class RetryTestController
    {
        public static Int32 FailCount;
        public static Int32 CallCount;

        public String TransientAction()
        {
            var count = Interlocked.Increment(ref CallCount);
            if (count <= FailCount)
                throw new InvalidOperationException($"间歇性失败: 第{count}次");

            return "Success";
        }
    }

    /// <summary>简单重试策略：对所有异常进行重试，无延迟</summary>
    class SimpleRetryPolicy : IRetryPolicy
    {
        public Boolean ShouldRetry(Exception exception, Int32 attempt, out TimeSpan delay, out Boolean refreshClient)
        {
            delay = TimeSpan.Zero;
            refreshClient = false;
            return true; // 对所有异常都重试
        }
    }

    /// <summary>选择性重试策略：不对 ApiException 重试</summary>
    class SelectiveRetryPolicy : IRetryPolicy
    {
        public Boolean ShouldRetry(Exception exception, Int32 attempt, out TimeSpan delay, out Boolean refreshClient)
        {
            delay = TimeSpan.Zero;
            refreshClient = false;

            // 不对 ApiException 重试
            if (exception is ApiException) return false;
            return true;
        }
    }
    #endregion
}
