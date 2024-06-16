using NewLife.IoT.Models;
using NewLife.Remoting.Extensions;
using NewLife.Remoting.Extensions.Models;
using NewLife.Remoting.Extensions.Services;
using NewLife.Remoting.Models;

namespace IoTZero.Services;

/// <summary>IoT扩展</summary>
public static class IoTExtensions
{
    public static IServiceCollection AddIoT(this IServiceCollection services, ITokenSetting setting)
    {
        ArgumentNullException.ThrowIfNull(setting);

        services.AddSingleton<IDeviceService, MyDeviceService>();

        services.AddTransient<ILoginRequest, LoginInfo>();
        services.AddTransient<IPingRequest, PingInfo>();

        // 注册Remoting所必须的服务
        services.AddRemoting(setting);

        return services;
    }
}
