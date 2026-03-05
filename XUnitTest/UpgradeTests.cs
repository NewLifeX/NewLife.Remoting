using System;
using System.ComponentModel;
using System.Threading.Tasks;
using NewLife.Remoting;
using NewLife.Remoting.Clients;
using NewLife.Remoting.Models;
using Xunit;

namespace XUnitTest;

public class UpgradeTests
{
    [Fact]
    [DisplayName("默认属性")]
    public void DefaultProperties()
    {
        var upgrade = new Upgrade();

        Assert.NotNull(upgrade.Name);
        Assert.Equal("Update", upgrade.UpdatePath);
        Assert.Equal(".", upgrade.DestinationPath);
        Assert.Null(upgrade.Url);
        Assert.Null(upgrade.SourceFile);
        Assert.Null(upgrade.TempPath);
        Assert.Equal(UpdateModes.Standard, upgrade.Mode);
    }

    [Fact]
    [DisplayName("属性可写")]
    public void Properties_Writable()
    {
        var upgrade = new Upgrade
        {
            Name = "TestApp",
            UpdatePath = "MyUpdate",
            DestinationPath = "/opt/app",
            Url = "http://example.com/update.zip",
            SourceFile = "/tmp/update.zip",
            TempPath = "/tmp/extract",
            Mode = UpdateModes.Full
        };

        Assert.Equal("TestApp", upgrade.Name);
        Assert.Equal("MyUpdate", upgrade.UpdatePath);
        Assert.Equal("/opt/app", upgrade.DestinationPath);
        Assert.Equal("http://example.com/update.zip", upgrade.Url);
        Assert.Equal("/tmp/update.zip", upgrade.SourceFile);
        Assert.Equal("/tmp/extract", upgrade.TempPath);
        Assert.Equal(UpdateModes.Full, upgrade.Mode);
    }

    [Fact]
    [DisplayName("CheckFileHash空hash")]
    public void CheckFileHash_EmptyHash()
    {
        var upgrade = new Upgrade();

        Assert.False(upgrade.CheckFileHash(""));
        Assert.False(upgrade.CheckFileHash(null!));
    }

    [Fact]
    [DisplayName("CheckFileHash无源文件")]
    public void CheckFileHash_NoSourceFile()
    {
        var upgrade = new Upgrade();

        Assert.False(upgrade.CheckFileHash("abc123"));
    }

    [Fact]
    [DisplayName("CheckFileHash源文件不存在")]
    public void CheckFileHash_FileNotExist()
    {
        var upgrade = new Upgrade
        {
            SourceFile = "nonexistent_file.zip"
        };

        Assert.False(upgrade.CheckFileHash("abc123"));
    }

    [Fact]
    [DisplayName("Download空Url返回false")]
    public async Task Download_EmptyUrl()
    {
        var upgrade = new Upgrade();

        var result = await upgrade.Download();

        Assert.False(result);
    }

    [Fact]
    [DisplayName("Extract无源文件返回false")]
    public void Extract_NoSourceFile()
    {
        var upgrade = new Upgrade();

        Assert.False(upgrade.Extract());
    }

    [Fact]
    [DisplayName("Extract源文件不存在返回false")]
    public void Extract_FileNotExist()
    {
        var upgrade = new Upgrade
        {
            SourceFile = "nonexistent.zip"
        };

        Assert.False(upgrade.Extract());
    }

    [Fact]
    [DisplayName("Update空目标目录返回false")]
    public void Update_EmptyDestination()
    {
        var upgrade = new Upgrade
        {
            DestinationPath = null
        };

        Assert.False(upgrade.Update());
    }

    [Fact]
    [DisplayName("Update无临时目录返回false")]
    public void Update_NoTempPath()
    {
        var upgrade = new Upgrade
        {
            DestinationPath = ".",
            TempPath = null
        };

        Assert.False(upgrade.Update());
    }
}
