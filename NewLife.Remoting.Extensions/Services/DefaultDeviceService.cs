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
/// <remarks>
/// 提供设备登录、注销、心跳、命令下发等核心功能的默认实现。
/// 使用泛型约束支持不同的设备和在线实体类型。
/// </remarks>
/// <typeparam name="TDevice">设备实体类型</typeparam>
/// <typeparam name="TOnline">在线实体类型</typeparam>
/// <param name="sessionManager">会话管理器</param>
/// <param name="passwordProvider">密码提供者</param>
/// <param name="cacheProvider">缓存提供者</param>
/// <param name="serviceProvider">服务提供者</param>
public abstract class DefaultDeviceService<TDevice, TOnline>(ISessionManager sessionManager, IPasswordProvider passwordProvider, ICacheProvider cacheProvider, IServiceProvider serviceProvider) : IDeviceService2
    where TDevice : Entity<TDevice>, IDeviceModel, new()
    where TOnline : Entity<TOnline>, IOnlineModel, new()
{
    #region 属性
    /// <summary>服务名</summary>
    public String Name { get; set; } = "Device";

    private readonly ICache _cache = cacheProvider.InnerCache;
    private static Func<String, TDevice?>? _findDevice;
    private static Func<String, TOnline?>? _findOnline;
    private readonly ITracer? _tracer = serviceProvider.GetService<ITracer>();
    #endregion

    #region 静态构造
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
    #endregion

    #region 登录注销
    /// <summary>设备登录验证。内部支持动态注册</summary>
    /// <remarks>
    /// 内部流程：Authorize->Register(OnRegister)->OnLogin->GetOnline/CreateOnline
    /// </remarks>
    /// <param name="context">设备上下文</param>
    /// <param name="request">登录请求</param>
    /// <param name="source">登录来源</param>
    /// <returns>登录响应</returns>
    /// <exception cref="ArgumentNullException">request为空时抛出</exception>
    /// <exception cref="ApiException">设备禁用或登录失败时抛出</exception>
    public virtual ILoginResponse Login(DeviceContext context, ILoginRequest request, String source)
    {
        if (request == null) throw new ArgumentOutOfRangeException(nameof(request));

        using var span = _tracer?.NewSpan($"{Name}Login", new { request.Code, request.ClientId, source });

        // 先查找设备，为下文的鉴权和注册做准备
        var code = request.Code;
        var device = context.Device;
        if (device == null && !code.IsNullOrEmpty()) device = QueryDevice(code);
        if (device != null && !device.Enable) throw new ApiException(ApiCode.Forbidden, "禁止登录");
        if (device != null)
        {
            context.Device = device;
            if (context.Code.IsNullOrEmpty()) context.Code = device.Code;
        }

        if (!request.ClientId.IsNullOrEmpty()) context.ClientId = request.ClientId;
        if (!source.IsNullOrEmpty()) context["Source"] = source;

        // 设备存在但密钥验证失败（可能配置被重置或密钥被改），
        // 将 device 置 null 以触发下方的自动注册流程，重新分配密钥。
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

        // 注册完成后，需要尽快更新上下文
        context.Device = device;
        if (context.Code.IsNullOrEmpty()) context.Code = device.Code;

        OnLogin(context, request);

        var rs = new LoginResponse
        {
            Name = device.Name
        };

        // 动态注册，下发节点证书
        if (autoReg && device is IDeviceModel2 device2)
        {
            rs.Code = device2.Code;
            rs.Secret = device2.Secret;
        }

        return rs;
    }

    /// <summary>验证设备合法性。验证密钥</summary>
    /// <remarks>
    /// 验证策略（按优先级）：
    /// 1. 设备无密钥（Secret 为空）→ 直接放行（首次注册场景）
    /// 2. 明文匹配（Secret 相等）→ 放行
    /// 3. passwordProvider.Verify 验证 → 放行
    /// 4. 均不通过 → 记录鉴权失败历史，返回 false
    /// 此外还会校验 UUID 一致性，防止客户端拷贝配置文件。
    /// </remarks>
    /// <param name="context">设备上下文</param>
    /// <param name="request">登录请求</param>
    /// <returns>验证是否通过</returns>
    public virtual Boolean Authorize(DeviceContext context, ILoginRequest request)
    {
        if (context.Device is not IDeviceModel2 device) return false;

        using var span = _tracer?.NewSpan($"{Name}Authorize", new { request.Code, request.ClientId });

        // 没有密码时无需验证
        if (device.Secret.IsNullOrEmpty()) return true;
        if (device.Secret.EqualIgnoreCase(request.Secret)) return true;

        if (request.Secret.IsNullOrEmpty() || !passwordProvider.Verify(device.Secret, request.Secret))
        {
            WriteHistory(context, Name + "鉴权", false, "密钥校验失败");
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

    /// <summary>自动注册设备。验证密钥失败后调用</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="request">登录请求</param>
    /// <returns>注册后的设备信息</returns>
    /// <exception cref="ApiException">注册失败时抛出</exception>
    public virtual IDeviceModel Register(DeviceContext context, ILoginRequest request)
    {
        var code = request.Code;
        if (code.IsNullOrEmpty() && request is ILoginRequest2 request2) code = request2.UUID.GetBytes().Crc().ToString("X8");
        if (code.IsNullOrEmpty()) code = Rand.NextString(8);

        using var span = _tracer?.NewSpan($"{Name}Register", new { code });

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

            // 框架默认启用设备。下游（如星尘）可在 OnRegister 中根据业务规则
            // 将 Enable 改回 false（例如需人工审核），此时 Login 仅返回证书不下发令牌。
            device.Enable = true;

            // 注册就必然更新密钥
            if (device is IDeviceModel2 device2)
                device2.Secret = Rand.NextString(16);

            OnRegister(context, request);

            WriteHistory(context, "动态注册", true, request.ToJson(false, false, false));
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);

            WriteHistory(context, "动态注册", false, $"[{code}/{device}]注册失败！{ex.Message}");

            throw;
        }

        return device;
    }

    /// <summary>注册中，填充业务数据并持久化。下游（如星尘）通常重写此方法来校验产品、设置产品编号等</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="request">登录请求</param>
    protected virtual void OnRegister(DeviceContext context, ILoginRequest request) => (context.Device as IEntity)!.Save();

    /// <summary>鉴权后的登录处理。修改设备信息、创建在线记录和写日志</summary>
    /// <remarks>
    /// 复用已有在线记录时刷新 LoginTime，标记新会话开始；首次登录则创建新记录。
    /// 无论注销还是超时，在线时长结算统一由 SettleOnline 负责。
    /// </remarks>
    /// <param name="context">设备上下文</param>
    /// <param name="request">登录请求</param>
    public virtual void OnLogin(DeviceContext context, ILoginRequest request)
    {
        var online = GetOnline(context);
        if (online != null)
        {
            // 复用已有在线记录，刷新本次登录时间
            if (online is IOnlineModel2 entity)
                entity.LoginTime = DateTime.Now;
        }
        else
        {
            online = CreateOnline(context);
        }
        context.Online = online;

        var device = context.Device!;
        var source = context["Source"] as String;

        // 登录历史
        WriteHistory(context, source + "登录", true, $"[{device.Name}/{device.Code}]登录成功 " + request.ToJson(false, false, false));
    }

    /// <summary>设备注销</summary>
    /// <remarks>
    /// 注销时结算在线时长，但不删除数据库记录。
    /// 记录保留供后续登录复用，最终由超时清理线程（<see cref="RemoveOnline"/>）删除。
    /// </remarks>
    /// <param name="context">设备上下文</param>
    /// <param name="reason">注销原因</param>
    /// <param name="source">登录来源</param>
    /// <returns>在线信息</returns>
    public virtual IOnlineModel? Logout(DeviceContext context, String? reason, String source)
    {
        using var span = _tracer?.NewSpan($"{Name}Logout", new { context.Code, reason, source });

        var online = context.Online;
        if (online is IEntity entity)
        {
            context.Online = online;

            var msg = $"{reason} [{context.Device}/{online.SessionId}]]登录于{entity["CreateTime"]}，最后活跃于{entity["UpdateTime"]}";
            WriteHistory(context, source + "设备下线", true, msg);
            //entity.Delete();

            // 结算本次会话在线时长，不清除数据库记录（留给超时清理）
            SettleOnline(online, context.Device);

            // 标记长连接断开
            if (online is IOnlineModel2 online2)
                online2.LongLink = false;

            entity.Update();
        }

        return online;
    }

    /// <summary>获取设备。先查缓存再查库</summary>
    /// <param name="code">设备编码</param>
    /// <returns>设备信息</returns>
    public virtual IDeviceModel? GetDevice(String code)
    {
        if (code.IsNullOrEmpty()) return null;

        var cacheKey = $"{Name}Device:{code}";
        var device = _cache.Get<IDeviceModel>(cacheKey);
        if (device != null) return device;

        device = QueryDevice(code);

        if (device != null) _cache.Set(cacheKey, device, 60);

        return device;
    }
    #endregion

    #region 心跳保活
    /// <summary>设备心跳。更新在线记录信息</summary>
    /// <remarks>
    /// 内部流程：OnPing、AcquireCommands
    /// </remarks>
    /// <param name="context">设备上下文</param>
    /// <param name="request">心跳请求</param>
    /// <param name="response">心跳响应。如果未传入则内部实例化</param>
    /// <returns>心跳响应</returns>
    public virtual IPingResponse Ping(DeviceContext context, IPingRequest? request, IPingResponse? response)
    {
        using var span = _tracer?.NewSpan($"{Name}Ping", new { context.Code, context.ClientId });

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
    /// 优先复用 context.Online，若无则 CreateOnline，然后 Save 心跳数据。
    /// Save 内部自行处理记录被过期清理等情况。
    /// </remarks>
    /// <param name="context">设备上下文</param>
    /// <param name="request">心跳请求</param>
    /// <returns>在线信息</returns>
    public virtual IOnlineModel OnPing(DeviceContext context, IPingRequest? request)
    {
        var online = context.Online ?? CreateOnline(context);
        context.Online = online;

        if (online is IOnlineModel2 online2)
        {
            // 保存心跳数据到在线记录
            online2.Save(request, context);
        }

        return online;
    }

    /// <summary>设置设备的长连接上线/下线</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="online">上线/下线状态</param>
    public virtual void SetOnline(DeviceContext context, Boolean online)
    {
        var olt = context.Online;
        if (olt is IOnlineModel2 online2)
        {
            online2.LongLink = online;
            if (olt is IEntity entity)
                entity.Update();
        }
        // 暂时为了兼容旧代码
        else if (olt is IEntity entity)
        {
            entity.SetItem("WebSocket", online);
            entity.Update();
        }
    }

    /// <summary>获取会话标识。用于唯一定位在线对象，写入查询数据库和缓存</summary>
    /// <param name="context">设备上下文</param>
    /// <returns>会话标识</returns>
    protected virtual String GetSessionId(DeviceContext context) => !context.ClientId.IsNullOrEmpty() ? context.ClientId : $"{context.Code ?? context.Device?.Code}@{context.UserHost}";

    /// <summary>获取在线。直接查数据库，不用缓存</summary>
    /// <remarks>
    /// 不使用缓存，每次直接查询数据库获取最新状态。
    /// 解决 LoginTime 多实例不一致导致的重复结算问题——缓存中可能存在同一记录的多个对象副本，
    /// 一个被 ClearExpire 结算（LoginTime=MinValue），另一个仍保留旧值，导致二次累加。
    /// 直接查库确保每次拿到的都是数据库中的权威状态，LoginTime 一致性由数据库保证。
    /// </remarks>
    /// <param name="context">设备上下文</param>
    /// <returns>在线信息</returns>
    public virtual IOnlineModel? GetOnline(DeviceContext context)
    {
        var sid = GetSessionId(context);
        if (sid.IsNullOrEmpty()) return null;

        return QueryOnline(sid);
    }

    /// <summary>创建在线。直接写数据库</summary>
    /// <param name="context">设备上下文</param>
    /// <returns>新创建的在线信息</returns>
    /// <exception cref="InvalidDataException">设备未实现IDeviceModel2时抛出</exception>
    public virtual IOnlineModel CreateOnline(DeviceContext context)
    {
        var sid = GetSessionId(context);
        var online = context.Online;
        if (online == null)
        {
            using var span = _tracer?.NewSpan($"{Name}CreateOnline", new { context.Code, context.ClientId });

            if (context.Device is not IDeviceModel2 device)
                throw new InvalidDataException($"创建在线对象需要{GetType().FullName}重载CreateOnline或者设备实体类{typeof(TDevice).FullName}实现IDeviceModel2");

            online = device.CreateOnline(sid);
            if (online is IEntity entity)
            {
                var now = DateTime.Now;
                if (entity is IOnlineModel2 online2) online2.LoginTime = now;

                entity.SetItem("CreateUser", Environment.MachineName);
                entity.SetItem("CreateIP", context.UserHost);
                entity.SetItem("CreateTime", now);

                entity.Save();
            }
        }

        return online;
    }

    /// <summary>删除在线。先结算在线时长再删数据库再删缓存</summary>
    /// <remarks>
    /// 由超时清理线程调用，统一完成：结算在线时长 → 删除数据库记录。
    /// 注销路径不走此方法（仅结算，不删库）。
    /// 在线记录不再使用缓存，故无需清缓存。
    /// </remarks>
    /// <param name="context">设备上下文</param>
    /// <returns>删除的记录数</returns>
    public virtual Int32 RemoveOnline(DeviceContext context)
    {
        var online = context.Online;
        if (online is IEntity entity)
        {
            // 结算本会话在线时长（LoginTime 守卫防重复）
            SettleOnline(online, context.Device);

            return entity.Delete();
        }

        return 0;
    }

    /// <summary>结算在线时长。依赖 <see cref="IOnlineModel2.LoginTime"/> 计算差值累加到设备，并重置 LoginTime 防止重复结算。非 IOnlineModel2 实现会静默跳过</summary>
    /// <param name="online">在线实体。需实现 <see cref="IOnlineModel2"/> 才支持 LoginTime 防重复守卫</param>
    /// <param name="device">设备信息</param>
    protected virtual void SettleOnline(IOnlineModel online, IDeviceModel? device)
    {
        if (device == null) return;
        if (online is not IEntity entity) return;
        if (online is not IOnlineModel2 online2) return;

        var loginTime = online2.LoginTime;
        if (loginTime.Year <= 2000) return;

        var sessionId = entity["SessionId"] as String;
        using var span = _tracer?.NewSpan($"{Name}SettleOnline", new { sessionId, loginTime });

        OnSettleOnline(online, device);

        // 重置 LoginTime 为 MinValue 并持久化，标记已结算，永久防重复
        online2.LoginTime = DateTime.MinValue;
        //entity.Update();
    }

    /// <summary>结算在线时长的扩展点。子类重写以将本次会话时长累加到设备实体</summary>
    /// <param name="online">在线实体</param>
    /// <param name="device">设备信息</param>
    protected virtual void OnSettleOnline(IOnlineModel online, IDeviceModel device) { }

    /// <summary>获取下行命令。默认返回空，子类可重写实现持久化命令查询</summary>
    /// <param name="context">设备上下文</param>
    /// <returns>待下发的命令列表</returns>
    public virtual CommandModel[] AcquireCommands(DeviceContext context) => [];
    #endregion

    #region 下行通知
    /// <summary>发送命令并等待响应。内部调用，不需要应用令牌</summary>
    /// <param name="device">目标设备</param>
    /// <param name="command">命令对象</param>
    /// <param name="timeout">超时秒数。0 不等待（fire-and-forget），大于 0 阻塞等待</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>命令响应，超时或 fire-and-forget 时返回 null</returns>
    public virtual Task<CommandReplyModel?> SendCommand(IDeviceModel device, CommandModel command, Int32 timeout = 0, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(command);

        var id = WriteHistory(device, "发送命令", true, command.ToJson(false, false, false));
        if (command.Id == 0) command.Id = id;

        return sessionManager.PublishAsync(device.Code, command, null, timeout, cancellationToken);
    }

    /// <summary>发送命令。外部平台级接口调用</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="model">命令输入模型</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>命令响应</returns>
    /// <exception cref="ArgumentNullException">设备编码为空或设备不存在时抛出</exception>
    public virtual async Task<CommandReplyModel?> SendCommand(DeviceContext context, CommandInModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        var target = GetDevice(model.Code!);
        if (target == null) throw new ArgumentNullException(nameof(model.Code), "未找到指定设备 " + model.Code);

        // 验证令牌。[AllowAnonymous] 接口（如 SendCommand）使用应用令牌而非设备令牌：
        // 若未注册 ITokenService（测试环境），则跳过；注册了则必须携带合法令牌。
        var tokenService = serviceProvider.GetService<ITokenService>();
        if (tokenService != null)
        {
            if (context.Token.IsNullOrEmpty())
                throw new ApiException(ApiCode.Unauthorized, "SendCommand 需要应用令牌");
            var (jwt, ex) = tokenService.DecodeToken(context.Token);
            if (ex != null) throw ex;
        }

        // 构建指令
        var now = DateTime.Now;
        var cmd = new CommandModel
        {
            Id = Rand.Next(),
            Command = model.Command,
            Argument = model.Argument,
            TraceId = DefaultSpan.Current?.TraceId,
        };
        if (model.StartTime > 0) cmd.StartTime = now.AddSeconds(model.StartTime);
        if (model.Expire > 0) cmd.Expire = now.AddSeconds(model.Expire);

        var reply = await SendCommand(target, cmd, model.Timeout, cancellationToken).ConfigureAwait(false);
        if (reply != null)
        {
            using var span = _tracer?.NewSpan($"cmd:CommandReply", reply);

            if (reply.Status == CommandStatus.错误)
                throw new Exception($"命令错误！{reply.Data}");
            else if (reply.Status == CommandStatus.取消)
                throw new Exception($"命令已取消！{reply.Data}");

            return reply;
        }

        return null;
    }

    /// <summary>命令响应</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="model">命令响应模型</param>
    /// <returns>处理结果</returns>
    public virtual Int32 CommandReply(DeviceContext context, CommandReplyModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        // 通过会话管理器内置的响应事件总线广播响应（fire-and-forget，跨实例广播不阻塞）
        _ = sessionManager.PublishResponseAsync(model, default);

        WriteHistory(context, "命令响应", true, model.ToJson(false, false, false));

        return 1;
    }

    #endregion

    #region 升级更新
    /// <summary>升级检查</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="channel">更新通道</param>
    /// <returns>升级信息</returns>
    public virtual IUpgradeInfo? Upgrade(DeviceContext context, String? channel) => null;
    #endregion

    #region 事件上报
    /// <summary>上报事件。默认批量写入设备历史表</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="events">事件集合</param>
    /// <returns>处理的事件数量</returns>
    public virtual Int32 PostEvents(DeviceContext context, EventModel[] events)
    {
        if (events == null || events.Length == 0) return 0;

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
    /// <param name="context">设备上下文</param>
    /// <param name="device">设备信息</param>
    /// <param name="model">事件模型</param>
    /// <returns>事件实体</returns>
    /// <exception cref="InvalidDataException">创建失败时抛出</exception>
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
    /// <param name="code">设备编码</param>
    /// <returns>设备信息</returns>
    public virtual IDeviceModel? QueryDevice(String code) => _findDevice?.Invoke(code);

    /// <summary>查找在线。反射调用FindBySessionId</summary>
    /// <param name="sessionId">会话标识</param>
    /// <returns>在线信息</returns>
    public virtual IOnlineModel? QueryOnline(String sessionId) => _findOnline?.Invoke(sessionId)!;

    /// <summary>写设备历史。扩展调用IDeviceModel2.WriteLog</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="action">动作名称</param>
    /// <param name="success">是否成功</param>
    /// <param name="remark">备注内容</param>
    public virtual void WriteHistory(DeviceContext context, String action, Boolean success, String remark)
    {
        DefaultSpan.Current?.AppendTag($"[{action}]{remark}");

        if (context.Device is IDeviceModel2 device2)
        {
            var history = device2.CreateHistory(action, success, remark);
            (history as IEntity)?.SaveAsync();
        }
        else if (context.Device is ILogProvider log)
        {
            log.WriteLog(action, success, remark);
        }
    }

    /// <summary>写设备历史。扩展调用IDeviceModel2.WriteLog</summary>
    /// <param name="device">设备信息</param>
    /// <param name="action">动作名称</param>
    /// <param name="success">是否成功</param>
    /// <param name="remark">备注内容</param>
    /// <returns>历史记录ID</returns>
    public virtual Int64 WriteHistory(IDeviceModel device, String action, Boolean success, String remark)
    {
        DefaultSpan.Current?.AppendTag($"[{action}]{remark}");

        if (device is IDeviceModel2 device2)
        {
            var history = device2.CreateHistory(action, success, remark);
            if (history is IEntity entity)
            {
                entity.Insert();
                return entity["Id"].ToLong();
            }
        }
        else if (device is ILogProvider log)
        {
            log.WriteLog(action, success, remark);
        }

        return 0;
    }
    #endregion
}