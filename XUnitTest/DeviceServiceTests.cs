using System;
using NewLife;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using Xunit;

namespace XUnitTest;

/// <summary>设备服务测试</summary>
[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class DeviceServiceTests
{
    #region DeviceContext测试
    [Fact(DisplayName = "设备上下文基本属性")]
    public void DeviceContextBasicProperties()
    {
        var ctx = new DeviceContext
        {
            Code = "test001",
            ClientId = "client001",
            UserHost = "192.168.1.100",
            Token = "test_token"
        };

        Assert.Equal("test001", ctx.Code);
        Assert.Equal("client001", ctx.ClientId);
        Assert.Equal("192.168.1.100", ctx.UserHost);
        Assert.Equal("test_token", ctx.Token);
    }

    [Fact(DisplayName = "设备上下文扩展数据")]
    public void DeviceContextExtendData()
    {
        var ctx = new DeviceContext();
        ctx["key1"] = "value1";
        ctx["key2"] = 123;

        Assert.Equal("value1", ctx["key1"]);
        Assert.Equal(123, ctx["key2"]);
        Assert.Null(ctx["notexist"]);
    }

    [Fact(DisplayName = "设备上下文清除")]
    public void DeviceContextClear()
    {
        var ctx = new DeviceContext
        {
            Code = "test001",
            ClientId = "client001",
            UserHost = "192.168.1.100",
            Token = "test_token"
        };
        ctx["key1"] = "value1";

        ctx.Clear();

        Assert.Null(ctx.Code);
        Assert.Null(ctx.ClientId);
        Assert.Null(ctx.UserHost);
        Assert.Null(ctx.Token);
        Assert.Null(ctx.Device);
        Assert.Null(ctx.Online);
        Assert.Empty(ctx.Items);
    }
    #endregion

    #region DeviceServiceExtensions测试
    [Fact(DisplayName = "扩展方法空服务检查")]
    public void WriteHistoryNullService()
    {
        IDeviceService? service = null;
        var device = new TestDevice { Code = "test001", Name = "测试设备", Enable = true };

        Assert.Throws<ArgumentNullException>(() =>
            service!.WriteHistory(device, "测试", true, "测试内容", "client001", "192.168.1.1"));
    }
    #endregion

    #region 模型测试
    [Fact(DisplayName = "登录请求模型")]
    public void LoginRequestModel()
    {
        var now = DateTime.UtcNow;
        var request = new LoginRequest
        {
            Code = "device001",
            Secret = "password123",
            ClientId = "client001",
            Version = "1.0.0",
            IP = "192.168.1.100",
            UUID = "uuid123",
            Time = now.ToLong()
        };

        Assert.Equal("device001", request.Code);
        Assert.Equal("password123", request.Secret);
        Assert.Equal("client001", request.ClientId);
        Assert.Equal("1.0.0", request.Version);
        Assert.Equal("192.168.1.100", request.IP);
        Assert.Equal("uuid123", request.UUID);
        Assert.True(request.Time > 0);
    }

    [Fact(DisplayName = "登录响应模型")]
    public void LoginResponseModel()
    {
        var now = DateTime.UtcNow;
        var response = new LoginResponse
        {
            Code = "device001",
            Secret = "newsecret",
            Name = "测试设备",
            Token = "jwt_token",
            Expire = 3600,
            ServerTime = now.ToLong()
        };

        Assert.Equal("device001", response.Code);
        Assert.Equal("newsecret", response.Secret);
        Assert.Equal("测试设备", response.Name);
        Assert.Equal("jwt_token", response.Token);
        Assert.Equal(3600, response.Expire);
        Assert.True(response.ServerTime > 0);
        Assert.Equal("测试设备", response.ToString());
    }

    [Fact(DisplayName = "心跳请求模型")]
    public void PingRequestModel()
    {
        var now = DateTime.UtcNow;
        var request = new PingRequest
        {
            Memory = 16UL * 1024 * 1024 * 1024,
            AvailableMemory = 8UL * 1024 * 1024 * 1024,
            CpuRate = 0.5,
            Temperature = 65.5,
            IP = "192.168.1.100",
            Uptime = 3600,
            Time = now.ToLong(),
            Delay = 50
        };

        Assert.Equal(16UL * 1024 * 1024 * 1024, request.Memory);
        Assert.Equal(8UL * 1024 * 1024 * 1024, request.AvailableMemory);
        Assert.Equal(0.5, request.CpuRate);
        Assert.Equal(65.5, request.Temperature);
        Assert.Equal("192.168.1.100", request.IP);
        Assert.Equal(3600, request.Uptime);
        Assert.True(request.Time > 0);
        Assert.Equal(50, request.Delay);
    }

    [Fact(DisplayName = "心跳响应模型")]
    public void PingResponseModel()
    {
        var now = DateTime.UtcNow;
        var response = new PingResponse
        {
            Time = 1000,
            ServerTime = now.ToLong(),
            Period = 60,
            Token = "new_token",
            NewServer = "https://new.server.com",
            Commands = [new CommandModel { Id = 1, Command = "restart" }]
        };

        Assert.Equal(1000, response.Time);
        Assert.True(response.ServerTime > 0);
        Assert.Equal(60, response.Period);
        Assert.Equal("new_token", response.Token);
        Assert.Equal("https://new.server.com", response.NewServer);
        Assert.NotNull(response.Commands);
        Assert.Single(response.Commands);
        Assert.Equal("restart", response.Commands[0].Command);
    }

    [Fact(DisplayName = "命令模型")]
    public void CommandModelTest()
    {
        var now = DateTime.Now;
        var cmd = new CommandModel
        {
            Id = 123,
            Command = "upgrade",
            Argument = "{\"version\":\"2.0\"}",
            StartTime = now,
            Expire = now.AddMinutes(30),
            TraceId = "trace123"
        };

        Assert.Equal(123, cmd.Id);
        Assert.Equal("upgrade", cmd.Command);
        Assert.Equal("{\"version\":\"2.0\"}", cmd.Argument);
        Assert.Equal(now, cmd.StartTime);
        Assert.Equal(now.AddMinutes(30), cmd.Expire);
        Assert.Equal("trace123", cmd.TraceId);
    }

    [Fact(DisplayName = "命令响应模型")]
    public void CommandReplyModelTest()
    {
        var reply = new CommandReplyModel
        {
            Id = 123,
            Status = CommandStatus.已完成,
            Data = "success"
        };

        Assert.Equal(123, reply.Id);
        Assert.Equal(CommandStatus.已完成, reply.Status);
        Assert.Equal("success", reply.Data);
    }
    #endregion

    #region 辅助类
    /// <summary>测试设备</summary>
    private class TestDevice : IDeviceModel
    {
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public bool Enable { get; set; }
    }
    #endregion
}
