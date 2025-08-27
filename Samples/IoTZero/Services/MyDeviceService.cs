using IoT.Data;
using NewLife.Caching;
using NewLife.IoT.Models;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Security;
using NewLife.Serialization;

namespace IoTZero.Services;

/// <summary>设备服务</summary>
/// <param name="passwordProvider"></param>
/// <param name="cacheProvider"></param>
/// <param name="setting"></param>
/// <param name="tracer"></param>
public class MyDeviceService(ISessionManager sessionManager, IPasswordProvider passwordProvider, ICacheProvider cacheProvider, ITokenSetting setting, ITracer tracer) : IDeviceService
{
    private readonly ICache _cache = cacheProvider.InnerCache;

    #region 登录注销
    /// <summary>
    /// 设备登录验证，内部支持动态注册
    /// </summary>
    /// <param name="request">登录信息</param>
    /// <param name="source">登录来源</param>
    /// <returns></returns>
    /// <exception cref="ApiException"></exception>
    public ILoginResponse Login(DeviceContext context, ILoginRequest request, String source)
    {
        if (request is not LoginInfo inf) throw new ArgumentOutOfRangeException(nameof(request));

        var code = inf.Code;
        var secret = inf.Secret;

        var dv = Device.FindByCode(code);
        var ip = context.UserHost;

        var autoReg = false;
        if (dv == null)
        {
            if (inf.ProductKey.IsNullOrEmpty()) throw new ApiException(ApiCode.BadRequest, "找不到设备，且产品证书为空，无法登录");

            dv = AutoRegister(null, inf, ip);
            autoReg = true;
        }
        else
        {
            if (!dv.Enable) throw new ApiException(ApiCode.Forbidden, "禁止登录");

            // 校验唯一编码，防止客户端拷贝配置
            var uuid = inf.UUID;
            if (!uuid.IsNullOrEmpty() && !dv.Uuid.IsNullOrEmpty() && uuid != dv.Uuid)
                WriteHistory(context, source + "登录校验", false, $"新旧唯一标识不一致！（新）{uuid}!={dv.Uuid}（旧）");

            // 登录密码未设置或者未提交，则执行动态注册
            if (dv == null || !dv.Secret.IsNullOrEmpty()
                && (secret.IsNullOrEmpty() || !passwordProvider.Verify(dv.Secret, secret)))
            {
                if (inf.ProductKey.IsNullOrEmpty()) throw new ApiException(ApiCode.Unauthorized, "设备验证失败，且产品证书为空，无法登录");

                dv = AutoRegister(dv, inf, ip);
                autoReg = true;
            }
        }

        //if (dv != null && !dv.Enable) throw new ApiException(99, "禁止登录");

        if (dv == null) throw new ApiException(ApiCode.Unauthorized, "登录失败");

        dv.Login(inf, ip);
        context.Device = dv;

        // 在线记录
        var olt = GetOnline(dv, ip) ?? CreateOnline(dv, ip);
        olt.Save(inf, null, null);
        context.Online = olt;

        //SetChildOnline(dv, ip);

        // 登录历史
        WriteHistory(context, source + "登录", true, $"[{dv.Name}/{dv.Code}]登录成功 " + inf.ToJson(false, false, false));

        var rs = new LoginResponse
        {
            Code = dv.Code,
            Name = dv.Name,
        };

        // 动态注册，下发节点证书
        if (autoReg) rs.Secret = dv.Secret;

        return rs;
    }

    /// <summary>设置设备在线，同时检查在线表</summary>
    /// <param name="dv"></param>
    /// <param name="ip"></param>
    /// <param name="reason"></param>
    public void SetDeviceOnline(Device dv, String ip, String reason)
    {
        // 如果已上线，则不需要埋点
        //if (dv.Online) tracer = null;
        using var span = tracer?.NewSpan(nameof(SetDeviceOnline), new { dv.Name, dv.Code, ip, reason });

        var olt = GetOnline(dv, ip) ?? CreateOnline(dv, ip);

        dv.SetOnline(ip, reason);

        // 避免频繁更新心跳数
        if (olt.UpdateTime.AddSeconds(60) < DateTime.Now)
            olt.Save(null, null, null);
    }

    /// <summary>自动注册</summary>
    /// <param name="device">设备</param>
    /// <param name="inf"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    /// <exception cref="ApiException"></exception>
    public Device AutoRegister(Device device, LoginInfo inf, String ip)
    {
        // 全局开关，是否允许自动注册新产品
        if (!setting.AutoRegister) throw new ApiException(ApiCode.Forbidden, "禁止自动注册");

        // 验证产品，即使产品不给自动注册，也会插入一个禁用的设备
        var product = Product.FindByCode(inf.ProductKey);
        if (product == null || !product.Enable)
            throw new ApiException(ApiCode.NotFound, $"无效产品[{inf.ProductKey}]！");
        //if (!product.Secret.IsNullOrEmpty() && !_passwordProvider.Verify(product.Secret, inf.ProductSecret))
        //    throw new ApiException(13, $"非法产品[{product}]！");

        //// 检查白名单
        //if (!product.IsMatchWhiteIP(ip)) throw new ApiException(13, "非法来源，禁止注册");

        var code = inf.Code;
        if (code.IsNullOrEmpty()) code = Rand.NextString(8);

        device ??= new Device
        {
            Code = code,
            CreateIP = ip,
            CreateTime = DateTime.Now,
            Secret = Rand.NextString(8),
        };

        // 如果未打开动态注册，则把节点修改为禁用
        device.Enable = true;

        if (device.Name.IsNullOrEmpty()) device.Name = inf.Name;

        device.ProductId = product.Id;
        //device.Secret = Rand.NextString(16);
        device.UpdateIP = ip;
        device.UpdateTime = DateTime.Now;

        device.Save();

        // 更新产品设备总量避免界面无法及时获取设备数量信息
        device.Product.Fix();

        this.WriteHistory(device, "动态注册", true, inf.ToJson(false, false, false), null, ip);

        return device;
    }

    /// <summary>注销</summary>
    /// <param name="context">上下文</param>
    /// <param name="reason">注销原因</param>
    /// <param name="source">登录来源</param>
    /// <returns></returns>
    public IOnlineModel Logout(DeviceContext context, String reason, String source)
    {
        var dv = context.Device as Device;
        var ip = context.UserHost;
        var olt = GetOnline(dv, ip);
        if (olt != null)
        {
            context.Online = olt;

            var msg = $"{reason} [{dv}]]登录于{olt.CreateTime.ToFullString()}，最后活跃于{olt.UpdateTime.ToFullString()}";
            WriteHistory(context, source + "设备下线", true, msg);
            olt.Delete();

            var sid = $"{dv.Id}@{ip}";
            _cache.Remove($"DeviceOnline:{sid}");

            // 计算在线时长
            if (olt.CreateTime.Year > 2000)
                dv.OnlineTime += (Int32)(DateTime.Now - olt.CreateTime).TotalSeconds;

            dv.Logout();

            //DeviceOnlineService.CheckOffline(device, "注销");
        }

        return olt;
    }
    #endregion

    #region 心跳保活
    /// <summary>心跳</summary>
    /// <param name="context">上下文</param>
    /// <param name="request">心跳请求</param>
    /// <returns></returns>
    public IOnlineModel Ping(DeviceContext context, IPingRequest request)
    {
        var dv = context.Device as Device;
        var ip = context.UserHost;
        var inf = request as PingInfo;
        if (inf != null && !inf.IP.IsNullOrEmpty()) dv.IP = inf.IP;

        // 自动上线
        if (dv != null && !dv.Online) dv.SetOnline(ip, "心跳");

        dv.UpdateIP = ip;
        dv.SaveAsync();

        var online = GetOnline(dv, ip) ?? CreateOnline(dv, ip);
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
    public IOnlineModel SetOnline(DeviceContext context, Boolean online)
    {
        if (context.Device is Device dv)
        {
            // 上线打标记
            var olt = GetOnline(dv, context.UserHost);
            if (olt != null)
            {
                olt.WebSocket = online;
                olt.Update();
            }

            return olt;
        }

        return null;
    }

    /// <summary></summary>
    /// <param name="device">设备</param>
    /// <param name="ip"></param>
    /// <returns></returns>
    public virtual DeviceOnline GetOnline(Device device, String ip)
    {
        var sid = $"{device.Id}@{ip}";
        var olt = _cache.Get<DeviceOnline>($"DeviceOnline:{sid}");
        if (olt != null)
        {
            _cache.SetExpire($"DeviceOnline:{sid}", TimeSpan.FromSeconds(600));
            return olt;
        }

        return DeviceOnline.FindBySessionId(sid);
    }

    /// <summary>检查在线</summary>
    /// <param name="device">设备</param>
    /// <param name="ip"></param>
    /// <returns></returns>
    public virtual DeviceOnline CreateOnline(Device device, String ip)
    {
        var sid = $"{device.Id}@{ip}";
        var olt = DeviceOnline.GetOrAdd(sid);
        olt.ProductId = device.ProductId;
        olt.DeviceId = device.Id;
        olt.Name = device.Name;
        olt.IP = device.IP;
        olt.CreateIP = ip;

        olt.Creator = Environment.MachineName;

        _cache.Set($"DeviceOnline:{sid}", olt, 600);

        return olt;
    }

    /// <summary>删除在线</summary>
    /// <param name="deviceId"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    public Int32 RemoveOnline(Int32 deviceId, String ip)
    {
        var sid = $"{deviceId}@{ip}";

        return _cache.Remove($"DeviceOnline:{sid}");
    }
    #endregion

    #region 升级更新
    /// <summary>升级检查</summary>
    /// <param name="channel">更新通道</param>
    /// <returns></returns>
    public IUpgradeInfo Upgrade(DeviceContext context, String channel) => null;
    #endregion

    #region 下行通知
    /// <summary>发送命令</summary>
    /// <param name="device">设备</param>
    /// <param name="command"></param>
    /// <returns></returns>
    public Task<Int32> SendCommand(IDeviceModel device, CommandModel command, CancellationToken cancellationToken) => sessionManager.PublishAsync(device.Code, command, null, cancellationToken);
    #endregion

    #region 事件上报
    /// <summary>命令响应</summary>
    /// <param name="context">上下文</param>
    /// <param name="model"></param>
    /// <returns></returns>
    public Int32 CommandReply(DeviceContext context, CommandReplyModel model) => 0;

    /// <summary>上报事件</summary>
    /// <param name="context">上下文</param>
    /// <param name="events"></param>
    /// <returns></returns>
    public Int32 PostEvents(DeviceContext context, EventModel[] events) => 0;
    #endregion

    #region 辅助
    /// <summary>查找设备</summary>
    /// <param name="code"></param>
    /// <returns></returns>
    public IDeviceModel QueryDevice(String code) => Device.FindByCode(code);

    /// <summary>写设备历史</summary>
    /// <param name="context">上下文</param>
    /// <param name="action">动作</param>
    /// <param name="success">成功</param>
    /// <param name="remark">备注内容</param>
    public void WriteHistory(DeviceContext context, String action, Boolean success, String remark)
    {
        var traceId = DefaultSpan.Current?.TraceId;
        var hi = DeviceHistory.Create(context.Device as Device, action, success, remark, Environment.MachineName, context.UserHost, traceId);
    }
    #endregion
}