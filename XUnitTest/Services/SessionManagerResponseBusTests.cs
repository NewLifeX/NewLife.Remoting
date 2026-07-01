using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using Xunit;

namespace XUnitTest.Services;

/// <summary>SessionManager 响应广播集成测试</summary>
[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class SessionManagerResponseBusTests
{
    private static IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    [Fact(DisplayName = "单实例内发布和订阅_响应正确匹配")]
    public async Task SingleInstance_PublishAndSubscribe_Matches()
    {
        var sp = CreateServiceProvider();
        using var bus = new CommandResponseBus(sp);

        var reply = new CommandReplyModel { Id = 100, Status = CommandStatus.已完成, Data = "ok" };

        var waitTask = bus.WaitResponseAsync(100, 5, CancellationToken.None);
        await Task.Delay(100);

        await bus.PublishResponseAsync(reply, CancellationToken.None);

        var result = await waitTask;
        Assert.NotNull(result);
        Assert.Equal(100, result.Id);
        Assert.Equal("ok", result.Data);
    }

    [Fact(DisplayName = "内存模式不同实例Topic独立_无法跨实例通信")]
    public async Task MemoryMode_DifferentInstances_Independent()
    {
        // 内存模式下，两个 CommandResponseBus 的 EventBus 是独立的
        // 跨实例通信需要共享 Redis EventBus（通过 IEventBusFactory 注入）
        var sp = CreateServiceProvider();
        using var busA = new CommandResponseBus(sp) { Topic = "Independent" };
        using var busB = new CommandResponseBus(sp) { Topic = "Independent" };

        var waitTask = busA.WaitResponseAsync(200, 1, CancellationToken.None);

        // busB 发布，busA 不应收到（内存模式独立 EventBus）
        await busB.PublishResponseAsync(
            new CommandReplyModel { Id = 200, Status = CommandStatus.已完成 },
            CancellationToken.None);

        // busA 超时返回 null
        var result = await waitTask;
        Assert.Null(result);
    }

    [Fact(DisplayName = "无匹配回调时发布不崩溃")]
    public async Task NoMatchingCallback_NoCrash()
    {
        var sp = CreateServiceProvider();
        using var bus = new CommandResponseBus(sp);

        await bus.PublishResponseAsync(
            new CommandReplyModel { Id = 77777, Status = CommandStatus.已完成 },
            CancellationToken.None);

        Assert.True(true);
    }

    [Fact(DisplayName = "路由字段在广播中正确传递")]
    public async Task RouteFields_PreservedInBroadcast()
    {
        var sp = CreateServiceProvider();
        using var bus = new CommandResponseBus(sp);

        var reply = new CommandReplyModel
        {
            Id = 500,
            Status = CommandStatus.已送达,
            Data = "delivered",
            Code = "device-route",
            SenderNodeId = "node-A"
        };

        var waitTask = bus.WaitResponseAsync(500, 3, CancellationToken.None);
        await Task.Delay(100);
        await bus.PublishResponseAsync(reply, CancellationToken.None);

        var result = await waitTask;
        Assert.NotNull(result);
        Assert.Equal("device-route", result.Code);
        Assert.Equal("node-A", result.SenderNodeId);
    }
}
