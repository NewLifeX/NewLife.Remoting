using System.Reflection;
using NewLife.Caching;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Security;
using NewLife.Serialization;
using XCode;

namespace NewLife.Remoting.Extensions.Services;

/// <summary>默认设备服务</summary>
/// <param name="sessionManager"></param>
/// <param name="passwordProvider"></param>
/// <param name="cacheProvider"></param>
public abstract class DefaultDeviceService<TDevice, TOnline>(ISessionManager sessionManager, IPasswordProvider passwordProvider, ICacheProvider cacheProvider) : IDeviceService
    where TDevice : Entity<TDevice>, IDeviceModel, new()
    where TOnline : Entity<TOnline>, IOnlineModel, new()
{
    private readonly ICache _cache = cacheProvider.InnerCache;
    private static Func<String, TDevice?>? _findDevice;
    private static Func<String, TOnline?>? _findOnline;

    static DefaultDeviceService()
    {
        {
            var type = typeof(TDevice);
            var method = type.GetMethod("FindByCode", BindingFlags.Public | BindingFlags.Static, [typeof(String)]);
            method ??= type.GetMethod("FindByName", BindingFlags.Public | BindingFlags.Static, [typeof(String)]);

            _findDevice = method?.CreateDelegate<Func<String, TDevice?>>();
        }
        {
            var type = typeof(TOnline);
            var method = type.GetMethod("FindBySessionId", BindingFlags.Public | BindingFlags.Static, [typeof(String)]);
            method ??= type.GetMethod("FindBySessionID", BindingFlags.Public | BindingFlags.Static, [typeof(String)]);
            _findOnline = method?.CreateDelegate<Func<String, TOnline?>>();
        }
    }

    #region 登录注销
    /// <summary>
    /// 设备登录验证，内部支持动态注册
    /// </summary>
    /// <param name="context">上下文</param>
    /// <param name="request">登录信息</param>
    /// <param name="source">登录来源</param>
    /// <returns></returns>
    /// <exception cref="ApiException"></exception>
    public ILoginResponse Login(DeviceContext context, ILoginRequest request, String source)
    {
        if (request == null) throw new ArgumentOutOfRangeException(nameof(request));

        var code = request.Code;
        var device = code.IsNullOrEmpty() ? null : QueryDevice(code);
        if (device != null && !device.Enable) throw new ApiException(ApiCode.Forbidden, "禁止登录");
        if (device != null) context.Device = device;

        // 设备不存在或者验证失败，执行注册流程
        if (device != null && !Authorize(context, request))
        {
            device = null;
        }

        var autoReg = false;
        if (device == null)
        {
            device = Register(context, request);
            autoReg = true;
        }

        if (device == null) throw new ApiException(ApiCode.Unauthorized, "登录失败");

        context.Device = device;
        OnLogin(context, request);

        context.Online = GetOnline(context) ?? CreateOnline(context);

        // 登录历史
        WriteHistory(context, source + "登录", true, $"[{device.Name}/{device.Code}]登录成功 " + request.ToJson(false, false, false));

        var rs = new LoginResponse
        {
            Code = device.Code,
            Name = device.Name
        };

        // 动态注册，下发节点证书
        if (autoReg && device is IDeviceModel2 device2) rs.Secret = device2.Secret;

        return rs;
    }

    /// <summary>验证设备合法性</summary>
    public virtual Boolean Authorize(DeviceContext context, ILoginRequest request)
    {
        if (context.Device is not IDeviceModel2 device) return false;

        // 没有密码时无需验证
        if (device.Secret.IsNullOrEmpty()) return true;
        if (device.Secret.EqualIgnoreCase(request.Secret)) return true;

        if (request.Secret.IsNullOrEmpty() || !passwordProvider.Verify(device.Secret, request.Secret))
        {
            WriteHistory(context, "节点鉴权", false, "密钥校验失败");
            return false;
        }

        // 校验唯一编码，防止客户端拷贝配置
        if (request is ILoginRequest2 request2 && device is IEntity entity)
        {
            var uuid = request2.UUID;
            var uuid2 = entity["uuid"] as String;
            if (!uuid.IsNullOrEmpty() && !uuid2.IsNullOrEmpty() && uuid != uuid2)
            {
                WriteHistory(context, "登录校验", false, $"新旧唯一标识不一致！（新）{uuid}!={uuid2}（旧）");
            }
        }

        return true;
    }

    /// <summary>自动注册</summary>
    /// <param name="context"></param>
    /// <param name="request"></param>
    /// <returns></returns>
    /// <exception cref="ApiException"></exception>
    public virtual IDeviceModel Register(DeviceContext context, ILoginRequest request)
    {
        var code = request.Code;
        if (code.IsNullOrEmpty() && request is ILoginRequest2 request2) code = request2.UUID.GetBytes().Crc().ToString("X8");
        if (code.IsNullOrEmpty()) code = Rand.NextString(8);

        var device = context.Device;
        try
        {
            // 查询已有设备，或新建设备
            device ??= QueryDevice(code);
            if (device == null)
            {
                device = (Entity<TDevice>.Meta.Factory.Create() as IDeviceModel)!;
                device.Code = code;
            }
            context.Device = device;

            // 如果未打开动态注册，则把节点修改为禁用
            device.Enable = true;

            // 注册就必然更新密钥
            if (device is IDeviceModel2 device2)
                device2.Secret = Rand.NextString(16);

            OnRegister(context, request);

            WriteHistory(context, "动态注册", true, request.ToJson(false, false, false));
        }
        catch (Exception ex)
        {
            WriteHistory(context, "动态注册", false, $"[{code}/{device}]注册失败！{ex.Message}");

            throw;
        }

        return device;
    }

    /// <summary>注册中，填充数据并保存</summary>
    /// <param name="context"></param>
    /// <param name="request"></param>
    protected virtual void OnRegister(DeviceContext context, ILoginRequest request) => (context.Device as IEntity)!.Save();

    /// <summary>登录中</summary>
    /// <param name="context"></param>
    /// <param name="request"></param>
    protected virtual void OnLogin(DeviceContext context, ILoginRequest request) { }

    /// <summary>注销</summary>
    /// <param name="context">上下文</param>
    /// <param name="reason">注销原因</param>
    /// <param name="source">登录来源</param>
    /// <returns></returns>
    public virtual IOnlineModel? Logout(DeviceContext context, String? reason, String source)
    {
        var online = context.Online ?? GetOnline(context);
        if (online is IEntity entity)
        {
            context.Online = online;

            var msg = $"{reason} [{context.Device}]]登录于{entity["CreateTime"]}，最后活跃于{entity["UpdateTime"]}";
            WriteHistory(context, source + "设备下线", true, msg);
            entity.Delete();

            RemoveOnline(context);
        }

        return online;
    }
    #endregion

    #region 心跳保活
    /// <summary>心跳</summary>
    /// <param name="context">上下文</param>
    /// <param name="request">心跳请求</param>
    /// <returns></returns>
    public virtual IOnlineModel Ping(DeviceContext context, IPingRequest? request)
    {
        var online = context.Online ?? GetOnline(context) ?? CreateOnline(context);
        context.Online = online;

        return online;
    }

    /// <summary>设置设备的长连接上线/下线</summary>
    /// <param name="context">上下文</param>
    /// <param name="online"></param>
    /// <returns></returns>
    public virtual void SetOnline(DeviceContext context, Boolean online)
    {
        var olt = context.Online ?? GetOnline(context);
        if (olt != null && olt is IEntity entity)
        {
            entity.SetItem("WebSocket", online);
            entity.Update();
        }
    }

    /// <summary>获取会话标识</summary>
    /// <param name="context"></param>
    /// <returns></returns>
    protected virtual String GetSessionId(DeviceContext context) => $"{context.Code ?? context.Device?.Code}@{context.UserHost}";

    /// <summary>获取在线。反射调用FindBySessionId</summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public virtual IOnlineModel? GetOnline(DeviceContext context)
    {
        var sid = GetSessionId(context);
        var online = _cache.Get<IOnlineModel>($"Online:{sid}");
        if (online != null)
        {
            _cache.SetExpire($"Online:{sid}", TimeSpan.FromSeconds(600));
            return online;
        }

        return _findOnline?.Invoke(sid);
    }

    /// <summary>创建在线</summary>
    /// <param name="context">上下文</param>
    /// <returns></returns>
    public virtual IOnlineModel CreateOnline(DeviceContext context)
    {
        var sid = GetSessionId(context);
        var online = context.Online;
        if (online == null)
        {
            if (context.Device is not IDeviceModel2 device)
                throw new InvalidDataException($"创建在线对象需要{GetType().FullName}重载CreateOnline或者设备实体类{typeof(TDevice).FullName}实现IDeviceModel2");

            online = device.CreateOnline(sid);
            if (online is IEntity entity)
            {
                entity.SetItem("CreateUser", Environment.MachineName);
                entity.SetItem("CreateIP", context.UserHost);
                entity.SetItem("CreateTime", DateTime.Now);
            }
        }

        _cache.Set($"Online:{sid}", online, 600);

        return online;
    }

    /// <summary>删除在线</summary>
    /// <param name="context">上下文</param>
    /// <returns></returns>
    public virtual Int32 RemoveOnline(DeviceContext context)
    {
        var sid = context.Online?.SessionId;
        if (sid.IsNullOrEmpty()) GetSessionId(context);

        return _cache.Remove($"Online:{sid}");
    }
    #endregion

    #region 下行通知
    /// <summary>发送命令</summary>
    /// <param name="device">设备</param>
    /// <param name="command">命令对象</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public virtual Task<Int32> SendCommand(IDeviceModel device, CommandModel command, CancellationToken cancellationToken) => sessionManager.PublishAsync(device.Code, command, null, cancellationToken);
    #endregion

    #region 升级更新
    /// <summary>升级检查</summary>
    /// <param name="context">上下文</param>
    /// <param name="channel">更新通道</param>
    /// <returns></returns>
    public virtual IUpgradeInfo? Upgrade(DeviceContext context, String? channel) => null;
    #endregion

    #region 事件上报
    /// <summary>命令响应</summary>
    /// <param name="context">上下文</param>
    /// <param name="model"></param>
    /// <returns></returns>
    public virtual Int32 CommandReply(DeviceContext context, CommandReplyModel model) => 0;

    /// <summary>上报事件</summary>
    /// <param name="context">上下文</param>
    /// <param name="events"></param>
    /// <returns></returns>
    public virtual Int32 PostEvents(DeviceContext context, EventModel[] events) => 0;
    #endregion

    #region 辅助
    /// <summary>查找设备。反射调用FindByCode/FindByName</summary>
    /// <param name="code">编码</param>
    /// <returns></returns>
    public virtual IDeviceModel? QueryDevice(String code) => _findDevice!.Invoke(code);

    /// <summary>写设备历史。扩展调用IDeviceModel2.WriteLog</summary>
    /// <param name="context">上下文</param>
    /// <param name="action">动作</param>
    /// <param name="success">成功</param>
    /// <param name="remark">备注内容</param>
    public virtual void WriteHistory(DeviceContext context, String action, Boolean success, String remark)
    {
        if (context.Device is IDeviceModel2 device)
            device.WriteLog(action, success, remark);
    }
    #endregion
}