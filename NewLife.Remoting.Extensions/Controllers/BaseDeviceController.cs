using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife.Http;
using NewLife.Log;
using NewLife.Remoting.Extensions.Services;
using NewLife.Remoting.Models;
using WebSocket = System.Net.WebSockets.WebSocket;

namespace NewLife.Remoting.Extensions;

/// <summary>设备类控制器基类</summary>
[ApiFilter]
[ApiController]
[Route("[controller]")]
public class BaseDeviceController : BaseController
{
    /// <summary>设备</summary>
    protected IDeviceModel _device = null!;

    private readonly IDeviceService _deviceService;
    private readonly TokenService _tokenService;
    private readonly ITracer? _tracer;

    #region 构造
    /// <summary>实例化设备控制器</summary>
    /// <param name="serviceProvider"></param>
    public BaseDeviceController(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _deviceService = serviceProvider.GetRequiredService<IDeviceService>();
        _tokenService = serviceProvider.GetRequiredService<TokenService>();
        _tracer = serviceProvider.GetService<ITracer>();
    }

    /// <summary>验证身份</summary>
    /// <param name="token"></param>
    /// <returns></returns>
    /// <exception cref="ApiException"></exception>
    protected override Boolean OnAuthorize(String token)
    {
        if (!base.OnAuthorize(token) || Jwt == null || Jwt.Subject.IsNullOrEmpty()) return false;

        var dv = _deviceService.QueryDevice(Jwt.Subject);
        if (dv == null || !dv.Enable) throw new ApiException(ApiCode.Forbidden, "无效设备！");

        _device = dv;

        return true;
    }
    #endregion

    #region 登录
    /// <summary>设备登录</summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpPost(nameof(Login))]
    public virtual ILoginResponse Login([FromServices][FromBody] ILoginRequest request)
    {
        // 先查一次，后续即使登录失败，也可以写设备历史
        _device = _deviceService.QueryDevice(request.Code);

        var (dv, online, rs) = _deviceService.Login(request, "Http", UserHost);

        rs ??= new LoginResponse { Name = dv.Name, };

        rs.Code = dv.Code;
        rs.Time = DateTime.UtcNow.ToLong();

        // 动态注册的设备不可用时，不要发令牌，只发证书
        if (dv.Enable)
        {
            var tm = _tokenService.IssueToken(dv.Code, request.ClientId);

            rs.Token = tm.AccessToken;
        }

        return rs;
    }

    /// <summary>设备注销</summary>
    /// <param name="reason">注销原因</param>
    /// <returns></returns>
    [HttpGet(nameof(Logout))]
    public virtual ILogoutResponse Logout(String reason)
    {
        var olt = _deviceService.Logout(_device, reason, "Http", UserHost);

        return new LogoutResponse
        {
            Name = _device?.Name,
            Token = null,
        };
    }
    #endregion

    #region 心跳
    /// <summary>设备心跳</summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpPost(nameof(Ping))]
    public virtual IPingResponse Ping(IPingRequest request) => OnPing(request);

    /// <summary>设备心跳</summary>
    /// <returns></returns>
    [HttpGet(nameof(Ping))]
    public virtual IPingResponse Ping() => OnPing(null);

    /// <summary>设备心跳</summary>
    /// <param name="request"></param>
    /// <returns></returns>
    protected IPingResponse OnPing(IPingRequest? request)
    {
        var rs = new PingResponse
        {
            Time = 0,
            ServerTime = DateTime.UtcNow.ToLong(),
        };

        if (request != null) rs.Time = request.Time;

        var device = _device;
        if (device != null)
        {
            //rs.Period = device.Period;

            _deviceService.Ping(device, request, Token, UserHost);

            // 令牌有效期检查，10分钟内到期的令牌，颁发新令牌。
            // 这里将来由客户端提交刷新令牌，才能颁发新的访问令牌。
            var tm = _deviceService.ValidAndIssueToken(device.Code, Token);
            if (tm != null)
                rs.Token = tm.AccessToken;
        }

        return rs;
    }
    #endregion

    #region 升级
    /// <summary>升级检查</summary>
    /// <returns></returns>
    [HttpGet(nameof(Upgrade))]
    public virtual IUpgradeInfo Upgrade()
    {
        var device = _device ?? throw new ApiException(ApiCode.Unauthorized, "节点未登录");

        //throw new NotImplementedException();
        return new UpgradeInfo { };
    }
    #endregion

    #region 下行通知
    /// <summary>下行通知</summary>
    /// <returns></returns>
    [HttpGet(nameof(Notify))]
    public virtual async Task Notify()
    {
        if (Token.IsNullOrEmpty())
        {
            HttpContext.Response.StatusCode = (Int32)HttpStatusCode.Unauthorized;
            return;
        }

        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            using var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();

            await Handle(socket, Token);
        }
        else
            HttpContext.Response.StatusCode = 400;
    }

    private async Task Handle(WebSocket socket, String token)
    {
        var device = _device ?? throw new InvalidOperationException("未登录！");

        _deviceService.WriteHistory(device, "WebSocket连接", true, socket.State + "", UserHost);

        var source = new CancellationTokenSource();
        var queue = _deviceService.GetQueue(device.Code);
        _ = Task.Run(() => socket.ConsumeAndPushAsync(queue, onProcess: null, source));
        await socket.WaitForClose(null, source);
    }
    #endregion

    #region 辅助
    /// <summary>写日志</summary>
    /// <param name="action"></param>
    /// <param name="success"></param>
    /// <param name="message"></param>
    protected override void WriteLog(String action, Boolean success, String message) => _deviceService.WriteHistory(_device, action, success, message, UserHost);
    #endregion
}