using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using Xunit;

namespace XUnitTest.Services;

/// <summary>命令响应总线测试</summary>
[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class CommandResponseBusTests
{
    private static IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    #region 基础属性
    [Fact(DisplayName = "CommandResponseBus基本属性")]
    public void BasicProperties()
    {
        var sp = CreateServiceProvider();
        using var bus = new CommandResponseBus(sp);

        Assert.Equal("CommandReplies", bus.Topic);
        Assert.NotNull(bus.Log);
    }

    [Fact(DisplayName = "CommandResponseBus自定义Topic")]
    public void CustomTopic()
    {
        var sp = CreateServiceProvider();
        using var bus = new CommandResponseBus(sp) { Topic = "MyReplies" };

        Assert.Equal("MyReplies", bus.Topic);
    }
    #endregion

    #region WaitResponseAsync
    [Fact(DisplayName = "WaitResponseAsync_超时为0_直接返回null")]
    public async Task WaitResponseAsync_ZeroTimeout_ReturnsNull()
    {
        var sp = CreateServiceProvider();
        using var bus = new CommandResponseBus(sp);

        var result = await bus.WaitResponseAsync(1, 0, CancellationToken.None);
        Assert.Null(result);
    }

    [Fact(DisplayName = "WaitResponseAsync_超时未收到响应_返回null")]
    public async Task WaitResponseAsync_Timeout_ReturnsNull()
    {
        var sp = CreateServiceProvider();
        using var bus = new CommandResponseBus(sp);

        var result = await bus.WaitResponseAsync(2, 1, CancellationToken.None);
        Assert.Null(result);
    }

    [Fact(DisplayName = "WaitResponseAsync_收到响应_返回正确结果")]
    public async Task WaitResponseAsync_ReceivesResponse_ReturnsReply()
    {
        var sp = CreateServiceProvider();
        using var bus = new CommandResponseBus(sp);

        var expectedReply = new CommandReplyModel
        {
            Id = 3,
            Status = CommandStatus.已完成,
            Data = "success"
        };

        var waitTask = bus.WaitResponseAsync(3, 5, CancellationToken.None);
        await Task.Delay(200);

        await bus.PublishResponseAsync(expectedReply, CancellationToken.None);

        var result = await waitTask;
        Assert.NotNull(result);
        Assert.Equal(3, result.Id);
        Assert.Equal(CommandStatus.已完成, result.Status);
        Assert.Equal("success", result.Data);
    }

    [Fact(DisplayName = "WaitResponseAsync_取消令牌_抛出异常")]
    public async Task WaitResponseAsync_Cancelled_Throws()
    {
        var sp = CreateServiceProvider();
        using var bus = new CommandResponseBus(sp);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => bus.WaitResponseAsync(4, 60, cts.Token));
    }
    #endregion

    #region PublishResponseAsync
    [Fact(DisplayName = "PublishResponseAsync_null参数_抛出ArgumentNullException")]
    public async Task PublishResponseAsync_NullReply_Throws()
    {
        var sp = CreateServiceProvider();
        using var bus = new CommandResponseBus(sp);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => bus.PublishResponseAsync(null!, CancellationToken.None));
    }

    [Fact(DisplayName = "PublishResponseAsync_有效响应_不抛异常")]
    public async Task PublishResponseAsync_ValidReply_NoException()
    {
        var sp = CreateServiceProvider();
        using var bus = new CommandResponseBus(sp);

        var reply = new CommandReplyModel
        {
            Id = 5,
            Status = CommandStatus.错误,
            Data = "device offline"
        };

        var result = await bus.PublishResponseAsync(reply, CancellationToken.None);
        Assert.True(result >= 0);
    }
    #endregion

    #region CleanupExpiredCallbacks
    [Fact(DisplayName = "CleanupExpiredCallbacks_返回非负数")]
    public async Task CleanupExpiredCallbacks_ReturnsNonNegative()
    {
        var sp = CreateServiceProvider();
        using var bus = new CommandResponseBus(sp);

        _ = bus.WaitResponseAsync(100, 1, CancellationToken.None);
        await Task.Delay(1500);

        var count = bus.CleanupExpiredCallbacks();
        Assert.True(count >= 0);
    }
    #endregion
}
