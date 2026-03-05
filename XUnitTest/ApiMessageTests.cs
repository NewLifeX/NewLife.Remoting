using System;
using System.ComponentModel;
using NewLife.Data;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest;

/// <summary>ApiMessage单元测试</summary>
public class ApiMessageTests
{
    [Fact]
    [DisplayName("默认属性")]
    public void DefaultProperties()
    {
        var msg = new ApiMessage();

        Assert.Null(msg.Action);
        Assert.Equal(0, msg.Code);
        Assert.Null(msg.Data);
    }

    [Fact]
    [DisplayName("属性读写")]
    public void Properties_SetAndGet()
    {
        var data = new ArrayPacket(new Byte[] { 1, 2, 3 });
        var msg = new ApiMessage
        {
            Action = "Device/Login",
            Code = 200,
            Data = data
        };

        Assert.Equal("Device/Login", msg.Action);
        Assert.Equal(200, msg.Code);
        Assert.NotNull(msg.Data);
    }

    [Fact]
    [DisplayName("ToString有Code")]
    public void ToString_WithCode()
    {
        var msg = new ApiMessage
        {
            Action = "Device/Login",
            Code = 200
        };

        Assert.Equal("Device/Login[200]", msg.ToString());
    }

    [Fact]
    [DisplayName("ToString零Code")]
    public void ToString_ZeroCode()
    {
        var msg = new ApiMessage
        {
            Action = "Device/Login",
            Code = 0
        };

        Assert.Equal("Device/Login", msg.ToString());
    }

    [Fact]
    [DisplayName("Dispose释放Data")]
    public void Dispose_ReleasesData()
    {
        var msg = new ApiMessage
        {
            Action = "Test",
            Data = new ArrayPacket(new Byte[] { 1, 2, 3 })
        };

        // Dispose应该不抛异常
        msg.Dispose();
    }

    [Fact]
    [DisplayName("Dispose空Data不抛异常")]
    public void Dispose_NullData_NoException()
    {
        var msg = new ApiMessage { Action = "Test" };

        msg.Dispose();
    }
}
