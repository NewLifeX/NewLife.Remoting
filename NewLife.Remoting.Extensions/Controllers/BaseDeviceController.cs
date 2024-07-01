using System.Net;
using System.Net.WebSockets;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife.Caching;
using NewLife.Http;
using NewLife.Log;
using NewLife.Remoting.Extensions.Services;
using NewLife.Remoting.Models;
using NewLife.Serialization;
using WebSocket = System.Net.WebSockets.WebSocket;
using WebSocketMessageType = System.Net.WebSockets.WebSocketMessageType;

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
        if (dv == null || !dv.Enable) throw new ApiException(ApiCode.Forbidden, "无效客户端！");

        _device = dv;

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
        if (!request.Code.IsNullOrEmpty()) _device = _deviceService.QueryDevice(request.Code);

        var (dv, online, rs) = _deviceService.Login(request, "Http", UserHost);

        rs ??= new LoginResponse { Name = dv.Name, };

        rs.Code = dv.Code;
        rs.Time = DateTime.UtcNow.ToLong();

        // 动态注册的设备不可用时，不要发令牌，只发证书
        if (dv.Enable)
        {
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
        var olt = _deviceService.Logout(_device, reason, "Http", UserHost);

        return new LogoutResponse
        {
            Name = _device?.Name,
            Token = null,
        };
    }
    #endregion

    #region 心跳保活
    /// <summary>设备心跳</summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpPost(nameof(Ping))]
    public virtual IPingResponse Ping([FromBody] IPingRequest request) => OnPing(request);

    /// <summary>设备心跳</summary>
    /// <returns></returns>
    [HttpGet(nameof(Ping))]
    public virtual IPingResponse Ping() => OnPing(null);

    /// <summary>设备心跳</summary>
    /// <param name="request"></param>
    /// <returns></returns>
    protected virtual IPingResponse OnPing(IPingRequest? request)
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

    #region 升级更新
    /// <summary>升级检查</summary>
    /// <returns></returns>
    [HttpGet(nameof(Upgrade))]
    [HttpPost(nameof(Upgrade))]
    public virtual IUpgradeInfo Upgrade(String? channel)
    {
        var device = _device ?? throw new ApiException(ApiCode.Unauthorized, "未登录");

        // 基础路径
        var uri = Request.GetRawUrl().ToString();
        var p = uri.IndexOf('/', "https://".Length);
        if (p > 0) uri = uri[..p];

        var info = _deviceService.Upgrade(device, channel, UserHost);

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
        if (Token.IsNullOrEmpty())
        {
            HttpContext.Response.StatusCode = (Int32)HttpStatusCode.Unauthorized;
            return;
        }

        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            using var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();

            await HandleNotify(socket, Token);
        }
        else
            HttpContext.Response.StatusCode = 400;
    }

    /// <summary>长连接处理</summary>
    /// <param name="socket"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    protected virtual async Task HandleNotify(WebSocket socket, String token)
    {
        var device = _device ?? throw new InvalidOperationException("未登录！");

        var ip = UserHost;
        WriteLog("WebSocket连接", true, socket.State + "");

        var source = new CancellationTokenSource();
        var queue = _deviceService.GetQueue(device.Code);
        //_ = Task.Run(() => socket.ConsumeAndPushAsync(queue, onProcess: null, source));
        _ = Task.Run(() => ConsumeMessage(socket, device.Code, queue, ip, source));

        await socket.WaitForClose(null, source);
    }

    private async Task ConsumeMessage(WebSocket socket, String code, IProducerConsumer<String> queue, String ip, CancellationTokenSource source)
    {
        DefaultSpan.Current = null;
        var cancellationToken = source.Token;
        try
        {
            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                ISpan? span = null;
                var mqMsg = await queue.TakeOneAsync(15, cancellationToken);
                if (mqMsg != null)
                {
                    // 埋点
                    span = _tracer?.NewSpan($"mq:Command", mqMsg);

                    // 解码
                    var dic = JsonParser.Decode(mqMsg)!;
                    var msg = JsonHelper.Convert<CommandModel>(dic);
                    span?.Detach(dic);

                    if (msg == null || /*msg.Id == 0 ||*/ msg.Expire.Year > 2000 && msg.Expire < DateTime.Now)
                    {
                        WriteLog("WebSocket发送", false, "消息无效或已过期。" + mqMsg);
                    }
                    else
                    {
                        WriteLog("WebSocket发送", true, mqMsg);

                        await socket.SendAsync(mqMsg.GetBytes(), WebSocketMessageType.Text, true, cancellationToken);
                    }

                    span?.Dispose();
                }
                else
                {
                    await Task.Delay(1_000, cancellationToken);
                }
            }
        }
        catch (TaskCanceledException) { }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            XTrace.WriteLine("WebSocket异常 client={0} ip={1}", code, ip);
            XTrace.WriteException(ex);
            WriteLog("WebSocket断开", false, $"State={socket.State} CloseStatus={socket.CloseStatus} {ex}");
        }
        finally
        {
            source.Cancel();
        }
    }

    /// <summary>设备端响应服务调用</summary>
    /// <param name="model">服务</param>
    /// <returns></returns>
    [HttpPost(nameof(CommandReply))]
    public virtual Int32 CommandReply(CommandReplyModel model) => _deviceService.CommandReply(_device, model, UserHost);
    #endregion

    #region 事件上报
    /// <summary>批量上报事件</summary>
    /// <param name="events">事件集合</param>
    /// <returns></returns>
    [HttpPost(nameof(PostEvents))]
    public virtual Int32 PostEvents(EventModel[] events) => _deviceService.PostEvents(_device, events, UserHost);
    #endregion

    #region 辅助
    /// <summary>写日志</summary>
    /// <param name="action"></param>
    /// <param name="success"></param>
    /// <param name="message"></param>
    protected override void WriteLog(String action, Boolean success, String message) => _deviceService.WriteHistory(_device, action, success, message, UserHost);
    #endregion
}