using IoT.Data;
using NewLife.Caching;
using NewLife.IoT.Models;
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
/// <param name="serviceProvider"></param>
public class MyDeviceService(ISessionManager sessionManager, IPasswordProvider passwordProvider, ICacheProvider cacheProvider, ITokenSetting setting, IServiceProvider serviceProvider) : DefaultDeviceService<Device, DeviceOnline>(sessionManager, passwordProvider, cacheProvider, serviceProvider)
{
    #region 登录注销
    /// <summary>设置设备在线，同时检查在线表</summary>
    /// <param name="context"></param>
    /// <param name="reason"></param>
    public void SetDeviceOnline(DeviceContext context, String reason)
    {
        // 如果已上线，则不需要埋点
        //if (dv.Online) tracer = null;
        //using var span = tracer?.NewSpan(nameof(SetDeviceOnline), new { dv.Name, dv.Code, ip, reason });

        var dv = context.Device as Device;
        //var context = new DeviceContext { Device = dv, UserHost = ip };
        var online = (GetOnline(context) ?? CreateOnline(context)) as DeviceOnline;

        dv.SetOnline(context.UserHost, reason);

        // 避免频繁更新心跳数
        if (online.UpdateTime.AddSeconds(60) < DateTime.Now)
            online.Save(null, context);
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

        if (context.Device is Device device)
            device.Logout();

        return online;
    }
    #endregion

    #region 心跳保活
    public override IPingResponse Ping(DeviceContext context, IPingRequest request, IPingResponse response)
    {
        var rs = base.Ping(context, request, response);
        if (rs is MyPingResponse mrs && context.Device is Device device)
        {
            mrs.PollingTime = device.PollingTime;
        }

        return rs;
    }

    /// <summary>设备心跳。更新在线记录信息</summary>
    /// <param name="context">上下文</param>
    /// <param name="request">心跳请求</param>
    /// <returns></returns>
    public override IOnlineModel OnPing(DeviceContext context, IPingRequest request)
    {
        var dv = context.Device as Device;
        if (request is PingInfo inf && !inf.IP.IsNullOrEmpty()) dv.IP = inf.IP;

        // 自动上线
        var ip = context.UserHost;
        if (dv != null && !dv.Online) dv.SetOnline(ip, "心跳");

        dv.UpdateIP = ip;
        dv.SaveAsync();

        return base.OnPing(context, request);
    }

    /// <summary>结算在线时长。累加本次会话在线时长到设备</summary>
    /// <param name="online">在线实体</param>
    /// <param name="device">设备信息</param>
    protected override void OnSettleOnline(IOnlineModel online, IDeviceModel device)
    {
        if (online is DeviceOnline olt && device is Device dev)
        {
            var sec = (Int32)(olt.UpdateTime - olt.LoginTime).TotalSeconds;
            if (sec > 0)
            {
                dev.OnlineTime += sec;
                dev.Update();
            }
        }
    }

    /// <summary>查找在线。直接查库，绕过 XCode SingleCache</summary>
    /// <param name="sessionId"></param>
    /// <returns></returns>
    public override IOnlineModel QueryOnline(String sessionId) => DeviceOnline.FindBySessionId(sessionId, false);
    #endregion
}