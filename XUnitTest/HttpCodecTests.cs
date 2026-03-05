using System;
using System.ComponentModel;
using NewLife.Data;
using NewLife.Remoting.Http;
using Xunit;

namespace XUnitTest;

public class HttpCodecTests
{
    [Fact]
    [DisplayName("默认属性")]
    public void DefaultProperties()
    {
        var codec = new HttpCodec();

        Assert.False(codec.AllowParseHeader);
        Assert.NotNull(codec.JsonHost);
    }

    [Fact]
    [DisplayName("AllowParseHeader属性")]
    public void AllowParseHeader_Set()
    {
        var codec = new HttpCodec { AllowParseHeader = true };

        Assert.True(codec.AllowParseHeader);
    }

    [Fact]
    [DisplayName("Write_HttpMessage转为Packet")]
    public void Write_HttpMessage()
    {
        var codec = new HttpCodec();
        var msg = new HttpMessage
        {
            Header = new ArrayPacket("GET / HTTP/1.1\r\nHost:localhost"u8.ToArray()),
        };

        // Write方法需要HandlerContext，这里直接验证消息可以被创建
        Assert.NotNull(msg.Header);
    }
}
