using Microsoft.Extensions.DependencyInjection.Extensions;
using NewLife.Caching;
using NewLife.Remoting.Extensions.Models;
using NewLife.Remoting.Extensions.Services;
using NewLife.Security;

namespace NewLife.Remoting.Extensions;

/// <summary>远程通信框架扩展</summary>
public static class RemotingExtensions
{
    /// <summary>添加远程通信服务端</summary>
    /// <param name="services"></param>
    /// <param name="setting"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static IServiceCollection AddRemoting(this IServiceCollection services, ITokenSetting setting)
    {
        if (setting == null) throw new ArgumentNullException(nameof(setting));

        // 注册Remoting所必须的服务
        services.TryAddSingleton<TokenService>();
        services.TryAddSingleton(setting);

        // 注册密码提供者，用于通信过程中保护密钥，避免明文传输
        services.TryAddSingleton<IPasswordProvider>(new SaltPasswordProvider { Algorithm = "md5", SaltTime = 60 });
       
        services.TryAddSingleton<ICache, MemoryCache>();

        return services;
    }
}