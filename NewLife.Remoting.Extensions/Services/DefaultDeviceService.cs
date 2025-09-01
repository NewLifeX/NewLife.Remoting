using System.Reflection;
using NewLife.Caching;
using NewLife.Log;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Security;
using NewLife.Serialization;
using XCode;

namespace NewLife.Remoting.Extensions.Services;

/// <summary>默认设备服务</summary>
/// <param name="sessionManager">会话管理器</param>
/// <param name="passwordProvider">密码提供者</param>
/// <param name="cacheProvider">缓存提供者</param>
/// <param name="serviceProvider">服务提供者</param>
public abstract class DefaultDeviceService<TDevice, TOnline>(ISessionManager sessionManager, IPasswordProvider passwordProvider, ICacheProvider cacheProvider, IServiceProvider serviceProvider) : IDeviceService2
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
    /// <summary>设备登录验证。内部支持动态注册</summary>
    /// <remarks>
    /// 内部流程：Authorize->Register(OnRegister)->OnLogin->GetOnline/CreateOnline
    /// </remarks>
    /// <param name="context">上下文</param>
    /// <param name="request">登录请求</param>
    /// <param name="source">登录来源</param>
    /// <returns></returns>
    /// <exception cref="ApiException"></exception>
    public virtual ILoginResponse Login(DeviceContext context, ILoginRequest request, String source)
    {
        if (request == null) throw new ArgumentOutOfRangeException(nameof(request));

        var code = request.Code;
        var device = code.IsNullOrEmpty() ? null : QueryDevice(code);
        //if (device == null && !code.IsNullOrEmpty()) device = QueryDevice(code);
        device ??= context.Device;
        if (device != null && !device.Enable) throw new ApiException(ApiCode.Forbidden, "禁止登录");
        if (device != null) context.Device = device;

        if (!source.IsNullOrEmpty()) context["Source"] = source;

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

        var rs = new LoginResponse
        {
            Code = device.Code,
            Name = device.Name
        };

        // 动态注册，下发节点证书
        if (autoReg && device is IDeviceModel2 device2) rs.Secret = device2.Secret;

        return rs;
    }

    /// <summary>验证设备合法性。验证密钥</summary>
    /// <param name="context">上下文</param>
    /// <param name="request">登录请求</param>
    /// <returns></returns>
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

    /// <summary>自动注册设备。验证密钥失败后</summary>
    /// <param name="context">上下文</param>
    /// <param name="request">登录请求</param>
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

    /// <summary>鉴权后的登录处理。修改设备信息、创建在线记录和写日志</summary>
    /// <param name="context">上下文</param>
    /// <param name="request">登录请求</param>
    public virtual void OnLogin(DeviceContext context, ILoginRequest request)
    {
        context.Online = GetOnline(context) ?? CreateOnline(context);

        var device = context.Device!;
        var source = context["Source"] as String;

        // 登录历史
        WriteHistory(context, source + "登录", true, $"[{device.Name}/{device.Code}]登录成功 " + request.ToJson(false, false, false));
    }

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
            //entity.Delete();

            RemoveOnline(context);
        }

        return online;
    }

    /// <summary>获取设备。先查缓存再查库</summary>
    /// <param name="code">设备编码</param>
    /// <returns></returns>
    public virtual IDeviceModel? GetDevice(String code)
    {
        var device = _cache.Get<IDeviceModel>($"Device:{code}");
        if (device != null) return device;

        device = QueryDevice(code);

        if (device != null) _cache.Set($"Device:{code}", device, 60);

        return device;
    }
    #endregion

    #region 心跳保活
    /// <summary>设备心跳。更新在线记录信息</summary>
    /// <remarks>
    /// 内部流程：OnPing、AcquireCommands
    /// </remarks>
    /// <param name="context">上下文</param>
    /// <param name="request">心跳请求</param>
    /// <param name="response">心跳响应。如果未传入则内部实例化</param>
    /// <returns>心跳响应</returns>
    public virtual IPingResponse Ping(DeviceContext context, IPingRequest? request, IPingResponse? response)
    {
        response ??= serviceProvider.GetService<IPingResponse>() ?? new PingResponse();

        response.Time = request?.Time ?? 0;
        response.ServerTime = DateTime.UtcNow.ToLong();

        OnPing(context, request);

        var rs = response as IPingResponse2;
        if (context.Device is IDeviceModel2 device)
        {
            response.Period = device.Period;
            if (rs != null) rs.NewServer = device.NewServer;
        }

        if (rs != null) rs.Commands = AcquireCommands(context);

        return response;
    }

    /// <summary>设备心跳。更新在线记录信息</summary>
    /// <remarks>
    /// 内部流程：GetOnline/CreateOnline、File
    /// </remarks>
    /// <param name="context">上下文</param>
    /// <param name="request">心跳请求</param>
    /// <returns></returns>
    public virtual IOnlineModel OnPing(DeviceContext context, IPingRequest? request)
    {
        var online = context.Online ?? GetOnline(context) ?? CreateOnline(context);
        context.Online = online;

        if (online is IOnlineModel2 online2)
        {
            online2.Save(request, context);
        }

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

    /// <summary>获取会话标识。用于唯一定位在线对象，写入查询数据库和缓存</summary>
    /// <param name="context"></param>
    /// <returns></returns>
    protected virtual String GetSessionId(DeviceContext context) => $"{context.Code ?? context.Device?.Code}@{context.ClientId ?? context.UserHost}";

    /// <summary>获取在线。先查缓存再查库</summary>
    /// <param name="context">上下文</param>
    /// <returns></returns>
    public virtual IOnlineModel? GetOnline(DeviceContext context)
    {
        var sid = GetSessionId(context);
        var online = _cache.Get<IOnlineModel>($"Online:{sid}");
        if (online != null)
        {
            //_cache.SetExpire($"Online:{sid}", TimeSpan.FromSeconds(600));
            return online;
        }

        online = QueryOnline(sid);

        if (online != null) _cache.Set($"Online:{sid}", online, 600);

        return online;
    }

    /// <summary>创建在线。先写数据库再写缓存</summary>
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

    /// <summary>删除在线。先删数据库再删缓存</summary>
    /// <param name="context">上下文</param>
    /// <returns></returns>
    public virtual Int32 RemoveOnline(DeviceContext context)
    {
        var sid = context.Online?.SessionId;
        if (sid.IsNullOrEmpty()) GetSessionId(context);

        if (context.Online is IEntity entity)
            entity.Delete();

        return _cache.Remove($"Online:{sid}");
    }

    /// <summary>获取下行命令</summary>
    /// <param name="context">上下文</param>
    /// <returns></returns>
    public virtual CommandModel[] AcquireCommands(DeviceContext context) => [];
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

    /// <summary>上报事件。默认批量写入设备历史表</summary>
    /// <param name="context">上下文</param>
    /// <param name="events"></param>
    /// <returns></returns>
    public virtual Int32 PostEvents(DeviceContext context, EventModel[] events)
    {
        if (context.Device is IDeviceModel2 device)
        {
            var list = new List<IEntity>();
            foreach (var model in events)
            {
                var entity = CreateEvent(context, device, model);
                list.Add(entity);
            }

            return list.Insert();
        }
        else
        {
            foreach (var model in events)
            {
                var success = !model.Type.EqualIgnoreCase("error");
                WriteHistory(context, model.Name ?? "事件", success, model.Remark!);
            }

            return events.Length;
        }
    }

    /// <summary>创建设备事件</summary>
    /// <param name="context">上下文</param>
    /// <param name="device">设备</param>
    /// <param name="model">事件</param>
    /// <returns></returns>
    /// <exception cref="InvalidDataException"></exception>
    protected virtual IEntity CreateEvent(DeviceContext context, IDeviceModel2 device, EventModel model)
    {
        //if (context.Device is not IDeviceModel2 device)
        //    throw new InvalidDataException($"创建事件对象需要设备实体类{typeof(TDevice).FullName}实现IDeviceModel2");

        var success = !model.Type.EqualIgnoreCase("error");
        var history = device.CreateHistory(model.Name ?? "事件", success, model.Remark!);
        if (history is IEntity entity)
        {
            var time = model.Time.ToDateTime().ToLocalTime();
            if (time.Year > 2000) entity.SetItem("CreateTime", time);
            return entity;
        }
        throw new InvalidDataException($"创建事件对象失败，设备实体类{typeof(TDevice).FullName}实现IDeviceModel2但CreateHistory返回空");
    }
    #endregion

    #region 辅助
    /// <summary>查找设备。反射调用FindByCode/FindByName</summary>
    /// <param name="code">编码</param>
    /// <returns></returns>
    public virtual IDeviceModel? QueryDevice(String code) => _findDevice?.Invoke(code);

    /// <summary>查找在线。反射调用FindBySessionId</summary>
    public virtual IOnlineModel? QueryOnline(String sessionId) => _findOnline?.Invoke(sessionId)!;

    /// <summary>写设备历史。扩展调用IDeviceModel2.WriteLog</summary>
    /// <param name="context">上下文</param>
    /// <param name="action">动作</param>
    /// <param name="success">成功</param>
    /// <param name="remark">备注内容</param>
    public virtual void WriteHistory(DeviceContext context, String action, Boolean success, String remark)
    {
        if (context.Device is IDeviceModel2 device)
        {
            var history = device.CreateHistory(action, success, remark);
            (history as IEntity)?.SaveAsync();
        }
        else if (context.Device is ILogProvider log)
            log.WriteLog(action, success, remark);
    }
    #endregion
}