using IoT.Data;
using IoTZero.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife.Http;
using NewLife.IoT.Drivers;
using NewLife.IoT.Models;
using NewLife.IoT.ThingModels;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Extensions;
using WebSocket = System.Net.WebSockets.WebSocket;

namespace IoTZero.Controllers;

/// <summary>设备控制器</summary>
[ApiFilter]
[ApiController]
[Route("[controller]")]
public class DeviceController : BaseController
{
    /// <summary>当前设备</summary>
    public Device Device { get; set; }

    private readonly QueueService _queue;
    private readonly MyDeviceService _deviceService;
    private readonly ThingService _thingService;
    private readonly ITracer _tracer;

    #region 构造
    /// <summary>实例化设备控制器</summary>
    /// <param name="serviceProvider"></param>
    /// <param name="queue"></param>
    /// <param name="deviceService"></param>
    /// <param name="thingService"></param>
    /// <param name="tracer"></param>
    public DeviceController(IServiceProvider serviceProvider, QueueService queue, MyDeviceService deviceService, ThingService thingService, ITracer tracer) : base(serviceProvider)
    {
        _queue = queue;
        _deviceService = deviceService;
        _thingService = thingService;
        _tracer = tracer;
    }

    protected override Boolean OnAuthorize(String token)
    {
        if (!base.OnAuthorize(token) || Jwt == null) return false;

        var dv = Device.FindByCode(Jwt.Subject);
        if (dv == null || !dv.Enable) throw new ApiException(ApiCode.Forbidden, "无效设备！");

        Device = dv;

        return true;
    }
    #endregion

    #region 登录
    /// <summary>设备登录</summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpPost(nameof(Login))]
    public LoginResponse Login(LoginInfo model) => _deviceService.Login(model, "Http", UserHost);

    /// <summary>设备注销</summary>
    /// <param name="reason">注销原因</param>
    /// <returns></returns>
    [HttpGet(nameof(Logout))]
    public LogoutResponse Logout(String reason)
    {
        var device = Device;
        if (device != null) _deviceService.Logout(device, reason, "Http", UserHost);

        return new LogoutResponse
        {
            Name = device?.Name,
            Token = null,
        };
    }
    #endregion

    #region 心跳
    /// <summary>设备心跳</summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPost(nameof(Ping))]
    public PingResponse Ping(PingInfo model)
    {
        var rs = new PingResponse
        {
            Time = model.Time,
            ServerTime = DateTime.UtcNow.ToLong(),
        };

        var device = Device;
        if (device != null)
        {
            rs.Period = device.Period;

            var olt = _deviceService.Ping(device, model, Token, UserHost);

            // 令牌有效期检查，10分钟内到期的令牌，颁发新令牌。
            // 这里将来由客户端提交刷新令牌，才能颁发新的访问令牌。
            var tm = _deviceService.ValidAndIssueToken(device.Code, Token);
            if (tm != null)
            {
                rs.Token = tm.AccessToken;

                //_deviceService.WriteHistory(device, "刷新令牌", true, tm.ToJson(), UserHost);
            }
        }

        return rs;
    }

    [HttpGet(nameof(Ping))]
    public PingResponse Ping() => new() { Time = 0, ServerTime = DateTime.UtcNow.ToLong(), };
    #endregion

    #region 升级
    /// <summary>升级检查</summary>
    /// <returns></returns>
    [HttpGet(nameof(Upgrade))]
    public UpgradeInfo Upgrade()
    {
        var device = Device ?? throw new ApiException(402, "节点未登录");

        throw new NotImplementedException();
    }
    #endregion

    #region 设备通道
    /// <summary>获取设备信息，包括主设备和子设备</summary>
    /// <returns></returns>
    [HttpGet(nameof(GetDevices))]
    public DeviceModel[] GetDevices() => throw new NotImplementedException();

    /// <summary>设备上线。驱动打开后调用，子设备发现，或者上报主设备/子设备的默认参数模版</summary>
    /// <remarks>
    /// 有些设备驱动具备扫描发现子设备能力，通过该方法上报设备。
    /// 主设备或子设备，也可通过该方法上报驱动的默认参数模版。
    /// 根据需要，驱动内可能多次调用该方法。
    /// </remarks>
    /// <param name="devices">设备信息集合。可传递参数模版</param>
    /// <returns>返回上报信息对应的反馈，如果新增子设备，则返回子设备信息</returns>
    [HttpPost(nameof(SetOnline))]
    public IDeviceInfo[] SetOnline(DeviceModel[] devices) => throw new NotImplementedException();

    /// <summary>设备下线。驱动内子设备变化后调用</summary>
    /// <remarks>
    /// 根据需要，驱动内可能多次调用该方法。
    /// </remarks>
    /// <param name="devices">设备编码集合。用于子设备离线</param>
    /// <returns>返回上报信息对应的反馈，如果新增子设备，则返回子设备信息</returns>
    [HttpPost(nameof(SetOffline))]
    public IDeviceInfo[] SetOffline(String[] devices) => throw new NotImplementedException();

    /// <summary>获取设备点位表</summary>
    /// <param name="deviceCode">设备编码</param>
    /// <returns></returns>
    [HttpGet(nameof(GetPoints))]
    public PointModel[] GetPoints(String deviceCode) => throw new NotImplementedException();

    /// <summary>提交驱动信息。客户端把自己的驱动信息提交到平台</summary>
    /// <param name="drivers"></param>
    /// <returns></returns>
    [HttpPost(nameof(PostDriver))]
    public Int32 PostDriver(DriverInfo[] drivers) => throw new NotImplementedException();
    #endregion

    #region 下行通知
    /// <summary>下行通知</summary>
    /// <returns></returns>
    [HttpGet("/Device/Notify")]
    public async Task Notify()
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            using var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();

            await Handle(socket, Token);
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
        }
    }

    private async Task Handle(WebSocket socket, String token)
    {
        var device = Device ?? throw new InvalidOperationException("未登录！");

        _deviceService.WriteHistory(device, "WebSocket连接", true, socket.State + "", UserHost);

        var source = new CancellationTokenSource();
        var queue = _queue.GetQueue(device.Code);
        _ = Task.Run(() => socket.ConsumeAndPushAsync(queue, onProcess: null, source));
        await socket.WaitForClose(null, source);
    }
    #endregion
}