using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;
using NewLife.Remoting.Extensions.Services;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Security;
using NewLife.Serialization;

namespace NewLife.Remoting.Extensions.Endpoints;

/// <summary>设备通信端点处理器。最小 API 宿主的入口，与 BaseDeviceController 实现同一套端点协议</summary>
/// <remarks>
/// 面向 NativeAOT：不依赖 MVC 与反射式模型绑定。
/// 请求体由 <see cref="JsonHelper"/> 反序列化为接口在 DI 中的实现类型，响应统一封装为 code/data 信封，
/// 与 MVC 宿主的 <see cref="ApiFilterAttribute"/> 行为对齐，便于设备客户端无缝切换。
/// 
/// 与 <c>BaseDeviceController</c> 的对应端点互为镜像实现，修改端点逻辑须两侧同步；
/// 行为一致性由 RemotingEndpointTests 与 BaseDeviceControllerTests 的镜像用例锁定。
/// </remarks>
internal static class RemotingEndpoints
{
    /// <summary>需要保留的内置模型元数据。FastJson 按运行时类型读写，AOT 下需保留构造与公开成员</summary>
    private const DynamicallyAccessedMemberTypes _ModelMembers = DynamicallyAccessedMemberTypes.PublicParameterlessConstructor
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicFields;

    #region 端点
    /// <summary>设备登录</summary>
    /// <param name="http">HTTP 上下文</param>
    /// <returns></returns>
    // 与 BaseDeviceController.Login 互为镜像，修改须同步
    public static Task LoginAsync(HttpContext http) => InvokeAsync(http, true, async (ctx, sp) =>
    {
        var request = await ReadModelAsync<ILoginRequest>(http).ConfigureAwait(false);
        if (request == null) throw new ArgumentNullException(nameof(ILoginRequest));

        var deviceService = sp.GetRequiredService<IDeviceService>();
        var tokenService = sp.GetRequiredService<ITokenService>();

        // 先查一次，后续即使登录失败，也可以写设备历史
        if (!request.Code.IsNullOrEmpty()) ctx.Device = deviceService.QueryDevice(request.Code);

        var rs = deviceService.Login(ctx, request, "Http");

        var dv = ctx.Device;
        if (dv == null) throw new ApiException(ApiCode.Forbidden, L.T("Device not found"));

        rs ??= new LoginResponse { Name = dv.Name, };

        if (request is ILoginRequest2 req) rs.Time = req.Time;
        rs.ServerTime = DateTime.UtcNow.ToLong();

        // 动态注册（自动注册）时，下游（如星尘）可能在 OnRegister 中将设备设为禁用（Enable=false），
        // 表示"已注册但需人工审核"。此时仅返回设备证书（Code+Secret），不颁发访问令牌。
        // 设备需待管理员在后台启用后方可正常通信。
        if (dv.Enable)
        {
            if (request.ClientId.IsNullOrEmpty()) ctx.ClientId = Rand.NextString(8);
            var tm = tokenService.IssueToken(dv.Code, ctx.ClientId);

            rs.Token = tm.AccessToken;
            rs.Expire = tm.ExpireIn;
        }

        return rs;
    });

    /// <summary>设备注销</summary>
    /// <param name="http">HTTP 上下文</param>
    /// <returns></returns>
    // 与 BaseDeviceController.Logout 互为镜像，修改须同步
    public static Task LogoutAsync(HttpContext http) => InvokeAsync(http, false, (ctx, sp) =>
    {
        var deviceService = sp.GetRequiredService<IDeviceService>();
        var tokenService = sp.GetRequiredService<ITokenService>();

        var reason = http.Request.Query["reason"] + "";

        deviceService.Logout(ctx, reason.IsNullOrEmpty() ? null : reason, "Http");

        // 注销后吊销当前访问令牌，防止令牌在有效期内被继续复用（V-14 吊销）
        if (!ctx.Token.IsNullOrEmpty())
            tokenService.RevokeToken(ctx.Token);

        return Task.FromResult<Object?>(new LogoutResponse
        {
            Name = ctx.Device?.Name,
            Token = null,
        });
    });

    /// <summary>设备心跳</summary>
    /// <param name="http">HTTP 上下文</param>
    /// <returns></returns>
    // 与 BaseDeviceController.Ping 互为镜像，修改须同步
    public static Task PingAsync(HttpContext http) => InvokeAsync(http, false, async (ctx, sp) =>
    {
        var request = await ReadModelAsync<IPingRequest>(http).ConfigureAwait(false);

        var deviceService = sp.GetRequiredService<IDeviceService>();
        var tokenService = sp.GetRequiredService<ITokenService>();

        var rs = deviceService.Ping(ctx, request, null) ?? new PingResponse();

        var device = ctx.Device;
        if (device != null && ctx.Token != null)
        {
            // 令牌续期：利用心跳请求（典型间隔 60s）做无感续期，避免设备令牌过期后重新登录。
            // 当令牌剩余有效期不足 10 分钟时，以原 Subject 和 Id 颁发新令牌，随心跳响应下发。
            // 10 分钟窗口配合 60s 心跳周期提供约 10 次续期机会——即使单次响应丢失，
            // 下次心跳仍会再次触发，实现自愈式的令牌刷新。
            var (jwt, ex) = tokenService.DecodeToken(ctx.Token);
            if (ex == null && jwt != null && jwt.Expire < DateTime.Now.AddMinutes(10))
            {
                using var span = sp.GetService<ITracer>()?.NewSpan("RefreshToken", new { device.Code, jwt.Subject });

                var tm = tokenService.IssueToken(device.Code, jwt.Id);
                rs.Token = tm.AccessToken;
            }
        }

        return rs;
    });

    /// <summary>升级检查</summary>
    /// <param name="http">HTTP 上下文</param>
    /// <returns></returns>
    // 与 BaseDeviceController.Upgrade 互为镜像，修改须同步
    public static Task UpgradeAsync(HttpContext http) => InvokeAsync(http, false, (ctx, sp) =>
    {
        var deviceService = sp.GetRequiredService<IDeviceService>();

        var channel = http.Request.Query["channel"] + "";

        if (ctx.Device == null) throw new ApiException(ApiCode.Unauthorized, L.T("Not logged in"));

        // 提取基础路径（scheme://host），兼容 http/https
        var uri = http.Request.GetRawUrl().ToString();
        var p = uri.IndexOf("://", StringComparison.Ordinal);
        if (p > 0)
        {
            p = uri.IndexOf('/', p + 3);
            if (p > 0) uri = uri[..p];
        }

        var info = deviceService.Upgrade(ctx, channel.IsNullOrEmpty() ? null : channel);

        // 为了兼容旧版本客户端，这里必须把路径处理为绝对路径
        if (info != null && !info.Source.StartsWithIgnoreCase("http://", "https://"))
        {
            info.Source = new Uri(new Uri(uri), info.Source) + "";
        }

        return Task.FromResult<Object?>(info);
    });

    /// <summary>下行通知。建立WebSocket长连接</summary>
    /// <param name="http">HTTP 上下文</param>
    /// <returns></returns>
    // 与 BaseDeviceController.Notify 互为镜像，修改须同步
    public static Task NotifyAsync(HttpContext http)
    {
        // 与 MVC 一致：非WebSocket请求按 400 业务码封装返回，不让客户端空等
        if (!http.WebSockets.IsWebSocketRequest) return WriteResultAsync(http, ApiCode.BadRequest, null, L.T("Not a WebSocket request"));

        return InvokeAsync(http, false, async (ctx, sp) =>
        {
            using var socket = await http.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);

            using var session = new WsCommandSession(socket);
            await HandleNotifyAsync(ctx, http, sp, session, "cmd:Ws:Create", http.RequestAborted).ConfigureAwait(false);

            return null;
        }, false);
    }

    /// <summary>SSE 下行通知。WebSocket 不可用时的降级通道</summary>
    /// <param name="http">HTTP 上下文</param>
    /// <returns></returns>
    // 与 BaseDeviceController.NotifySSE 互为镜像，修改须同步
    public static Task NotifySSEAsync(HttpContext http) => InvokeAsync(http, false, async (ctx, sp) =>
    {
        using var session = new SseCommandSession(http.Response, "", sp);
        await HandleNotifyAsync(ctx, http, sp, session, "cmd:SSE:Create", http.RequestAborted).ConfigureAwait(false);

        return null;
    }, false);

    /// <summary>设备端响应服务调用</summary>
    /// <param name="http">HTTP 上下文</param>
    /// <returns></returns>
    // 与 BaseDeviceController.CommandReply 互为镜像，修改须同步
    public static Task CommandReplyAsync(HttpContext http) => InvokeAsync(http, false, async (ctx, sp) =>
    {
        var model = await ReadModelAsync<CommandReplyModel>(http).ConfigureAwait(false);
        if (model == null) throw new ArgumentNullException(nameof(CommandReplyModel));

        var deviceService = sp.GetRequiredService<IDeviceService>();

        return deviceService.CommandReply(ctx, model);
    });

    /// <summary>向节点发送命令。通知节点更新、安装和启停应用等</summary>
    /// <param name="http">HTTP 上下文</param>
    /// <returns></returns>
    // 与 BaseDeviceController.SendCommand 互为镜像，修改须同步
    public static Task SendCommandAsync(HttpContext http) => InvokeAsync(http, true, async (ctx, sp) =>
    {
        var model = await ReadModelAsync<CommandInModel>(http).ConfigureAwait(false);
        if (model == null) throw new ArgumentNullException(nameof(CommandInModel));
        if (model.Code.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.Code), L.T("Code must be specified"));
        if (model.Command.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.Command), L.T("Command must be specified"));

        var deviceService = sp.GetRequiredService<IDeviceService>();

        return await deviceService.SendCommand(ctx, model, http.RequestAborted).ConfigureAwait(false);
    });

    /// <summary>批量上报事件</summary>
    /// <param name="http">HTTP 上下文</param>
    /// <returns></returns>
    // 与 BaseDeviceController.PostEvents 互为镜像，修改须同步
    public static Task PostEventsAsync(HttpContext http) => InvokeAsync(http, false, async (ctx, sp) =>
    {
        var events = await ReadModelAsync<EventModel[]>(http).ConfigureAwait(false);

        var deviceService = sp.GetRequiredService<IDeviceService>();

        return deviceService.PostEvents(ctx, events ?? []);
    });
    #endregion

    #region 管线
    /// <summary>验证令牌并装载设备上下文。与 BaseController.OnAuthorize 的鉴权段互为镜像，修改须同步</summary>
    /// <param name="token">访问令牌</param>
    /// <param name="context">设备上下文</param>
    /// <param name="tokenService">令牌服务</param>
    /// <param name="deviceService">设备服务。为空时只做令牌解码，不装载设备</param>
    /// <returns>验证是否通过</returns>
    /// <exception cref="ApiException">令牌无效、刷新令牌误用或设备不可用时抛出</exception>
    private static Boolean Authenticate(String? token, DeviceContext context, ITokenService tokenService, IDeviceService? deviceService)
    {
        if (token.IsNullOrEmpty()) return false;

        var (jwt, ex) = tokenService.DecodeToken(token);

        // 刷新令牌只能用于刷新，禁止作为访问令牌调用业务接口（V-14 令牌分离）
        if (ex == null && jwt != null && jwt["kind"] + "" == "refresh")
            ex = new ApiException(ApiCode.Unauthorized, L.T("A refresh token cannot be used as an access token!"));

        var code = context.Code = jwt?.Subject;
        context.ClientId = jwt?.Id!;
        context["__Jwt"] = jwt;

        var span = DefaultSpan.Current;
        span?.AppendTag($"code={context.Code} clientId={context.ClientId}");

        // 如果注入了设备服务，尝试获取设备/在线信息。
        // 此处收集所有异常（设备不存在/已禁用/解码失败等），统一在下方抛出，
        // 避免多次 try-catch 分散异常处理逻辑。
        if (deviceService != null)
        {
            if (code.IsNullOrEmpty()) return false;

            var ds2 = deviceService as IDeviceService2;

            var dv = ds2 != null ? ds2.GetDevice(code) : deviceService.QueryDevice(code);
            if (dv == null || !dv.Enable) ex ??= new ApiException(ApiCode.Forbidden, L.T("Invalid client!"));

            context.Device = dv!;

            if (ds2 != null && context.Online == null) context.Online = ds2.GetOnline(context);
        }

        if (ex != null) throw ex;

        return jwt != null;
    }

    /// <summary>把已鉴权的身份映射为 ClaimsPrincipal，便于端点内使用 User 或授权中间件</summary>
    /// <param name="context">设备上下文</param>
    /// <returns>身份主体</returns>
    private static ClaimsPrincipal BuildPrincipal(DeviceContext context)
    {
        var code = context.Code;
        var claims = new List<Claim>(8);
        if (!code.IsNullOrEmpty())
        {
            claims.Add(new Claim("code", code));
            claims.Add(new Claim(ClaimTypes.NameIdentifier, code));
        }
        if (!context.ClientId.IsNullOrEmpty()) claims.Add(new Claim("client_id", context.ClientId));

        var identity = new ClaimsIdentity(claims, "ApiToken");
        return new ClaimsPrincipal(identity);
    }

    /// <summary>处理通知会话。与 BaseDeviceController.HandleNotify 互为镜像，修改须同步</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="http">HTTP 上下文，会挂到设备上下文供会话等待使用</param>
    /// <param name="sp">服务提供者</param>
    /// <param name="session">命令会话</param>
    /// <param name="spanName">Span名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    /// <exception cref="ApiException">未登录时抛出</exception>
    private static async Task HandleNotifyAsync(DeviceContext context, HttpContext http, IServiceProvider sp, CommandSession session, String spanName, CancellationToken cancellationToken)
    {
        var device = context.Device ?? throw new ApiException(ApiCode.Unauthorized, L.T("Not logged in"));

        var deviceService = sp.GetRequiredService<IDeviceService>();
        var tracer = sp.GetService<ITracer>();

        using var span = tracer?.NewSpan(spanName, device.Code);
        span?.Detach(http.Request.Headers);

        // 把 HttpContext 藏到 DeviceContext.Items 中，供 CommandSession.WaitAsync 使用
        context["HttpContext"] = http;
        context["Span"] = span;
        try
        {
            // 统一配置会话公共属性
            session.Code = device.Code;
            session.SetOnline = online => deviceService.SetOnline(context, online);
            session.ServiceProvider = sp;
            session.Tracer = tracer;
            session.Log = new DeviceLogProvider(deviceService, context);

            sp.GetRequiredService<ISessionManager>().Add(session);

            // 下发积压命令
            if (deviceService is IDeviceService2 ds2)
            {
                var commands = ds2.AcquireCommands(context);
                if (commands != null)
                {
                    foreach (var cmd in commands)
                        await session.HandleAsync(cmd, null, cancellationToken).ConfigureAwait(false);
                }
            }

            await session.WaitAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // 这里不一定命中，因 WaitAsync 内部会提前结束span
            span?.SetError(ex, null);
            throw;
        }
    }

    /// <summary>端点执行管线。借出设备上下文、验证令牌、执行端点并统一封装响应</summary>
    /// <remarks>
    /// 内置模型按运行时类型读写（接口模型的实现类型由 DI 决定），FastJson 需要可反射的构造与公开成员；
    /// NativeAOT 下这些元数据必须显式保留，故用 <see cref="DynamicDependencyAttribute"/> 挂在本方法上。
    /// 应用自定义模型需要自行保留（<c>[DynamicDependency]</c> 或 <c>[JsonSerializable]</c> 标记）。
    /// </remarks>
    /// <param name="http">HTTP 上下文</param>
    /// <param name="allowAnonymous">是否允许匿名访问</param>
    /// <param name="handler">端点业务</param>
    /// <param name="writeResult">是否写 code/data 响应信封。长连接端点由会话自身写响应，无需封装</param>
    /// <returns></returns>
    [DynamicDependency(_ModelMembers, typeof(LoginRequest))]
    [DynamicDependency(_ModelMembers, typeof(LoginResponse))]
    [DynamicDependency(_ModelMembers, typeof(LogoutResponse))]
    [DynamicDependency(_ModelMembers, typeof(PingRequest))]
    [DynamicDependency(_ModelMembers, typeof(PingResponse))]
    [DynamicDependency(_ModelMembers, typeof(CommandModel))]
    [DynamicDependency(_ModelMembers, typeof(CommandInModel))]
    [DynamicDependency(_ModelMembers, typeof(CommandReplyModel))]
    [DynamicDependency(_ModelMembers, typeof(EventModel))]
    [DynamicDependency(_ModelMembers, typeof(UpgradeInfo))]
    private static async Task InvokeAsync(HttpContext http, Boolean allowAnonymous, Func<DeviceContext, IServiceProvider, Task<Object?>> handler, Boolean writeResult = true)
    {
        using var culture = http.Features.Get<IRequestCultureFeature>() == null
            ? RequestCultureScope.Create(http.Request.Headers.AcceptLanguage.ToString())
            : null;
        var ctx = DeviceContextPool.Rent();
        try
        {
            ctx.UserHost = http.GetUserHost();
            http.Items[nameof(DeviceContext)] = ctx;

            var provider = http.RequestServices;
            var tokenService = provider.GetRequiredService<ITokenService>();
            var deviceService = provider.GetService<IDeviceService>();

            try
            {
                ctx.Token = ApiFilterAttribute.GetToken(http);

                if (!allowAnonymous)
                {
                    var rs = Authenticate(ctx.Token, ctx, tokenService, deviceService);
                    if (!rs) throw new ApiException(ApiCode.Unauthorized, L.T("Authentication failed"));

                    // 将身份映射为 ClaimsPrincipal，便于端点内使用 User 或授权中间件
                    http.User = BuildPrincipal(ctx);
                }

                if (deviceService == null) throw new InvalidOperationException(L.T("Device service [{0}] is not registered", nameof(IDeviceService)));

                var value = await handler(ctx, provider).ConfigureAwait(false);

                if (writeResult) await WriteResultAsync(http, ApiCode.Ok, value, null).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                deviceService?.WriteHistory(ctx, http.Request.Path + "", false, ex.GetTrue() + "");

                // 长连接端点已写出响应头时不再封装错误，避免破坏已建立的 WebSocket/SSE 流
                if (!http.Response.HasStarted)
                    await WriteResultAsync(http, ex is ApiException aex ? aex.Code : ApiCode.InternalServerError, null, ex.GetTrue().Message).ConfigureAwait(false);
            }
        }
        finally
        {
            http.Items.Remove(nameof(DeviceContext));
            DeviceContextPool.Return(ctx);
        }
    }

    /// <summary>写统一响应信封。成功 {code:0,data:...}，失败 {code:...,message:...}</summary>
    /// <param name="http">HTTP 上下文</param>
    /// <param name="code">状态码</param>
    /// <param name="data">数据体</param>
    /// <param name="message">错误消息</param>
    /// <returns></returns>
    private static async Task WriteResultAsync(HttpContext http, Int32 code, Object? data, String? message)
    {
        var dic = new Dictionary<String, Object?>(4) { ["code"] = code };
        if (message != null)
        {
            dic["message"] = message;

            var traceId = DefaultSpan.Current?.TraceId;
            if (!traceId.IsNullOrEmpty()) dic["traceId"] = traceId;
        }
        else
            dic["data"] = data;

        var json = JsonHelper.ToJson(dic, JsonHelper.Default.Options);
        http.Response.ContentType = "application/json; charset=utf-8";
        await http.Response.WriteAsync(json, http.RequestAborted).ConfigureAwait(false);
    }
    #endregion

    #region 模型绑定
    /// <summary>读取请求体并反序列化为接口在 DI 中的实现类型</summary>
    /// <remarks>
    /// 与 MVC 的 <see cref="ModelBinders.InterfaceModelBinder"/> 语义一致：接口的具体实现类型由 DI 决定，
    /// 自定义扩展模型（如 IoTZero 的 LoginInfo）的额外字段一并生效。
    /// 内置模型已由 NewLife.Core 的 [JsonSerializable] 标记保留成员访问器，AOT 下走成员访问器表；
    /// 应用自定义模型若要在 AOT 下使用，同样需要该标记。
    /// </remarks>
    /// <typeparam name="T">模型类型，可以是接口</typeparam>
    /// <param name="http">HTTP 上下文</param>
    /// <returns>反序列化的模型，请求体为空时返回 null</returns>
    private static async Task<T?> ReadModelAsync<T>(HttpContext http) where T : class
    {
        var json = await ReadBodyAsync(http).ConfigureAwait(false);
        if (json.IsNullOrEmpty()) return null;

        // 由 DI 决定接口的实现类型，未注册时回退接口自身
        var implType = http.RequestServices.GetService<T>()?.GetType() ?? typeof(T);

        return ReadJson<T>(json, implType);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "实现类型来自应用自身的 DI 注册（静态可达），内置模型已由 Core 的 [JsonSerializable] 标记；自定义模型需应用自行标记")]
    private static T? ReadJson<T>(String json, Type implType) where T : class => (T?)JsonHelper.ToJsonEntity(json, implType, JsonHelper.Default.Options);

    /// <summary>读取请求体文本</summary>
    /// <param name="http">HTTP 上下文</param>
    /// <returns></returns>
    private static async Task<String> ReadBodyAsync(HttpContext http)
    {
        using var reader = new StreamReader(http.Request.Body, Encoding.UTF8, true, -1, true);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }
    #endregion

    /// <summary>设备会话日志提供者。把会话日志写入设备历史</summary>
    /// <param name="deviceService">设备服务</param>
    /// <param name="context">设备上下文</param>
    private sealed class DeviceLogProvider(IDeviceService deviceService, DeviceContext context) : ILogProvider
    {
        /// <summary>写日志</summary>
        /// <param name="action">动作名称</param>
        /// <param name="success">是否成功</param>
        /// <param name="message">消息内容</param>
        public void WriteLog(String action, Boolean success, String message) => deviceService.WriteHistory(context, action, success, message);
    }
}