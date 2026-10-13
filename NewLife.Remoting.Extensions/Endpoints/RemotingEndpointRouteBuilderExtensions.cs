using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using NewLife.Remoting.Extensions.Endpoints;

namespace NewLife.Remoting.Extensions;

/// <summary>最小 API 端点路由扩展。为设备通信提供 NativeAOT 可用的宿主入口</summary>
/// <remarks>
/// 与 <c>BaseDeviceController</c> 提供同一套端点协议，两侧各自实现、互为镜像；
/// 行为一致性由 RemotingEndpointTests 与 BaseDeviceControllerTests 的镜像用例锁定，修改端点逻辑须两侧同步。
/// 区别仅在于接入方式：
/// <list type="bullet">
/// <item>ASP.NET Core MVC 宿主：<c>AddRemoting()</c> + 继承 BaseDeviceController（不支持 AOT）</item>
/// <item>最小 API 宿主：<c>AddRemotingCore()</c> + <c>MapRemoting()</c>（支持 NativeAOT）</item>
/// </list>
/// </remarks>
public static class RemotingEndpointRouteBuilderExtensions
{
    /// <summary>映射设备通信端点</summary>
    /// <remarks>
    /// 端点：<c>Login</c>（匿名）、<c>Logout</c>、<c>Ping</c>、<c>Upgrade</c>、<c>Notify</c>（WebSocket）、
    /// <c>NotifySSE</c>、<c>CommandReply</c>、<c>SendCommand</c>（匿名）、<c>PostEvents</c>。
    /// 需配合 <c>AddRemotingCore()</c> 与 <c>UseRemoting()</c>（注册 WebSocket 中间件）使用。
    /// </remarks>
    /// <param name="endpoints">端点路由构建器</param>
    /// <param name="prefix">路由前缀。为空表示根路径，常用 "Device" 与 MVC 控制器路由对齐</param>
    /// <returns></returns>
    public static IEndpointRouteBuilder MapRemoting(this IEndpointRouteBuilder endpoints, String? prefix = null)
    {
        var p = prefix.IsNullOrEmpty() ? null : "/" + prefix.Trim('/');

        endpoints.MapPost($"{p}/Login", RemotingEndpoints.LoginAsync).WithMetadata(new AllowAnonymousAttribute());
        endpoints.MapMethods($"{p}/Logout", ["GET", "POST"], RemotingEndpoints.LogoutAsync);
        endpoints.MapMethods($"{p}/Ping", ["GET", "POST"], RemotingEndpoints.PingAsync);
        endpoints.MapMethods($"{p}/Upgrade", ["GET", "POST"], RemotingEndpoints.UpgradeAsync);
        endpoints.MapGet($"{p}/Notify", RemotingEndpoints.NotifyAsync);
        endpoints.MapGet($"{p}/NotifySSE", RemotingEndpoints.NotifySSEAsync);
        endpoints.MapPost($"{p}/CommandReply", RemotingEndpoints.CommandReplyAsync);
        endpoints.MapPost($"{p}/SendCommand", RemotingEndpoints.SendCommandAsync).WithMetadata(new AllowAnonymousAttribute());
        endpoints.MapPost($"{p}/PostEvents", RemotingEndpoints.PostEventsAsync);

        return endpoints;
    }
}
