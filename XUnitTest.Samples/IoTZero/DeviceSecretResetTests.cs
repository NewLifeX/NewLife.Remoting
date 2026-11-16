extern alias IoTZero;

using System.Threading.Tasks;
using IoTZero::IoT.Data;
using IoTZero::IoTEdge;
using Microsoft.Extensions.DependencyInjection;
using NewLife;
using NewLife.Log;
using NewLife.Remoting.Extensions.Services;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Security;
using Xunit;

namespace XUnitTest.Samples.IoTZero;

/// <summary>设备密钥重置策略的端到端回归</summary>
/// <remarks>
/// 示例宿主显式开启 AllowSecretResetOnAuthFailure，密钥不匹配时清空设备并转入动态注册，
/// 是否真正允许由全局 AutoRegister 决定；通用默认是拒绝重置。
/// </remarks>
[Collection("SamplesIntegration")]
public class DeviceSecretResetTests : IClassFixture<IoTZeroWebFactory>
{
    private readonly IoTZeroWebFactory _factory;

    public DeviceSecretResetTests(IoTZeroWebFactory factory) => _factory = factory;

    [Fact(DisplayName = "IoTZero_允许动态注册时，密钥错误重新注册并下发新密钥")]
    public async Task WrongSecret_AutoRegisterEnabled_ResetsSecret()
    {
        Assert.False(_factory.BaseUrl.IsNullOrEmpty(), "BaseUrl 未初始化，Factory 启动失败");

        var (code, _, secret) = await RegisterAsync("SEC");
        Assert.False(secret.IsNullOrEmpty(), "服务端应下发密钥");

        // 错误密钥登录：按设计清空设备并重新注册，登录成功且下发新密钥
        var badSetting = new ClientSetting { Server = _factory.BaseUrl, DeviceCode = code, DeviceSecret = "wrong" + Rand.NextString(8) };
        using var intruder = new HttpDevice(badSetting) { Log = XTrace.Log, Timeout = 60_000 };

        var (ok, error) = await intruder.LoginWithBudgetAsync("错误密钥", 60);
        Assert.True(ok, $"允许动态注册时应重新注册成功（最后错误：{error}）");
        Assert.False(badSetting.DeviceSecret.IsNullOrEmpty(), "重新注册后应下发新密钥");
        Assert.NotEqual(secret, badSetting.DeviceSecret);

        var device = Device.FindByCode(code);
        Assert.NotNull(device);
        Assert.Equal(badSetting.DeviceSecret, device!.Secret);
    }

    [Fact(DisplayName = "IoTZero_关闭动态注册时，密钥错误拒绝登录且不重置密钥")]
    public async Task WrongSecret_AutoRegisterDisabled_RejectedWithoutReset()
    {
        Assert.False(_factory.BaseUrl.IsNullOrEmpty(), "BaseUrl 未初始化，Factory 启动失败");

        var (code, _, secret) = await RegisterAsync("SEC");

        var setting = _factory.Services.GetRequiredService<ITokenSetting>();
        var old = setting.AutoRegister;
        setting.AutoRegister = false;
        try
        {
            var badSetting = new ClientSetting { Server = _factory.BaseUrl, DeviceCode = code, DeviceSecret = "wrong" + Rand.NextString(8) };
            using var intruder = new HttpDevice(badSetting) { Log = XTrace.Log, Timeout = 20_000 };

            var (ok, _) = await intruder.LoginWithBudgetAsync("错误密钥", 10);
            Assert.False(ok, "关闭动态注册时，密钥不匹配不得登录成功");
        }
        finally
        {
            setting.AutoRegister = old;
        }

        var device = Device.FindByCode(code);
        Assert.NotNull(device);
        Assert.Equal(secret, device!.Secret);
    }

    [Fact(DisplayName = "IoTZero_关闭密钥重置开关时，密钥错误拒绝登录且不重置密钥")]
    public async Task WrongSecret_SecretResetDisabled_RejectedWithoutReset()
    {
        Assert.False(_factory.BaseUrl.IsNullOrEmpty(), "BaseUrl 未初始化，Factory 启动失败");

        var (code, _, secret) = await RegisterAsync("SEC");

        var service = _factory.Services.GetRequiredService<IDeviceService>();
        var deviceService = Assert.IsAssignableFrom<DefaultDeviceService<Device, DeviceOnline>>(service);
        var old = deviceService.AllowSecretResetOnAuthFailure;
        deviceService.AllowSecretResetOnAuthFailure = false;
        try
        {
            var badSetting = new ClientSetting { Server = _factory.BaseUrl, DeviceCode = code, DeviceSecret = "wrong" + Rand.NextString(8) };
            using var intruder = new HttpDevice(badSetting) { Log = XTrace.Log, Timeout = 20_000 };

            var (ok, _) = await intruder.LoginWithBudgetAsync("错误密钥", 10);
            Assert.False(ok, "关闭密钥重置开关时，密钥不匹配不得登录成功");
        }
        finally
        {
            deviceService.AllowSecretResetOnAuthFailure = old;
        }

        var device = Device.FindByCode(code);
        Assert.NotNull(device);
        Assert.Equal(secret, device!.Secret);
    }

    private async Task<(String Code, ClientSetting Setting, String Secret)> RegisterAsync(String prefix)
    {
        var code = prefix + Rand.NextString(8);
        var setting = new ClientSetting { Server = _factory.BaseUrl, DeviceCode = code };
        using var client = new HttpDevice(setting) { Log = XTrace.Log, Timeout = 60_000 };

        var (ok, error) = await client.LoginWithBudgetAsync("首次注册", 120);
        Assert.True(ok, $"首次注册应成功（最后错误：{error}）");

        return (code, setting, setting.DeviceSecret);
    }
}