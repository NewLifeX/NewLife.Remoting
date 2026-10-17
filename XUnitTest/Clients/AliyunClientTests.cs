using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Remoting.CloudProviders;
using Xunit;

namespace XUnitTest;

/// <summary>AliyunClient 请求构造测试</summary>
public class AliyunClientTests
{
    private sealed class TestAliyunClient : AliyunClient
    {
        public HttpRequestMessage Build(HttpMethod method, String action, Object? args) => BuildRequest(method, action, args, null);

        public Task<HttpResponseMessage> SendForTestAsync(HttpRequestMessage request) => SendAsync(request, CancellationToken.None);
    }

    [Fact(DisplayName = "阿里云客户端_GET请求写入签名头")]
    public void BuildRequest_Get_WritesSignedHeaders()
    {
        using var client = new TestAliyunClient
        {
            Endpoint = "ecs.aliyuncs.com",
            AccessKeyId = "test-access-key",
            AccessKeySecret = "test-secret",
        };

        using var request = client.Build(HttpMethod.Get, "DescribeRegions", new { RegionId = "cn-hangzhou" });

        Assert.Equal("ecs.aliyuncs.com", request.Headers.Host);
        Assert.Equal("DescribeRegions", request.Headers.GetValues("x-acs-action").Single());
        Assert.Equal("2017-05-25", request.Headers.GetValues("x-acs-version").Single());
        Assert.False(request.Headers.GetValues("x-acs-date").Single().IsNullOrEmpty());
        Assert.False(request.Headers.GetValues("x-acs-signature-nonce").Single().IsNullOrEmpty());

        var authorization = request.Headers.GetValues("Authorization").Single();
        Assert.StartsWith("ACS3-HMAC-SHA256 Credential=test-access-key", authorization);
        Assert.Contains("SignedHeaders=", authorization);
        Assert.Contains("Signature=", authorization);
    }

    [Fact(DisplayName = "阿里云客户端_未设置Endpoint时发送请求抛异常")]
    public async Task SendAsync_WithoutEndpoint_Throws()
    {
        using var client = new TestAliyunClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1/");

        await Assert.ThrowsAsync<ArgumentNullException>(() => client.SendForTestAsync(request));
    }
}
