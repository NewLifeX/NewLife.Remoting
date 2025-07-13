using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebSockets;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NewLife.Caching;
using NewLife.Remoting.Extensions.ModelBinders;
using NewLife.Remoting.Extensions.Services;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Security;

namespace NewLife.Remoting.Extensions;

/// <summary>远程通信框架扩展</summary>
public static class RemotingExtensions
{
    /// <summary>添加远程通信服务端，注册BaseDeviceController所需类型服务</summary>
    /// <remarks>
    /// 注册登录心跳等模型类，可再次扩展模型类，传输更多内容；
    /// 注册TokenService令牌服务，提供令牌颁发与验证服务；
    /// 注册密码提供者，用于通信过程中保护密钥，避免明文传输；
    /// 注册缓存提供者的默认实现；
    /// </remarks>
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

        services.TryAddSingleton<ISessionManager, SessionManager>();

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

    /// <summary>使用远程通信服务端，注册WebSocket中间件</summary>
    /// <param name="app"></param>
    public static void UseRemoting(this IApplicationBuilder app)
    {
        // 判断是否已经添加了WebSocket中间件
        if (!app.Properties.TryGetValue("__MiddlewareDescriptions", out var value) ||
            value is not IList<String> result || !result.Contains(typeof(WebSocketMiddleware).FullName!))
        {
            app.UseWebSockets(new WebSocketOptions()
            {
                KeepAliveInterval = TimeSpan.FromSeconds(60),
            });
        }

        // 应用停止时，关闭会话管理器，清除所有会话
        var sessionManager = app.ApplicationServices.GetService<ISessionManager>();
        if (sessionManager != null)
        {
            var lifeTime = app.ApplicationServices.GetService<IHostApplicationLifetime>();
            lifeTime?.ApplicationStopping.Register(() =>
            {
                // 关闭时，清除所有会话
                sessionManager.TryDispose();
            });
        }
    }
}