using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NewLife.Caching;
using NewLife.Remoting.Extensions.ModelBinders;
using NewLife.Remoting.Extensions.Models;
using NewLife.Remoting.Extensions.Services;
using NewLife.Remoting.Models;
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
    public static IServiceCollection AddRemoting(this IServiceCollection services, ITokenSetting? setting = null)
    {
        //if (setting == null) throw new ArgumentNullException(nameof(setting));

        services.TryAddTransient<ILoginRequest, LoginRequest>();
        services.TryAddTransient<ILoginResponse, LoginResponse>();
        services.TryAddTransient<ILogoutResponse, LogoutResponse>();
        services.TryAddTransient<IPingRequest, PingRequest>();
        services.TryAddTransient<IPingResponse, PingResponse>();

        // 注册Remoting所必须的服务
        if (setting != null)
        {
            services.TryAddSingleton<TokenService>();
            services.TryAddSingleton(setting);
        }

        // 注册密码提供者，用于通信过程中保护密钥，避免明文传输
        services.TryAddSingleton<IPasswordProvider>(new SaltPasswordProvider { Algorithm = "md5", SaltTime = 60 });

        // 注册缓存提供者，必须有默认实现
        services.TryAddSingleton<ICacheProvider, CacheProvider>();

        // 添加模型绑定器
        //var binderProvider = new ServiceModelBinderProvider();
        services.Configure<MvcOptions>(mvcOptions =>
        {
            //mvcOptions.ModelBinderProviders.Insert(0, binderProvider);
            mvcOptions.ModelBinderProviders.Insert(0, new InterfaceModelBinderProvider());
        });
        //services.AddSingleton<IModelMetadataProvider, ServicModelMetadataProvider>();

        return services;
    }
}