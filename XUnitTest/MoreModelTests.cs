using System;
using System.ComponentModel;
using NewLife.Remoting.Models;
using Xunit;

namespace XUnitTest;

/// <summary>更多模型类单元测试</summary>
public class MoreModelTests
{
    #region LoginRequest
    [Fact]
    [DisplayName("LoginRequest默认值")]
    public void LoginRequest_DefaultValues()
    {
        var req = new LoginRequest();

        Assert.Null(req.Code);
        Assert.Null(req.Secret);
        Assert.Null(req.ClientId);
        Assert.Null(req.Version);
        Assert.Equal(0L, req.Compile);
        Assert.Null(req.IP);
        Assert.Null(req.Macs);
        Assert.Null(req.UUID);
        Assert.Equal(0L, req.Time);
    }

    [Fact]
    [DisplayName("LoginRequest属性读写")]
    public void LoginRequest_Properties()
    {
        var req = new LoginRequest
        {
            Code = "device001",
            Secret = "secret123",
            ClientId = "192.168.1.1@1234",
            Version = "1.0.0",
            Compile = 1750000000000,
            IP = "192.168.1.1",
            Macs = "AA-BB-CC-DD-EE-FF",
            UUID = "unique-id-001",
            Time = 1750000000000
        };

        Assert.Equal("device001", req.Code);
        Assert.Equal("secret123", req.Secret);
        Assert.Equal("192.168.1.1@1234", req.ClientId);
        Assert.Equal("1.0.0", req.Version);
        Assert.Equal(1750000000000, req.Compile);
        Assert.Equal("192.168.1.1", req.IP);
        Assert.Equal("AA-BB-CC-DD-EE-FF", req.Macs);
        Assert.Equal("unique-id-001", req.UUID);
        Assert.Equal(1750000000000, req.Time);
    }

    [Fact]
    [DisplayName("LoginRequest实现ILoginRequest接口")]
    public void LoginRequest_ImplementsInterface()
    {
        ILoginRequest req = new LoginRequest();
        Assert.NotNull(req);

        ILoginRequest2 req2 = new LoginRequest();
        Assert.NotNull(req2);
    }
    #endregion

    #region LoginResponse
    [Fact]
    [DisplayName("LoginResponse默认值")]
    public void LoginResponse_DefaultValues()
    {
        var res = new LoginResponse();

        Assert.Null(res.Code);
        Assert.Null(res.Secret);
        Assert.Null(res.Name);
        Assert.Null(res.Token);
        Assert.Equal(0, res.Expire);
        Assert.Equal(0L, res.Time);
        Assert.Equal(0L, res.ServerTime);
    }

    [Fact]
    [DisplayName("LoginResponse属性读写")]
    public void LoginResponse_Properties()
    {
        var res = new LoginResponse
        {
            Code = "device001",
            Secret = "newSecret",
            Name = "测试设备",
            Token = "jwt.token.here",
            Expire = 7200,
            Time = 1750000000000,
            ServerTime = 1750000001000
        };

        Assert.Equal("device001", res.Code);
        Assert.Equal("newSecret", res.Secret);
        Assert.Equal("测试设备", res.Name);
        Assert.Equal("jwt.token.here", res.Token);
        Assert.Equal(7200, res.Expire);
        Assert.Equal(1750000000000, res.Time);
        Assert.Equal(1750000001000, res.ServerTime);
    }

    [Fact]
    [DisplayName("LoginResponse_ToString有Name")]
    public void LoginResponse_ToString_WithName()
    {
        var res = new LoginResponse { Name = "设备A", Code = "dev001" };
        Assert.Equal("设备A", res.ToString());
    }

    [Fact]
    [DisplayName("LoginResponse_ToString无Name用Code")]
    public void LoginResponse_ToString_WithoutName()
    {
        var res = new LoginResponse { Code = "dev001" };
        Assert.Equal("dev001", res.ToString());
    }

    [Fact]
    [DisplayName("LoginResponse实现ILoginResponse接口")]
    public void LoginResponse_ImplementsInterface()
    {
        ILoginResponse res = new LoginResponse();
        Assert.NotNull(res);
    }
    #endregion

    #region PingRequest
    [Fact]
    [DisplayName("PingRequest默认值")]
    public void PingRequest_DefaultValues()
    {
        var req = new PingRequest();

        Assert.Equal(0UL, req.Memory);
        Assert.Equal(0UL, req.AvailableMemory);
        Assert.Equal(0UL, req.FreeMemory);
        Assert.Equal(0UL, req.TotalSize);
        Assert.Equal(0UL, req.AvailableFreeSpace);
        Assert.Equal(0.0, req.CpuRate);
        Assert.Equal(0.0, req.Temperature);
        Assert.Equal(0.0, req.Battery);
        Assert.Equal(0, req.Signal);
        Assert.Null(req.IP);
        Assert.Equal(0, req.Uptime);
        Assert.Equal(0L, req.Time);
        Assert.Equal(0, req.Delay);
    }

    [Fact]
    [DisplayName("PingRequest属性读写")]
    public void PingRequest_Properties()
    {
        var req = new PingRequest
        {
            Memory = 16_000_000_000,
            AvailableMemory = 8_000_000_000,
            FreeMemory = 6_000_000_000,
            TotalSize = 500_000_000_000,
            AvailableFreeSpace = 200_000_000_000,
            CpuRate = 0.35,
            Temperature = 65.5,
            Battery = 85.0,
            Signal = -60,
            UplinkSpeed = 1_000_000,
            DownlinkSpeed = 5_000_000,
            IP = "192.168.1.100",
            Uptime = 86400,
            Time = 1750000000000,
            Delay = 15
        };

        Assert.Equal(16_000_000_000UL, req.Memory);
        Assert.Equal(8_000_000_000UL, req.AvailableMemory);
        Assert.Equal(6_000_000_000UL, req.FreeMemory);
        Assert.Equal(500_000_000_000UL, req.TotalSize);
        Assert.Equal(200_000_000_000UL, req.AvailableFreeSpace);
        Assert.Equal(0.35, req.CpuRate);
        Assert.Equal(65.5, req.Temperature);
        Assert.Equal(85.0, req.Battery);
        Assert.Equal(-60, req.Signal);
        Assert.Equal(1_000_000UL, req.UplinkSpeed);
        Assert.Equal(5_000_000UL, req.DownlinkSpeed);
        Assert.Equal("192.168.1.100", req.IP);
        Assert.Equal(86400, req.Uptime);
        Assert.Equal(1750000000000, req.Time);
        Assert.Equal(15, req.Delay);
    }

    [Fact]
    [DisplayName("PingRequest实现接口")]
    public void PingRequest_ImplementsInterface()
    {
        IPingRequest req = new PingRequest();
        Assert.NotNull(req);

        IPingRequest2 req2 = new PingRequest();
        Assert.NotNull(req2);
    }
    #endregion

    #region PingResponse
    [Fact]
    [DisplayName("PingResponse默认值")]
    public void PingResponse_DefaultValues()
    {
        var res = new PingResponse();

        Assert.Equal(0L, res.Time);
        Assert.Equal(0L, res.ServerTime);
        Assert.Equal(0, res.Period);
        Assert.Null(res.Token);
        Assert.Null(res.NewServer);
        Assert.Null(res.Commands);
    }

    [Fact]
    [DisplayName("PingResponse属性读写")]
    public void PingResponse_Properties()
    {
        var cmds = new[] { new CommandModel { Id = 1, Command = "restart" } };
        var res = new PingResponse
        {
            Time = 1750000000000,
            ServerTime = 1750000001000,
            Period = 60,
            Token = "new_token",
            NewServer = "http://new-server:8080",
            Commands = cmds
        };

        Assert.Equal(1750000000000, res.Time);
        Assert.Equal(1750000001000, res.ServerTime);
        Assert.Equal(60, res.Period);
        Assert.Equal("new_token", res.Token);
        Assert.Equal("http://new-server:8080", res.NewServer);
        Assert.Single(res.Commands!);
        Assert.Equal("restart", res.Commands![0].Command);
    }

    [Fact]
    [DisplayName("PingResponse实现接口")]
    public void PingResponse_ImplementsInterface()
    {
        IPingResponse res = new PingResponse();
        Assert.NotNull(res);

        IPingResponse2 res2 = new PingResponse();
        Assert.NotNull(res2);
    }
    #endregion

    #region CommandModel
    [Fact]
    [DisplayName("CommandModel默认值")]
    public void CommandModel_DefaultValues()
    {
        var cmd = new CommandModel();

        Assert.Equal(0, cmd.Id);
        Assert.Null(cmd.Command);
        Assert.Null(cmd.Argument);
        Assert.Equal(DateTime.MinValue, cmd.StartTime);
        Assert.Equal(DateTime.MinValue, cmd.Expire);
        Assert.Null(cmd.TraceId);
    }

    [Fact]
    [DisplayName("CommandModel属性读写")]
    public void CommandModel_Properties()
    {
        var now = DateTime.UtcNow;
        var cmd = new CommandModel
        {
            Id = 12345,
            Command = "restart",
            Argument = "{\"force\":true}",
            StartTime = now,
            Expire = now.AddHours(1),
            TraceId = "trace-001"
        };

        Assert.Equal(12345, cmd.Id);
        Assert.Equal("restart", cmd.Command);
        Assert.Equal("{\"force\":true}", cmd.Argument);
        Assert.Equal(now, cmd.StartTime);
        Assert.Equal(now.AddHours(1), cmd.Expire);
        Assert.Equal("trace-001", cmd.TraceId);
    }
    #endregion

    #region CommandReplyModel
    [Fact]
    [DisplayName("CommandReplyModel默认值")]
    public void CommandReplyModel_DefaultValues()
    {
        var reply = new CommandReplyModel();

        Assert.Equal(0, reply.Id);
        Assert.Equal(CommandStatus.就绪, reply.Status);
        Assert.Null(reply.Data);
    }

    [Fact]
    [DisplayName("CommandReplyModel属性读写")]
    public void CommandReplyModel_Properties()
    {
        var reply = new CommandReplyModel
        {
            Id = 100,
            Status = CommandStatus.已完成,
            Data = "operation successful"
        };

        Assert.Equal(100, reply.Id);
        Assert.Equal(CommandStatus.已完成, reply.Status);
        Assert.Equal("operation successful", reply.Data);
    }
    #endregion

    #region DeviceContext
    [Fact]
    [DisplayName("DeviceContext默认值")]
    public void DeviceContext_DefaultValues()
    {
        var ctx = new DeviceContext();

        Assert.Null(ctx.Code);
        Assert.Null(ctx.ClientId);
        Assert.Null(ctx.UserHost);
        Assert.Null(ctx.Token);
    }

    [Fact]
    [DisplayName("DeviceContext属性读写")]
    public void DeviceContext_Properties()
    {
        var ctx = new DeviceContext
        {
            Code = "dev001",
            ClientId = "192.168.1.1@5678",
            UserHost = "192.168.1.1",
            Token = "mytoken"
        };

        Assert.Equal("dev001", ctx.Code);
        Assert.Equal("192.168.1.1@5678", ctx.ClientId);
        Assert.Equal("192.168.1.1", ctx.UserHost);
        Assert.Equal("mytoken", ctx.Token);
    }
    #endregion
}
