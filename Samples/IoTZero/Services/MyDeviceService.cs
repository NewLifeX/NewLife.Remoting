using IoT.Data;
using NewLife.Caching;
using NewLife.IoT.Models;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Extensions.Services;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Security;

namespace IoTZero.Services;

/// <summary>设备服务</summary>
/// <param name="passwordProvider"></param>
/// <param name="cacheProvider"></param>
/// <param name="setting"></param>
/// <param name="tracer"></param>
public class MyDeviceService(ISessionManager sessionManager, IPasswordProvider passwordProvider, ICacheProvider cacheProvider, ITokenSetting setting, ITracer tracer) : DefaultDeviceService<Device, DeviceOnline>(sessionManager, passwordProvider, cacheProvider)
{
    #region 登录注销
    /// <summary>设置设备在线，同时检查在线表</summary>
    /// <param name="dv"></param>
    /// <param name="ip"></param>
    /// <param name="reason"></param>
    public void SetDeviceOnline(Device dv, String ip, String reason)
    {
        // 如果已上线，则不需要埋点
        //if (dv.Online) tracer = null;
        using var span = tracer?.NewSpan(nameof(SetDeviceOnline), new { dv.Name, dv.Code, ip, reason });

        var context = new DeviceContext { Device = dv, UserHost = ip };
        var online = (GetOnline(context) ?? CreateOnline(context)) as DeviceOnline;

        dv.SetOnline(ip, reason);

        // 避免频繁更新心跳数
        if (online.UpdateTime.AddSeconds(60) < DateTime.Now)
            online.Save(null, null, null);
    }

    protected override void OnRegister(DeviceContext context, ILoginRequest request)
    {
        // 全局开关，是否允许自动注册新产品
        if (!setting.AutoRegister) throw new ApiException(ApiCode.Forbidden, "禁止自动注册");

        var inf = request as LoginInfo;

        // 验证产品，即使产品不给自动注册，也会插入一个禁用的设备
        var product = Product.FindByCode(inf.ProductKey);
        if (product == null || !product.Enable)
            throw new ApiException(ApiCode.NotFound, $"无效产品[{inf.ProductKey}]！");

        var device = context.Device as Device;
        device.ProductId = product.Id;

        base.OnRegister(context, request);

        // 更新产品设备总量避免界面无法及时获取设备数量信息
        device.Product.Fix();
    }

    /// <summary>注销</summary>
    /// <param name="context">上下文</param>
    /// <param name="reason">注销原因</param>
    /// <param name="source">登录来源</param>
    /// <returns></returns>
    public override IOnlineModel Logout(DeviceContext context, String? reason, String source)
    {
        var online = base.Logout(context, reason, source);
        if (online is DeviceOnline online2 && context.Device is Device device)
        {
            // 计算在线时长
            if (online2.CreateTime.Year > 2000)
                device.OnlineTime += (Int32)(DateTime.Now - online2.CreateTime).TotalSeconds;

            device.Logout();
        }

        return online;
    }
    #endregion

    #region 心跳保活
    /// <summary>设备心跳。更新在线记录信息</summary>
    /// <param name="context">上下文</param>
    /// <param name="request">心跳请求</param>
    /// <returns></returns>
    public override IOnlineModel Ping(DeviceContext context, IPingRequest request)
    {
        var dv = context.Device as Device;
        var ip = context.UserHost;
        var inf = request as PingInfo;
        if (inf != null && !inf.IP.IsNullOrEmpty()) dv.IP = inf.IP;

        // 自动上线
        if (dv != null && !dv.Online) dv.SetOnline(ip, "心跳");

        dv.UpdateIP = ip;
        dv.SaveAsync();

        var online = base.Ping(context, request) as DeviceOnline;
        online.Name = dv.Name;
        online.GroupPath = dv.GroupPath;
        online.ProductId = dv.ProductId;
        online.Save(null, inf, context.Token);

        context.Online = online;

        return online;
    }

    /// <summary>设置设备的长连接上线/下线</summary>
    /// <param name="context">上下文</param>
    /// <param name="online"></param>
    /// <returns></returns>
    public override void SetOnline(DeviceContext context, Boolean online)
    {
        if ((context.Online ?? GetOnline(context)) is DeviceOnline olt)
        {
            olt.WebSocket = online;
            olt.Update();
        }
    }

    /// <summary>创建在线</summary>
    /// <param name="context">上下文</param>
    /// <returns></returns>
    public override IOnlineModel CreateOnline(DeviceContext context)
    {
        if (context.Device is not Device device) return null;

        var online = base.CreateOnline(context) as DeviceOnline;

        online.ProductId = device.ProductId;
        online.DeviceId = device.Id;
        online.Name = device.Name;
        online.IP = device.IP;
        online.CreateIP = context.UserHost;
        online.Creator = Environment.MachineName;

        return online;
    }
    #endregion
}