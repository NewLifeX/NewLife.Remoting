using System;
using System.ComponentModel;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Clients;
using NewLife.Remoting.Models;
using Xunit;

namespace XUnitTest.Clients;

/// <summary>升级版本去重测试（M6-2 _lastVersion）</summary>
public class UpgradeVersionTests
{
    /// <summary>可控制 UpgradeAsync 返回结果的 ClientBase 子类</summary>
    private class TestClientBase : ClientBase
    {
        /// <summary>模拟的升级信息</summary>
        public IUpgradeInfo? MockUpgradeInfo { get; set; }

        public TestClientBase()
        {
            Name = "Test";
            Server = "http://localhost:12345";
            Log = XTrace.Log;
        }

        /// <summary>重写 UpgradeAsync 返回模拟数据，避免真实网络请求</summary>
        protected override Task<IUpgradeInfo?> UpgradeAsync(String? channel, CancellationToken cancellationToken)
            => Task.FromResult(MockUpgradeInfo);
    }

    [Fact]
    [DisplayName("Upgrade_同一版本_跳过下载")]
    public async Task Upgrade_SameVersion_SkipsDownload()
    {
        using var client = new TestClientBase();
        client.MockUpgradeInfo = new UpgradeInfo
        {
            Version = "1.0.0",
            Source = "http://example.com/update.zip",
            FileHash = "abc123"
        };

        // 使用反射设置 _lastVersion，模拟已升级过的版本
        var field = typeof(ClientBase).GetField("_lastVersion", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(client, "1.0.0");

        // 调用 Upgrade：_lastVersion == info.Version，应跳过下载直接返回 info
        var info = await client.Upgrade(null);
        Assert.NotNull(info);
        Assert.Equal("1.0.0", info.Version);
        // 验证未抛出异常（未尝试真实下载）
    }

    [Fact]
    [DisplayName("Upgrade_UpgradeAsync返回null_返回null")]
    public async Task Upgrade_UpgradeAsyncReturnsNull_ReturnsNull()
    {
        using var client = new TestClientBase();
        client.MockUpgradeInfo = null;

        var info = await client.Upgrade(null);
        Assert.Null(info);
    }

    [Fact]
    [DisplayName("_lastVersion_Null初始值")]
    public void LastVersion_InitiallyNull()
    {
        using var client = new TestClientBase();

        var field = typeof(ClientBase).GetField("_lastVersion", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var val = field.GetValue(client);
        Assert.Null(val);
    }

    [Fact]
    [DisplayName("Upgrade_不同版本_尝试升级")]
    public async Task Upgrade_DifferentVersion_AttemptsUpgrade()
    {
        using var client = new TestClientBase();
        client.MockUpgradeInfo = new UpgradeInfo
        {
            Version = "2.0.0",
            Source = "http://localhost:1/nonexistent.zip",
            FileHash = "def456"
        };

        // 使用反射设置 _lastVersion 为旧版本
        var field = typeof(ClientBase).GetField("_lastVersion", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(client, "1.0.0");

        // 不同版本时，Upgrade 会尝试下载，但由于 Source 不可达，预期抛出 HttpRequestException
        // 这验证了版本去重逻辑允许不同版本通过（不因版本匹配而阻塞）
        await Assert.ThrowsAsync<HttpRequestException>(() => client.Upgrade(null));
    }
}
