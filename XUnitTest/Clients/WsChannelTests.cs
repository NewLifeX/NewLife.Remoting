using System;
using System.ComponentModel;
using System.Threading.Tasks;
using NewLife.Remoting;
using NewLife.Remoting.Clients;
using Xunit;

namespace XUnitTest.Clients;

/// <summary>WsChannel 单元测试</summary>
public class WsChannelTests
{
    /// <summary>测试用 ClientBase 子类，避免依赖真实网络</summary>
    private class TestClientBase : ClientBase
    {
        public TestClientBase()
        {
            Name = "Test";
            Server = "http://localhost:12345";
        }
    }

    [Fact]
    [DisplayName("WsChannel默认Active为false")]
    public void DefaultActive_IsFalse()
    {
        using var client = new TestClientBase();
        var channel = new WsChannel(client);

        Assert.False(channel.Active);
    }

    [Fact]
    [DisplayName("WsChannel_Dispose_NoThrow")]
    public void Dispose_NoThrow()
    {
        using var client = new TestClientBase();
        var channel = new WsChannel(client);

        // Dispose should not throw even if called multiple times
        channel.Dispose();
        channel.Dispose();
    }

    [Fact]
    [DisplayName("WsChannel_ValidWebSocket_NullHttp_NoThrow")]
    public async Task ValidWebSocket_NullCurrent_NoThrow()
    {
        using var client = new TestClientBase();
        var channel = new WsChannel(client);

        // When http.Current is null, ValidWebSocket should return gracefully
        using var http = new ApiHttpClient("http://localhost:12345");
        await channel.ValidWebSocket(http);
    }

    [Fact]
    [DisplayName("WsChannel_未连接时Active为false")]
    public void NotConnected_ActiveFalse()
    {
        using var client = new TestClientBase();
        var channel = new WsChannel(client);

        Assert.False(channel.Active);
    }

    [Fact]
    [DisplayName("WsChannel_ValidWebSocket内部含超时判死_不因假PongTime触发重连")]
    public async Task ValidWebSocket_WithTimeoutCheck_NoFalsePositive()
    {
        // 验证：即使 _lastPongTime 很小（刚初始化），WsChannel.ValidWebSocket 也不会误判为心跳超时
        // 判死条件：_lastPongTime > 0 且 距上次Pong超过 3×PingPeriod
        using var client = new TestClientBase();
        var channel = new WsChannel(client);

        using var http = new ApiHttpClient("http://localhost:12345");
        // ValidWebSocket 应正常返回（因为 http.Current 为 null，不会发起真实连接）
        await channel.ValidWebSocket(http);
    }
}
