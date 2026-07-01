using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;
using NewLife.Remoting.Models;
using Xunit;

namespace XUnitTest.Services;

/// <summary>SSE 命令会话测试</summary>
[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class SseCommandSessionTests
{
    private static HttpContext CreateHttpContext()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var context = new DefaultHttpContext
        {
            RequestServices = sp,
            Response = { Body = new System.IO.MemoryStream() }
        };
        return context;
    }

    [Fact(DisplayName = "SseCommandSession基本属性")]
    public void BasicProperties()
    {
        var ctx = CreateHttpContext();
        var sp = ctx.RequestServices;
        using var session = new NewLife.Remoting.Extensions.Services.SseCommandSession(
            ctx.Response, "dev-sse-01", sp);

        Assert.Equal("dev-sse-01", session.Code);
        Assert.Equal(30, session.HeartbeatInterval);
        Assert.True(session.Active);
    }

    [Fact(DisplayName = "SseCommandSession自定义心跳间隔")]
    public void CustomHeartbeatInterval()
    {
        var ctx = CreateHttpContext();
        var sp = ctx.RequestServices;
        using var session = new NewLife.Remoting.Extensions.Services.SseCommandSession(
            ctx.Response, "dev-sse-02", sp)
        {
            HeartbeatInterval = 60
        };

        Assert.Equal(60, session.HeartbeatInterval);
    }

    [Fact(DisplayName = "SseCommandSession_HandleAsync发送SSE格式命令")]
    public async Task HandleAsync_SendsSseFormattedCommand()
    {
        var ctx = CreateHttpContext();
        var sp = ctx.RequestServices;
        using var session = new NewLife.Remoting.Extensions.Services.SseCommandSession(
            ctx.Response, "dev-sse-03", sp);

        var cmd = new CommandModel
        {
            Id = 1,
            Command = "Restart",
            Argument = "-f"
        };

        await session.HandleAsync(cmd, null, CancellationToken.None);

        // 读取 SSE 输出
        ctx.Response.Body.Position = 0;
        var reader = new System.IO.StreamReader(ctx.Response.Body);
        var output = await reader.ReadToEndAsync();

        Assert.Contains("event: command", output);
        Assert.Contains("Restart", output);
    }

    [Fact(DisplayName = "SseCommandSession_HandleAsync传入message直接发送")]
    public async Task HandleAsync_WithMessage_SendsDirectly()
    {
        var ctx = CreateHttpContext();
        var sp = ctx.RequestServices;
        using var session = new NewLife.Remoting.Extensions.Services.SseCommandSession(
            ctx.Response, "dev-sse-04", sp);

        var message = "{\"Id\":2,\"Command\":\"Update\"}";

        await session.HandleAsync(null!, message, CancellationToken.None);

        ctx.Response.Body.Position = 0;
        var reader = new System.IO.StreamReader(ctx.Response.Body);
        var output = await reader.ReadToEndAsync();

        Assert.Contains("event: command", output);
        Assert.Contains("Update", output);
    }
}
