extern alias IoTZero;

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using IoTZero::IoTEdge;
using Microsoft.Extensions.DependencyInjection;
using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Services;
using NewLife.Security;
using Xunit;

namespace XUnitTest.Samples.IoTZero;

/// <summary>下发命令接口的鉴权契约</summary>
/// <remarks>
/// <c>Device/SendCommand</c> 标了 [AllowAnonymous]（否则设备侧无法带令牌调用），
/// 鉴权只能在 Service 层完成：没有令牌、令牌无效或无权操作目标设备都必须被拒绝。
/// </remarks>
[Collection("SamplesIntegration")]
public class DeviceCommandAuthTests : IClassFixture<IoTZeroWebFactory>
{
    private readonly IoTZeroWebFactory _factory;

    public DeviceCommandAuthTests(IoTZeroWebFactory factory) => _factory = factory;

    [Fact(DisplayName = "IoTZero_下发命令缺少令牌被拒绝")]
    public async Task SendCommand_WithoutToken_Rejected()
    {
        Assert.False(_factory.BaseUrl.IsNullOrEmpty(), "BaseUrl 未初始化，Factory 启动失败");

        using var http = new HttpClient();
        var payload = JsonSerializer.Serialize(new
        {
            Code = "auth-test-device",
            Command = "test:noop",
            Timeout = 0,
        });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await http.PostAsync($"{_factory.BaseUrl}/Device/SendCommand", content).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized || body.Contains("401"),
            $"未携带令牌应被拒绝，实际状态={response.StatusCode}，内容={body}");
    }

    [Fact(DisplayName = "IoTZero_刷新令牌不能用于下发命令")]
    public async Task SendCommand_WithRefreshToken_Rejected()
    {
        Assert.False(_factory.BaseUrl.IsNullOrEmpty(), "BaseUrl 未初始化，Factory 启动失败");

        var tokenService = _factory.Services.GetRequiredService<ITokenService>();
        var token = tokenService.IssueToken("integration-test-app");
        Assert.False(token.RefreshToken.IsNullOrEmpty());

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.RefreshToken);

        var payload = JsonSerializer.Serialize(new
        {
            Code = "auth-test-device",
            Command = "test:noop",
            Timeout = 0,
        });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await http.PostAsync($"{_factory.BaseUrl}/Device/SendCommand", content).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized || body.Contains("401"),
            $"刷新令牌不能作为调用凭据，实际状态={response.StatusCode}，内容={body}");
    }

    [Fact(DisplayName = "IoTZero_空登录体返回统一错误信封")]
    public async Task Login_EmptyBody_ReturnsEnvelope()
    {
        Assert.False(_factory.BaseUrl.IsNullOrEmpty(), "BaseUrl 未初始化，Factory 启动失败");

        using var http = new HttpClient();
        using var content = new StringContent("", Encoding.UTF8, "application/json");

        var response = await http.PostAsync($"{_factory.BaseUrl}/Device/Login", content).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        Assert.True(body.Contains("\"code\":400"), body);
        Assert.True(body.Contains("request"), body);
    }

    [Fact(DisplayName = "IoTZero_设备自身令牌不能下发命令")]
    public async Task SendCommand_WithDeviceToken_Rejected()
    {
        Assert.False(_factory.BaseUrl.IsNullOrEmpty(), "BaseUrl 未初始化，Factory 启动失败");

        // 独立设备登录，拿到设备自身的访问令牌
        var code = "TOK" + Rand.NextString(8);
        var setting = new ClientSetting { Server = _factory.BaseUrl, DeviceCode = code };
        using var device = new HttpDevice(setting) { Log = XTrace.Log, Timeout = 60_000 };

        var (ok, error) = await device.LoginWithBudgetAsync("设备令牌测试", 120);
        Assert.True(ok, $"设备登录失败：{error}");

        var token = ((IApiClient)device).Token;
        Assert.False(token.IsNullOrEmpty(), "设备登录后应持有访问令牌");

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var payload = JsonSerializer.Serialize(new
        {
            Code = code,
            Command = "test:noop",
            Timeout = 0,
        });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await http.PostAsync($"{_factory.BaseUrl}/Device/SendCommand", content).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        Assert.True(
            response.StatusCode == HttpStatusCode.Forbidden || body.Contains("403"),
            $"设备令牌不得下发命令，实际状态={response.StatusCode}，内容={body}");
    }
}
