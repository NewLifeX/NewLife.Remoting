using System;
using System.ComponentModel;
using NewLife.Log;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest;

/// <summary>ApiHost单元测试</summary>
public class ApiHostTests
{
    /// <summary>用于测试的具体ApiHost子类</summary>
    private class TestApiHost : ApiHost
    {
        public TestApiHost()
        {
            Name = "TestApi";
        }
    }

    [Fact]
    [DisplayName("默认属性")]
    public void DefaultProperties()
    {
        using var host = new TestApiHost();

        Assert.Equal("TestApi", host.Name);
        Assert.Equal(15_000, host.Timeout);
        Assert.Equal(5_000, host.SlowTrace);
        Assert.NotNull(host.Items);
        Assert.False(host.ShowError);
    }

    [Fact]
    [DisplayName("属性读写")]
    public void Properties_SetAndGet()
    {
        using var host = new TestApiHost();

        host.Name = "MyHost";
        host.Timeout = 30_000;
        host.SlowTrace = 10_000;
        host.ShowError = true;

        Assert.Equal("MyHost", host.Name);
        Assert.Equal(30_000, host.Timeout);
        Assert.Equal(10_000, host.SlowTrace);
        Assert.True(host.ShowError);
    }

    [Fact]
    [DisplayName("Items索引器")]
    public void Items_Indexer()
    {
        using var host = new TestApiHost();

        host["key1"] = "value1";
        host["key2"] = 42;

        Assert.Equal("value1", host["key1"]);
        Assert.Equal(42, host["key2"]);
        Assert.Null(host["nonexist"]);
    }

    [Fact]
    [DisplayName("ToString返回Name")]
    public void ToString_ReturnsName()
    {
        using var host = new TestApiHost();
        host.Name = "TestHost";

        Assert.Equal("TestHost", host.ToString());
    }

    [Fact]
    [DisplayName("GetMessageCodec返回StandardCodec")]
    public void GetMessageCodec_ReturnsCodec()
    {
        using var host = new TestApiHost();
        host.Timeout = 20_000;

        var codec = host.GetMessageCodec();

        Assert.NotNull(codec);
    }

    [Fact]
    [DisplayName("StartTime初始化")]
    public void StartTime_Initialized()
    {
        var before = DateTime.Now;
        using var host = new TestApiHost();
        var after = DateTime.Now;

        Assert.True(host.StartTime >= before.AddSeconds(-1));
        Assert.True(host.StartTime <= after.AddSeconds(1));
    }

    [Fact]
    [DisplayName("日志属性")]
    public void Log_Properties()
    {
        using var host = new TestApiHost();

        Assert.NotNull(host.Log);
        Assert.NotNull(host.EncoderLog);

        host.Log = XTrace.Log;
        Assert.Equal(XTrace.Log, host.Log);
    }
}
