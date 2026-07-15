using System;
using System.ComponentModel;
using NewLife.Remoting.Clients;
using Xunit;

namespace XUnitTest.Clients;

/// <summary>SseChannel 单元测试</summary>
public class SseChannelTests
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
    [DisplayName("SseChannel默认Active为false")]
    public void DefaultActive_IsFalse()
    {
        using var client = new TestClientBase();
        var channel = new SseChannel(client);

        Assert.False(channel.Active);
    }

    [Fact]
    [DisplayName("SseChannel_Dispose_NoThrow")]
    public void Dispose_NoThrow()
    {
        using var client = new TestClientBase();
        var channel = new SseChannel(client);

        // Dispose should not throw even if called multiple times
        channel.Dispose();
        channel.Dispose();
    }

    [Fact]
    [DisplayName("SseChannel_StopSse_NoThrow")]
    public void StopSse_NoThrow()
    {
        using var client = new TestClientBase();
        var channel = new SseChannel(client);

        // StopSse when not started should not throw
        channel.StopSse();
    }

    [Fact]
    [DisplayName("SseChannel_ActiveAfterStop")]
    public void ActiveAfterStop()
    {
        using var client = new TestClientBase();
        var channel = new SseChannel(client);

        Assert.False(channel.Active);
        channel.StopSse();
        Assert.False(channel.Active);
    }
}
