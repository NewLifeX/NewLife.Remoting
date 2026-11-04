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

/// <summary>升级去重测试。去重标识为版本+文件哈希，允许同版本号重新打包后再次安装</summary>
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

        // 使用反射设置 _lastUpgradeKey，模拟已升级过的同一份安装包
        var field = typeof(ClientBase).GetField("_lastUpgradeKey", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(client, "1.0.0:abc123");

        // 调用 Upgrade：去重标识一致，应跳过下载直接返回 info
        var info = await client.Upgrade(null);
        Assert.NotNull(info);
        Assert.Equal("1.0.0", info.Version);
        // 验证未抛出异常（未尝试真实下载）
    }

    [Fact]
    [DisplayName("Upgrade_同版本号新安装包_重新下载")]
    public async Task Upgrade_SameVersionNewPackage_AttemptsUpgrade()
    {
        using var client = new TestClientBase();
        client.MockUpgradeInfo = new UpgradeInfo
        {
            Version = "1.0.0",
            Source = "http://localhost:1/nonexistent.zip",
            FileHash = "new456"
        };

        // 版本号相同但安装包哈希不同，应视为新包重新下载
        var field = typeof(ClientBase).GetField("_lastUpgradeKey", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(client, "1.0.0:old123");

        await Assert.ThrowsAsync<HttpRequestException>(() => client.Upgrade(null));
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
    [DisplayName("_lastUpgradeKey_Null初始值")]
    public void LastUpgradeKey_InitiallyNull()
    {
        using var client = new TestClientBase();

        var field = typeof(ClientBase).GetField("_lastUpgradeKey", BindingFlags.Instance | BindingFlags.NonPublic);
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

        // 使用反射设置 _lastUpgradeKey 为旧版本的安装包
        var field = typeof(ClientBase).GetField("_lastUpgradeKey", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(client, "1.0.0:def456");

        // 不同版本时，Upgrade 会尝试下载，但由于 Source 不可达，预期抛出 HttpRequestException
        // 这验证了版本去重逻辑允许不同版本通过（不因版本匹配而阻塞）
        await Assert.ThrowsAsync<HttpRequestException>(() => client.Upgrade(null));
    }
}
