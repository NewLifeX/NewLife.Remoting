using System;
using System.ComponentModel;
using NewLife.Remoting;
using NewLife.Remoting.Clients;
using Xunit;

namespace XUnitTest;

/// <summary>ServerTimeProvider单元测试</summary>
public class ServerTimeProviderTests
{
    /// <summary>测试用ClientBase子类</summary>
    private class TestClient : ClientBase
    {
        public TestClient() : base()
        {
            Name = "Test";
        }

        /// <summary>设置Span用于测试</summary>
        public void SetSpan(TimeSpan span)
        {
            // 通过反射设置_span私有字段
            var field = typeof(ClientBase).GetField("_span", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(this, span);
        }
    }

    [Fact]
    [DisplayName("默认属性")]
    public void DefaultProperties()
    {
        var provider = new ServerTimeProvider();

        Assert.Null(provider.Client);
    }

    [Fact]
    [DisplayName("GetUtcNow无Client返回系统时间")]
    public void GetUtcNow_NullClient()
    {
        var provider = new ServerTimeProvider();

        var before = DateTimeOffset.UtcNow;
        var result = provider.GetUtcNow();
        var after = DateTimeOffset.UtcNow;

        Assert.True(result >= before.AddSeconds(-1));
        Assert.True(result <= after.AddSeconds(1));
    }

    [Fact]
    [DisplayName("GetUtcNow有Client叠加时间差")]
    public void GetUtcNow_WithClient()
    {
        using var client = new TestClient();
        client.SetSpan(TimeSpan.FromHours(1));

        var provider = new ServerTimeProvider { Client = client };

        var before = DateTimeOffset.UtcNow.AddHours(1);
        var result = provider.GetUtcNow();
        var after = DateTimeOffset.UtcNow.AddHours(1);

        // 结果应在期望范围内（允许1秒误差）
        Assert.True(result >= before.AddSeconds(-1));
        Assert.True(result <= after.AddSeconds(1));
    }
}
