using System;
using System.ComponentModel;
using NewLife.Remoting.Http;
using Xunit;

namespace XUnitTest;

public class WebSocketCodecTests
{
    [Fact]
    [DisplayName("WebSocketClientCodec默认属性")]
    public void ClientCodec_DefaultProperties()
    {
        var codec = new WebSocketClientCodec();

        Assert.False(codec.UserPacket);
    }

    [Fact]
    [DisplayName("WebSocketClientCodec设置UserPacket")]
    public void ClientCodec_SetUserPacket()
    {
        var codec = new WebSocketClientCodec { UserPacket = true };

        Assert.True(codec.UserPacket);
    }

    [Fact]
    [DisplayName("WebSocketServerCodec默认属性")]
    public void ServerCodec_DefaultProperties()
    {
        var codec = new WebSocketServerCodec();

        Assert.Null(codec.Server);
        Assert.Null(codec.Version);
        Assert.Null(codec.Protocol);
    }

    [Fact]
    [DisplayName("WebSocketServerCodec设置属性")]
    public void ServerCodec_SetProperties()
    {
        var codec = new WebSocketServerCodec
        {
            Server = "TestServer",
            Version = "13",
            Protocol = "mqtt"
        };

        Assert.Equal("TestServer", codec.Server);
        Assert.Equal("13", codec.Version);
        Assert.Equal("mqtt", codec.Protocol);
    }
}
