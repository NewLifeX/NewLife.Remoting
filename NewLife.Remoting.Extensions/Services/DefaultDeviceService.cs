using System.Diagnostics.CodeAnalysis;
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
/// <para>安全策略默认从严，按需通过参数或继承扩展：</para>
/// <list type="bullet">
/// <item>密钥校验：重写 <see cref="Authorize"/>。</item>
/// <item>唯一标识校验：重写 <see cref="ValidateIdentity"/>，或用 <see cref="AllowIdentityMismatch"/> 放宽。</item>
/// <item>鉴权失败是否允许重置密钥：<see cref="AllowSecretResetOnAuthFailure"/>，默认 false。</item>
/// <item>注册流程：重写 <see cref="Register"/> / <see cref="OnRegister"/>。</item>
/// <item>命令下发归属：重写 <see cref="AuthorizeSendCommand"/>。</item>
/// </list>
/// </remarks>
/// <typeparam name="TDevice">设备实体类型</typeparam>
/// <typeparam name="TOnline">在线实体类型</typeparam>
/// <param name="sessionManager">会话管理器</param>
/// <param name="passwordProvider">密码提供者</param>
/// <param name="cacheProvider">缓存提供者</param>
/// <param name="serviceProvider">服务提供者</param>
/// <remarks>
/// 设备与在线实体类型需要保留公开方法：静态构造按名称查找 <c>FindByCode</c>/<c>FindByName</c>/<c>FindBySessionId</c>，
/// 并编译为强类型委托。NativeAOT 与裁剪下若成员被移除，将退化为空实现（找不到设备）。
/// 另需保留公开属性：<see cref="Entity{TEntity}"/> 的泛型注解要求 <c>PublicProperties</c>（XCode v12.2 AOT 基线）。
/// 防拷贝校验要求设备实体提供名为 <c>Uuid</c> 或 <c>UUID</c> 的公开字符串属性；缺失时跳过该校验
/// </remarks>
public abstract class DefaultDeviceService<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties)] TDevice, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties)] TOnline>(ISessionManager sessionManager, IPasswordProvider passwordProvider, ICacheProvider cacheProvider, IServiceProvider serviceProvider) : IDeviceService2
    where TDevice : Entity<TDevice>, IDeviceModel, new()
    where TOnline : Entity<TOnline>, IOnlineModel, new()
{
    #region 属性
    /// <summary>服务名</summary>
    public String Name { get; set; } = "Device";

    /// <summary>唯一标识不一致时是否仍允许登录。默认 false，防止拷贝配置文件后冒用设备身份</summary>
    /// <remarks>
    /// 设备首次注册时记录 UUID，后续登录必须一致。仅当同一份配置被合法共享（如同一设备多进程）时，
    /// 才可显式置为 true 退回“只写历史告警”的旧行为。
    /// </remarks>
    public Boolean AllowIdentityMismatch { get; set; }

    /// <summary>鉴权失败时是否允许重新注册并重置设备密钥。默认 false</summary>
    /// <remarks>
    /// 默认拒绝。设备编码是公开信息（IMEI、序列号等），凭编码重置密钥等同于允许设备被接管。
    /// 需要“配置丢失/固件重刷后自动恢复”的产品宿主，可显式置为 true；
    /// 是否真正允许仍由下游 <c>AutoRegister</c>/<see cref="OnRegister"/> 决定。
    /// </remarks>
    public Boolean AllowSecretResetOnAuthFailure { get; set; }

    private readonly ICache _cache = cacheProvider.InnerCache;
    private static Func<String, TDevice?>? _findDevice;
    private static Func<String, TOnline?>? _findOnline;
    private static readonly PropertyInfo? _identityProperty;
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
        {
            // 唯一标识属性。不能用 IEntity 索引器：索引器只访问动态字段字典，不会落到数据库列
            var pi = typeof(TDevice).GetProperty("Uuid") ?? typeof(TDevice).GetProperty("UUID");
            if (pi != null && pi.PropertyType == typeof(String) && pi.CanRead && pi.CanWrite) _identityProperty = pi;
        }
    }
    #endregion

    #region 登录注销
    /// <summary>设备登录验证。内部支持动态注册</summary>
    /// <remarks>
    /// 内部流程：查设备 → <see cref="Authorize"/> 校验密钥 → <see cref="ValidateIdentity"/> 校验唯一标识 →
    /// <see cref="Register"/>/<see cref="OnRegister"/>（首次注册或允许重置时）→ <see cref="OnLogin"/> → GetOnline/CreateOnline。
    /// 密钥或唯一标识校验失败默认拒绝登录；允许重置或放宽不一致需显式设置对应开关。
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
        if (device != null && !device.Enable) throw new ApiException(ApiCode.Forbidden, L.T("Login is forbidden"));
        if (device != null)
        {
            context.Device = device;
            if (context.Code.IsNullOrEmpty()) context.Code = device.Code;
        }

        if (!request.ClientId.IsNullOrEmpty()) context.ClientId = request.ClientId;
        if (!source.IsNullOrEmpty()) context["Source"] = source;

        if (device != null)
        {
            // 密钥验证失败：默认直接拒绝；显式允许重置时清空设备，转入动态注册流程，
            // 是否真正允许仍由下游 AutoRegister/OnRegister 决定。
            if (!Authorize(context, request))
            {
                if (!AllowSecretResetOnAuthFailure) throw new ApiException(ApiCode.Unauthorized, L.T("Login failed"));

                device = null;
            }
            // 密钥通过后再校验唯一标识：不一致属于身份冒用，直接拒绝，不能进入动态注册重置密钥
            else if (!ValidateIdentity(context, device, request))
            {
                throw new ApiException(ApiCode.Unauthorized, L.T("Login failed"));
            }
        }

        // 历史设备缺少唯一标识时，在鉴权成功后补录；下次登录即可启用防拷贝校验
        if (device != null) BindIdentity(device, request);

        var autoReg = false;
        if (device == null)
        {
            device = Register(context, request);
            autoReg = true;
        }

        if (device == null) throw new ApiException(ApiCode.Unauthorized, L.T("Login failed"));

        // 注册完成后，需要尽快更新上下文
        context.Device = device;
        if (context.Code.IsNullOrEmpty()) context.Code = device.Code;

        OnLogin(context, request);

        // 登录后统一更新设备和在线信息
        (device as IEntity)?.Update();
        (context.Online as IEntity)?.Update();

        var rs = new LoginResponse
        {
            Name = device.Name,
            DefaultLanguage = Localization.DefaultCulture?.Name ?? Localization.ForcedLanguage,
        };

        // 动态注册，下发节点证书
        if (autoReg && device is IDeviceModel2 device2)
        {
            rs.Code = device2.Code;
            rs.Secret = device2.Secret;
        }

        return rs;
    }

    /// <summary>校验设备唯一标识，防止客户端拷贝配置</summary>
    /// <remarks>不一致时写历史并按 <see cref="AllowIdentityMismatch"/> 决定是否放行；子类可重写实现更细的归属校验</remarks>
    /// <param name="context">设备上下文</param>
    /// <param name="device">设备实体</param>
    /// <param name="request">登录请求</param>
    /// <returns>一致、未启用校验或允许不一致时返回 true</returns>
    protected virtual Boolean ValidateIdentity(DeviceContext context, IDeviceModel device, ILoginRequest request)
    {
        if (request is not ILoginRequest2 request2 || _identityProperty == null) return true;

        var uuid = request2.UUID;
        var uuid2 = _identityProperty.GetValue(device) as String;
        if (uuid.IsNullOrEmpty() || uuid2.IsNullOrEmpty() || uuid == uuid2) return true;

        WriteHistory(context, DeviceActions.IdentityMismatch, false, $"Unique identifier mismatch (new != old): {uuid} != {uuid2}");

        return AllowIdentityMismatch;
    }

    /// <summary>绑定设备唯一标识。仅首次登记或历史数据缺失时写入，避免客户端每次登录都能改写</summary>
    /// <param name="device">设备实体</param>
    /// <param name="request">登录请求</param>
    private static void BindIdentity(IDeviceModel device, ILoginRequest request)
    {
        if (_identityProperty == null || request is not ILoginRequest2 request2 || request2.UUID.IsNullOrEmpty()) return;

        var uuid = _identityProperty.GetValue(device) as String;
        if (uuid.IsNullOrEmpty()) _identityProperty.SetValue(device, request2.UUID);
    }

    /// <summary>验证设备合法性。验证密钥</summary>
    /// <remarks>
    /// 验证策略（按优先级）：
    /// 1. 设备无密钥（Secret 为空）→ 通过密钥校验
    /// 2. 明文匹配（Secret 相等）→ 通过密钥校验
    /// 3. passwordProvider.Verify 验证 → 通过密钥校验
    /// 4. 均不通过 → 记录鉴权失败历史，返回 false
    /// 唯一标识校验不在此处进行，由登录流程在密钥通过后单独校验。
    /// </remarks>
    /// <param name="context">设备上下文</param>
    /// <param name="request">登录请求</param>
    /// <returns>验证是否通过</returns>
    public virtual Boolean Authorize(DeviceContext context, ILoginRequest request)
    {
        if (context.Device is not IDeviceModel2 device) return false;

        using var span = _tracer?.NewSpan($"{Name}Authorize", new { request.Code, request.ClientId });

        // 密钥校验不要提前返回：明文匹配与散列验证都必须完整执行
        var verified = device.Secret.IsNullOrEmpty()
            || device.Secret.EqualIgnoreCase(request.Secret)
            || (!request.Secret.IsNullOrEmpty() && passwordProvider.Verify(device.Secret, request.Secret));
        if (!verified)
        {
            WriteHistory(context, DeviceActions.DeviceAuthentication, false, "Secret validation failed.");
            return false;
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
        var backup = (device as IEntity)?.CloneEntity(false);
        var oldUuid = device != null ? _identityProperty?.GetValue(device) as String : null;
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

            // 首次登记绑定唯一标识，后续登录据此识别配置拷贝
            BindIdentity(device, request);

            OnRegister(context, request);

            WriteHistory(context, DeviceActions.DynamicRegistration, true, request.ToJson(false, false, false));
        }
        catch (Exception ex)
        {
            // 注册失败时恢复到注册前状态，避免未保存的密钥/标识等变更污染实体缓存。
            // CloneEntity 只覆盖数据库字段，唯一标识可能是非映射属性，需单独恢复。
            if (device is IEntity entity && backup != null) entity.CopyFrom(backup, true, false);
            if (device != null && _identityProperty != null) _identityProperty.SetValue(device, oldUuid);

            span?.SetError(ex, null);

            WriteHistory(context, DeviceActions.DynamicRegistration, false, $"[{code}/{device}] registration failed: {ex.Message}");

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
    /// 本方法仅设置属性不持久化——LoginTime、设备信息等修改由调用方 <see cref="Login"/> 方法
    /// 在返回前统一执行 <c>(context.Online as IEntity)?.Update()</c> 写入数据库，
    /// 子类重写时无需单独保存在线记录。
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

        // 登录历史
        WriteHistory(context, DeviceActions.DeviceLogin, true, $"[{device.Name}/{device.Code}] login successful " + request.ToJson(false, false, false));
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

            var name = context.Device?.Name;
            if (name.IsNullOrEmpty()) name = context.Device?.Code;
            if (name.IsNullOrEmpty()) name = context.Code;

            var msg = DeviceActions.FormatOfflineRemark(reason, name, online.SessionId, entity["CreateTime"].ToDateTime(), entity["UpdateTime"].ToDateTime());
            WriteHistory(context, DeviceActions.DeviceOffline, true, msg);
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

        // 默认语言随内置响应模型下发，服务端注入的自定义实现自行处理
        if (response is PingResponse ping)
            ping.DefaultLanguage = Localization.DefaultCulture?.Name ?? Localization.ForcedLanguage;

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
                throw new InvalidDataException(L.T("Creating an online object requires {0} to override CreateOnline or the device entity {1} to implement IDeviceModel2", GetType().FullName, typeof(TDevice).FullName));

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

        // 优先使用 SessionID（大写 D），兼容各实体的不同命名
        var sessionId = (entity["SessionID"] ?? entity["SessionId"]) as String;
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

        var id = WriteHistory(device, DeviceActions.CommandSent, true, command.ToJson(false, false, false));
        if (command.Id == 0) command.Id = id;

        return sessionManager.PublishAsync(device.Code, command, null, timeout, cancellationToken);
    }

    /// <summary>发送命令。外部平台级接口调用</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="model">命令输入模型</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>命令响应</returns>
    /// <exception cref="ApiException">令牌缺失/无效、刷新令牌误用、设备不存在或无权下发时抛出</exception>
    public virtual async Task<CommandReplyModel?> SendCommand(DeviceContext context, CommandInModel model, CancellationToken cancellationToken = default)
    {
        if (model == null) throw new ApiException(ApiCode.BadRequest, L.T("[{0}] is invalid!", "model"));

        // 鉴权：该接口标[AllowAnonymous]，网关层不拦，必须在这里自己保证。
        // 令牌只证明“是谁”，不证明“能不能操作这台设备”，因此完整校验分两步：签名有效 + 归属获准。
        // 先验令牌再查设备：未授权请求不应触达数据库，也避免通过“设备不存在”的差异探测设备编码。
        var tokenService = serviceProvider.GetService<ITokenService>();
        if (tokenService == null)
            throw new ApiException(ApiCode.Unauthorized, L.T("SendCommand requires an application token"));

        if (context.Token.IsNullOrEmpty())
            throw new ApiException(ApiCode.Unauthorized, L.T("SendCommand requires an application token"));

        var (jwt, ex) = tokenService.DecodeToken(context.Token);
        if (ex != null) throw ex;

        // 刷新令牌只能换取新的访问令牌，不能直接作为调用凭据
        if ("refresh".EqualIgnoreCase(jwt["kind"] + ""))
            throw new ApiException(ApiCode.Unauthorized, L.T("SendCommand requires an application token"));

        var target = GetDevice(model.Code!);
        if (target == null) throw new ApiException(ApiCode.NotFound, L.T("Cannot find device {0}", model.Code));

        // 令牌只证明“是谁”，不证明“能不能操作这台设备”，归属规则交给扩展点
        if (!AuthorizeSendCommand(context, jwt.Subject, target))
            throw new ApiException(ApiCode.Forbidden, L.T("No permission to send command to device {0}", model.Code));

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
                throw new Exception(L.T("Command error! {0}", reply.Data));
            else if (reply.Status == CommandStatus.取消)
                throw new Exception(L.T("Command canceled! {0}", reply.Data));

            return reply;
        }

        return null;
    }

    /// <summary>校验调用方是否有权向目标设备下发命令</summary>
    /// <remarks>
    /// 令牌只证明调用方身份，不证明其对目标设备的权限。默认拒绝设备自身令牌（subject 命中设备编码），
    /// 避免任一设备登录后横向控制其它设备；应用令牌默认放行，保证单应用部署开箱可用。
    /// 多应用、多租户或按产品授权的部署必须覆写本方法，校验调用方（如应用 AppId、所属产品）
    /// 与目标设备的归属关系，也可直接抛异常给出具体原因。
    /// </remarks>
    /// <param name="context">设备上下文，其中 Token 的签名已校验</param>
    /// <param name="subject">令牌主体，已验签</param>
    /// <param name="target">目标设备</param>
    /// <returns>是否允许下发，false 时按无权限拒绝</returns>
    protected virtual Boolean AuthorizeSendCommand(DeviceContext context, String? subject, IDeviceModel target)
    {
        if (subject.IsNullOrEmpty()) return true;

        // subject 命中设备编码，说明这是设备登录签发的令牌，不具备下发命令权限
        return GetDevice(subject) == null;
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

        WriteHistory(context, DeviceActions.CommandReply, true, model.ToJson(false, false, false));

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
                WriteHistory(context, model.Name ?? DeviceActions.DeviceEvent, success, model.Remark!);
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
        var history = device.CreateHistory(model.Name ?? DeviceActions.DeviceEvent, success, model.Remark!);
        if (history is IEntity entity)
        {
            var time = model.Time.ToDateTime().ToLocalTime();
            if (time.Year > 2000) entity.SetItem("CreateTime", time);
            return entity;
        }
        throw new InvalidDataException(L.T("Failed to create the event object: the device entity {0} implements IDeviceModel2 but CreateHistory returned null", typeof(TDevice).FullName));
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
    /// <param name="action">动作名称。使用 <see cref="DeviceActions"/> 的语言无关规范键，禁止写入随写入进程语言变化的文本</param>
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
    /// <param name="action">动作名称。使用 <see cref="DeviceActions"/> 的语言无关规范键，禁止写入随写入进程语言变化的文本</param>
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