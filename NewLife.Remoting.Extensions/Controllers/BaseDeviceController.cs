using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;
using NewLife.Remoting.Extensions.Services;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Security;
using WebSocket = System.Net.WebSockets.WebSocket;

namespace NewLife.Remoting.Extensions;

/// <summary>设备类控制器基类</summary>
[ApiFilter]
[ApiController]
[Route("[controller]")]
public abstract class BaseDeviceController : BaseController
{
    private readonly IDeviceService _deviceService;
    private readonly ITokenService _tokenService;
    private readonly ISessionManager _sessionManager;
    private readonly ITracer _tracer;

    #region 构造
    /// <summary>实例化设备控制器</summary>
    /// <param name="serviceProvider"></param>
    public BaseDeviceController(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _deviceService = serviceProvider.GetRequiredService<IDeviceService>();
        _tokenService = serviceProvider.GetRequiredService<ITokenService>();
        _sessionManager = serviceProvider.GetRequiredService<ISessionManager>();
        _tracer = serviceProvider.GetRequiredService<ITracer>();
    }

    /// <summary>实例化设备控制器</summary>
    /// <param name="deviceService"></param>
    /// <param name="tokenService"></param>
    /// <param name="sessionManager"></param>
    /// <param name="serviceProvider"></param>
    public BaseDeviceController(IDeviceService? deviceService, ITokenService? tokenService, ISessionManager? sessionManager, IServiceProvider serviceProvider) : base(deviceService, tokenService, serviceProvider)
    {
        _deviceService = deviceService ?? serviceProvider.GetRequiredService<IDeviceService>();
        _tokenService = tokenService ?? serviceProvider.GetRequiredService<ITokenService>();
        _sessionManager = sessionManager ?? serviceProvider.GetRequiredService<ISessionManager>();
        _tracer = serviceProvider.GetRequiredService<ITracer>();
    }

    /// <summary>验证身份</summary>
    /// <param name="token"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    /// <exception cref="ApiException"></exception>
    protected override Boolean OnAuthorize(String token, DeviceContext context)
    {
        // 先调用基类，获取Jwt。即使失败，也要继续往下走，获取设备信息。最后再决定是否抛出异常
        Exception? error = null;
        try
        {
            if (!base.OnAuthorize(token, context)) return false;
        }
        catch (Exception ex)
        {
            error = ex;
        }

        var ds2 = _deviceService as IDeviceService2;

        if (context.Device == null)
        {
            var code = Jwt?.Subject;
            if (code.IsNullOrEmpty()) return false;

            var dv = ds2 != null ? ds2.GetDevice(code) : _deviceService.QueryDevice(code);
            if (dv == null || !dv.Enable) error ??= new ApiException(ApiCode.Forbidden, "无效客户端！");

            context.Device = dv!;
        }

        // 在线对象
        context.Online ??= ds2?.GetOnline(context);

        if (error != null) throw error;

        return true;
    }
    #endregion

    #region 登录注销
    /// <summary>设备登录</summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpPost(nameof(Login))]
    public virtual ILoginResponse Login([FromBody] ILoginRequest request)
    {
        // 先查一次，后续即使登录失败，也可以写设备历史
        if (!request.Code.IsNullOrEmpty()) Context.Device = _deviceService.QueryDevice(request.Code);

        var rs = _deviceService.Login(Context, request, "Http");

        var dv = Context.Device!;
        rs ??= new LoginResponse { Name = dv.Name, };
        rs.Code = dv.Code;

        if (request is ILoginRequest2 req) rs.Time = req.Time;
        rs.ServerTime = DateTime.UtcNow.ToLong();

        // 动态注册的设备不可用时，不要发令牌，只发证书
        if (dv.Enable)
        {
            if (request.ClientId.IsNullOrEmpty()) Context.ClientId = request.ClientId = Rand.NextString(8);
            var tm = _tokenService.IssueToken(dv.Code, request.ClientId);

            rs.Token = tm.AccessToken;
            rs.Expire = tm.ExpireIn;
        }

        return rs;
    }

    /// <summary>设备注销</summary>
    /// <param name="reason">注销原因</param>
    /// <returns></returns>
    [HttpGet(nameof(Logout))]
    [HttpPost(nameof(Logout))]
    public virtual ILogoutResponse Logout(String? reason)
    {
        _deviceService.Logout(Context, reason, "Http");

        return new LogoutResponse
        {
            Name = Context.Device?.Name,
            Token = null,
        };
    }
    #endregion

    #region 心跳保活
    /// <summary>设备心跳</summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpGet(nameof(Ping))]
    [HttpPost(nameof(Ping))]
    public virtual IPingResponse Ping([FromBody] IPingRequest request)
    {
        var rs = _deviceService.Ping(Context, request);

        var device = Context.Device;
        if (device != null)
        {
            // 令牌有效期检查，10分钟内到期的令牌，颁发新令牌。
            // 这里将来由客户端提交刷新令牌，才能颁发新的访问令牌。
            var (jwt, ex) = _tokenService.DecodeToken(Context.Token!);
            if (ex == null && jwt != null && jwt.Expire < DateTime.Now.AddMinutes(10))
            {
                using var span = _tracer?.NewSpan("RefreshToken", new { device.Code, jwt.Subject });

                var tm = _tokenService.IssueToken(device.Code, jwt.Id);
                rs.Token = tm.AccessToken;
            }
        }

        return rs;
    }
    #endregion

    #region 升级更新
    /// <summary>升级检查</summary>
    /// <returns></returns>
    [HttpGet(nameof(Upgrade))]
    [HttpPost(nameof(Upgrade))]
    public virtual IUpgradeInfo Upgrade(String? channel)
    {
        if (Context.Device == null) throw new ApiException(ApiCode.Unauthorized, "未登录");

        // 基础路径
        var uri = Request.GetRawUrl().ToString();
        var p = uri.IndexOf('/', "https://".Length);
        if (p > 0) uri = uri[..p];

        var info = _deviceService.Upgrade(Context, channel);

        // 为了兼容旧版本客户端，这里必须把路径处理为绝对路径
        if (info != null && !info.Source.StartsWithIgnoreCase("http://", "https://"))
        {
            info.Source = new Uri(new Uri(uri), info.Source) + "";
        }

        return info!;
    }
    #endregion

    #region 下行通知
    /// <summary>下行通知</summary>
    /// <returns></returns>
    [HttpGet(nameof(Notify))]
    public virtual async Task Notify()
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            using var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();

            await HandleNotify(socket, HttpContext.RequestAborted);
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
        }
    }

    /// <summary>长连接处理</summary>
    /// <param name="socket"></param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    protected virtual async Task HandleNotify(WebSocket socket, CancellationToken cancellationToken)
    {
        var device = Context.Device ?? throw new InvalidOperationException("未登录！");
        var sessionManager = _sessionManager ?? throw new InvalidOperationException("未找到SessionManager服务");

        using var session = new WsCommandSession(socket)
        {
            Code = device.Code,
            Log = this,
            SetOnline = online => _deviceService.SetOnline(Context, online)
        };

        sessionManager.Add(session);

        await session.WaitAsync(HttpContext, cancellationToken);
    }

    /// <summary>设备端响应服务调用</summary>
    /// <param name="model">服务</param>
    /// <returns></returns>
    [HttpPost(nameof(CommandReply))]
    public virtual Int32 CommandReply(CommandReplyModel model) => _deviceService.CommandReply(Context, model);

    /// <summary>向节点发送命令。通知节点更新、安装和启停应用等</summary>
    /// <param name="model">命令模型</param>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpPost(nameof(SendCommand))]
    public Task<CommandReplyModel?> SendCommand(CommandInModel model)
    {
        if (model.Code.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.Code), "必须指定编码");
        if (model.Command.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.Command));

        return _deviceService.SendCommand(Context, model, HttpContext.RequestAborted);
    }
    #endregion

    #region 事件上报
    /// <summary>批量上报事件</summary>
    /// <param name="events">事件集合</param>
    /// <returns></returns>
    [HttpPost(nameof(PostEvents))]
    public virtual Int32 PostEvents(EventModel[] events) => _deviceService.PostEvents(Context, events);
    #endregion

    #region 辅助
    /// <summary>写日志</summary>
    /// <param name="action"></param>
    /// <param name="success"></param>
    /// <param name="message"></param>
    public override void WriteLog(String action, Boolean success, String message) => _deviceService.WriteHistory(Context, action, success, message);
    #endregion
}