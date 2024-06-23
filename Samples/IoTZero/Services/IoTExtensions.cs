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

        // 逐个注册每一个用到的服务，必须做到清晰明了
        services.AddSingleton<ThingService>();
        services.AddSingleton<DataService>();
        services.AddSingleton<QueueService>();

        services.AddSingleton<IDeviceService, MyDeviceService>();

        services.AddTransient<ILoginRequest, LoginInfo>();
        services.AddTransient<IPingRequest, PingInfo>();

        // 注册Remoting所必须的服务
        services.AddRemoting(setting);

        // 后台服务
        services.AddHostedService<ShardTableService>();
        services.AddHostedService<DeviceOnlineService>();

        return services;
    }
}
