using NewLife.Collections;
using NewLife.Remoting.Models;

namespace NewLife.Remoting.Extensions.Services;

/// <summary>设备上下文对象池。MVC 与最小 API 两套宿主共用，避免每请求分配</summary>
internal static class DeviceContextPool
{
    private static readonly Pool<DeviceContext> _pool = new(256);

    /// <summary>借出设备上下文</summary>
    /// <returns></returns>
    public static DeviceContext Rent() => _pool.Get();

    /// <summary>归还设备上下文，归还前清空数据</summary>
    /// <param name="context">设备上下文</param>
    public static void Return(DeviceContext context)
    {
        if (context == null) return;

        context.Clear();
        _pool.Return(context);
    }
}
