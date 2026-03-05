using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife.Remoting;
using NewLife.Remoting.Models;
using Xunit;

namespace XUnitTest;

public class ModelTests
{
    #region EventModel
    [Fact]
    [DisplayName("EventModel属性读写")]
    public void EventModel_Properties()
    {
        var model = new EventModel
        {
            Time = 1234567890123,
            Type = "alert",
            Name = "TemperatureHigh",
            Remark = "温度过高：85°C"
        };

        Assert.Equal(1234567890123, model.Time);
        Assert.Equal("alert", model.Type);
        Assert.Equal("TemperatureHigh", model.Name);
        Assert.Equal("温度过高：85°C", model.Remark);
    }

    [Fact]
    [DisplayName("EventModel默认值")]
    public void EventModel_DefaultValues()
    {
        var model = new EventModel();

        Assert.Equal(0, model.Time);
        Assert.Null(model.Type);
        Assert.Null(model.Name);
        Assert.Null(model.Remark);
    }
    #endregion

    #region CommandInModel
    [Fact]
    [DisplayName("CommandInModel属性读写")]
    public void CommandInModel_Properties()
    {
        var model = new CommandInModel
        {
            Code = "node001",
            Command = "Restart",
            Argument = "{\"force\":true}",
            StartTime = 10,
            Expire = 3600,
            Timeout = 30
        };

        Assert.Equal("node001", model.Code);
        Assert.Equal("Restart", model.Command);
        Assert.Equal("{\"force\":true}", model.Argument);
        Assert.Equal(10, model.StartTime);
        Assert.Equal(3600, model.Expire);
        Assert.Equal(30, model.Timeout);
    }

    [Fact]
    [DisplayName("CommandInModel默认值")]
    public void CommandInModel_DefaultValues()
    {
        var model = new CommandInModel();

        Assert.Null(model.Code);
        Assert.Null(model.Command);
        Assert.Null(model.Argument);
        Assert.Equal(0, model.StartTime);
        Assert.Equal(0, model.Expire);
        Assert.Equal(0, model.Timeout);
    }
    #endregion

    #region CommandEventArgs
    [Fact]
    [DisplayName("CommandEventArgs属性读写")]
    public void CommandEventArgs_Properties()
    {
        var cmd = new CommandModel { Id = 1, Command = "test" };
        var reply = new CommandReplyModel { Id = 1, Status = CommandStatus.已完成 };
        var args = new CommandEventArgs
        {
            Model = cmd,
            Message = "raw json",
            Reply = reply
        };

        Assert.Equal(cmd, args.Model);
        Assert.Equal("raw json", args.Message);
        Assert.Equal(reply, args.Reply);
    }

    [Fact]
    [DisplayName("CommandEventArgs默认值")]
    public void CommandEventArgs_DefaultValues()
    {
        var args = new CommandEventArgs();

        Assert.Null(args.Model);
        Assert.Null(args.Message);
        Assert.Null(args.Reply);
    }

    [Fact]
    [DisplayName("CommandEventArgs继承EventArgs")]
    public void CommandEventArgs_InheritsEventArgs()
    {
        var args = new CommandEventArgs();
        Assert.IsAssignableFrom<EventArgs>(args);
    }
    #endregion

    #region LoginEventArgs
    [Fact]
    [DisplayName("LoginEventArgs构造函数")]
    public void LoginEventArgs_Constructor()
    {
        var req = new LoginRequest { Code = "dev001" };
        var resp = new LoginResponse { Name = "设备1" };
        var args = new LoginEventArgs(req, resp);

        Assert.Equal(req, args.Request);
        Assert.Equal(resp, args.Response);
    }

    [Fact]
    [DisplayName("LoginEventArgs空参数")]
    public void LoginEventArgs_NullParams()
    {
        var args = new LoginEventArgs(null, null);

        Assert.Null(args.Request);
        Assert.Null(args.Response);
    }

    [Fact]
    [DisplayName("LoginEventArgs继承EventArgs")]
    public void LoginEventArgs_InheritsEventArgs()
    {
        var args = new LoginEventArgs(null, null);
        Assert.IsAssignableFrom<EventArgs>(args);
    }
    #endregion

    #region LogoutResponse
    [Fact]
    [DisplayName("LogoutResponse属性读写")]
    public void LogoutResponse_Properties()
    {
        var resp = new LogoutResponse
        {
            Name = "设备1",
            Token = "abc123"
        };

        Assert.Equal("设备1", resp.Name);
        Assert.Equal("abc123", resp.Token);
    }

    [Fact]
    [DisplayName("LogoutResponse实现ILogoutResponse")]
    public void LogoutResponse_ImplementsInterface()
    {
        ILogoutResponse resp = new LogoutResponse();
        resp.Token = "token123";

        Assert.Equal("token123", resp.Token);
    }
    #endregion

    #region UpgradeInfo
    [Fact]
    [DisplayName("UpgradeInfo属性读写")]
    public void UpgradeInfo_Properties()
    {
        var info = new UpgradeInfo
        {
            Version = "2.0.0",
            Source = "http://cdn.example.com/update.zip",
            FileHash = "abc123",
            FileSize = 1024000,
            Preinstall = "backup.bat",
            Executor = "install.bat",
            Force = true,
            Description = "重要更新"
        };

        Assert.Equal("2.0.0", info.Version);
        Assert.Equal("http://cdn.example.com/update.zip", info.Source);
        Assert.Equal("abc123", info.FileHash);
        Assert.Equal(1024000, info.FileSize);
        Assert.Equal("backup.bat", info.Preinstall);
        Assert.Equal("install.bat", info.Executor);
        Assert.True(info.Force);
        Assert.Equal("重要更新", info.Description);
    }

    [Fact]
    [DisplayName("UpgradeInfo实现IUpgradeInfo")]
    public void UpgradeInfo_ImplementsIUpgradeInfo()
    {
        IUpgradeInfo info = new UpgradeInfo
        {
            Version = "1.0",
            Source = "http://example.com",
            FileHash = "hash"
        };

        Assert.Equal("1.0", info.Version);
        Assert.Equal("http://example.com", info.Source);
        Assert.Equal("hash", info.FileHash);
    }

    [Fact]
    [DisplayName("UpgradeInfo实现IUpgradeInfo2")]
    public void UpgradeInfo_ImplementsIUpgradeInfo2()
    {
        IUpgradeInfo2 info = new UpgradeInfo
        {
            Preinstall = "pre.sh",
            Executor = "run.sh",
            Force = false
        };

        Assert.Equal("pre.sh", info.Preinstall);
        Assert.Equal("run.sh", info.Executor);
        Assert.False(info.Force);
    }
    #endregion

    #region UpdateModes
    [Fact]
    [DisplayName("UpdateModes枚举值")]
    public void UpdateModes_Values()
    {
        Assert.Equal(0, (Int32)UpdateModes.Default);
        Assert.Equal(1, (Int32)UpdateModes.Partial);
        Assert.Equal(2, (Int32)UpdateModes.Standard);
        Assert.Equal(3, (Int32)UpdateModes.Full);
    }

    [Fact]
    [DisplayName("UpdateModes描述属性")]
    public void UpdateModes_Descriptions()
    {
        var partialDesc = typeof(UpdateModes).GetField(nameof(UpdateModes.Partial))!
            .GetCustomAttributes(typeof(DescriptionAttribute), false);
        Assert.Single(partialDesc);
        Assert.Equal("部分包", ((DescriptionAttribute)partialDesc[0]).Description);
    }
    #endregion

    #region Features
    [Fact]
    [DisplayName("Features枚举Flags")]
    public void Features_FlagsValues()
    {
        Assert.Equal(1, (Int32)NewLife.Remoting.Clients.Features.Login);
        Assert.Equal(2, (Int32)NewLife.Remoting.Clients.Features.Logout);
        Assert.Equal(4, (Int32)NewLife.Remoting.Clients.Features.Ping);
        Assert.Equal(8, (Int32)NewLife.Remoting.Clients.Features.Upgrade);
        Assert.Equal(16, (Int32)NewLife.Remoting.Clients.Features.Notify);
        Assert.Equal(32, (Int32)NewLife.Remoting.Clients.Features.CommandReply);
        Assert.Equal(64, (Int32)NewLife.Remoting.Clients.Features.PostEvent);
        Assert.Equal(0xFF, (Int32)NewLife.Remoting.Clients.Features.All);
    }

    [Fact]
    [DisplayName("Features组合")]
    public void Features_Combination()
    {
        var features = NewLife.Remoting.Clients.Features.Login | NewLife.Remoting.Clients.Features.Ping;

        Assert.True(features.HasFlag(NewLife.Remoting.Clients.Features.Login));
        Assert.True(features.HasFlag(NewLife.Remoting.Clients.Features.Ping));
        Assert.False(features.HasFlag(NewLife.Remoting.Clients.Features.Upgrade));
    }

    [Fact]
    [DisplayName("Features.All包含全部")]
    public void Features_AllContainsAll()
    {
        var all = NewLife.Remoting.Clients.Features.All;

        Assert.True(all.HasFlag(NewLife.Remoting.Clients.Features.Login));
        Assert.True(all.HasFlag(NewLife.Remoting.Clients.Features.Logout));
        Assert.True(all.HasFlag(NewLife.Remoting.Clients.Features.Ping));
        Assert.True(all.HasFlag(NewLife.Remoting.Clients.Features.Upgrade));
        Assert.True(all.HasFlag(NewLife.Remoting.Clients.Features.Notify));
        Assert.True(all.HasFlag(NewLife.Remoting.Clients.Features.CommandReply));
        Assert.True(all.HasFlag(NewLife.Remoting.Clients.Features.PostEvent));
    }
    #endregion

    #region LoginStatus
    [Fact]
    [DisplayName("LoginStatus枚举值")]
    public void LoginStatus_Values()
    {
        Assert.Equal(0, (Int32)NewLife.Remoting.Clients.LoginStatus.Ready);
        Assert.Equal(1, (Int32)NewLife.Remoting.Clients.LoginStatus.LoggingIn);
        Assert.Equal(2, (Int32)NewLife.Remoting.Clients.LoginStatus.LoggedIn);
    }
    #endregion
}
