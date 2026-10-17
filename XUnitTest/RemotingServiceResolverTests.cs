using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest;

/// <summary>RemotingServiceResolver 服务解析测试</summary>
public class RemotingServiceResolverTests
{
    private static ServiceProvider CreateServices() => new ServiceCollection().BuildServiceProvider();

    [Fact(DisplayName = "解析器_同名服务复用客户端实例")]
    public async Task GetClientAsync_SameName_ReusesInstance()
    {
        using var sp = CreateServices();
        using var resolver = new RemotingServiceResolver(sp) { Servers = "http://127.0.0.1:12345" };

        var client1 = await resolver.GetClientAsync("order");
        var client2 = await resolver.GetClientAsync("order");

        Assert.Same(client1, client2);
        Assert.IsType<ApiHttpClient>(client1);
    }

    [Fact(DisplayName = "解析器_不同标签创建不同客户端实例")]
    public async Task GetClientAsync_DifferentTag_CreatesNewInstance()
    {
        using var sp = CreateServices();
        using var resolver = new RemotingServiceResolver(sp) { Servers = "http://127.0.0.1:12345" };

        var client1 = await resolver.GetClientAsync("order", "blue");
        var client2 = await resolver.GetClientAsync("order", "green");

        Assert.NotSame(client1, client2);
    }

    [Fact(DisplayName = "解析器_tcp地址创建ApiClient")]
    public async Task GetClientAsync_TcpAddress_CreatesApiClient()
    {
        using var sp = CreateServices();
        using var resolver = new RemotingServiceResolver(sp) { Servers = "tcp://127.0.0.1:1" };

        var client = await resolver.GetClientAsync("tcp");

        Assert.IsType<ApiClient>(client);
    }

    [Fact(DisplayName = "解析器_udp地址创建ApiClient")]
    public async Task GetClientAsync_UdpAddress_CreatesApiClient()
    {
        using var sp = CreateServices();
        using var resolver = new RemotingServiceResolver(sp) { Servers = "udp://127.0.0.1:1" };

        var client = await resolver.GetClientAsync("udp");

        Assert.IsType<ApiClient>(client);
    }

    [Fact(DisplayName = "解析器_http地址创建ApiHttpClient")]
    public async Task GetClientAsync_HttpAddress_CreatesApiHttpClient()
    {
        using var sp = CreateServices();
        using var resolver = new RemotingServiceResolver(sp) { Servers = "https://api.example.com" };

        var client = await resolver.GetClientAsync("http");

        Assert.IsType<ApiHttpClient>(client);
    }

    [Fact(DisplayName = "解析器_ws地址明确拒绝")]
    public async Task GetClientAsync_WebSocketAddress_Throws()
    {
        using var sp = CreateServices();
        using var resolver = new RemotingServiceResolver(sp) { Servers = "ws://127.0.0.1:12345" };

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => resolver.GetClientAsync("ws"));

        Assert.Contains("tcp://", ex.Message);
    }

    [Fact(DisplayName = "解析器_不支持协议回退基类并拒绝")]
    public async Task GetClientAsync_UnsupportedScheme_Throws()
    {
        using var sp = CreateServices();
        using var resolver = new RemotingServiceResolver(sp) { Servers = "ftp://127.0.0.1:12345" };

        await Assert.ThrowsAsync<NotSupportedException>(() => resolver.GetClientAsync("ftp"));
    }

    [Fact(DisplayName = "解析器_服务名为空抛异常")]
    public async Task GetClientAsync_EmptyName_Throws()
    {
        using var sp = CreateServices();
        using var resolver = new RemotingServiceResolver(sp) { Servers = "http://127.0.0.1:12345" };

        await Assert.ThrowsAsync<ArgumentNullException>(() => resolver.GetClientAsync(""));
    }

    [Fact(DisplayName = "解析器_多地址拆分并去除空白")]
    public async Task ResolveAddressesAsync_MultipleAddresses_SplitsAndTrims()
    {
        using var sp = CreateServices();
        using var resolver = new RemotingServiceResolver(sp) { Servers = " tcp://127.0.0.1:1 ; tcp://127.0.0.1:2 " };

        var addresses = await resolver.ResolveAddressesAsync("tcp");

        Assert.Equal(["tcp://127.0.0.1:1", "tcp://127.0.0.1:2"], addresses);
    }
}
