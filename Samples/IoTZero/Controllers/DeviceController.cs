using IoT.Data;
using IoTZero.Services;
using Microsoft.AspNetCore.Mvc;
using NewLife.IoT.Drivers;
using NewLife.IoT.Models;
using NewLife.IoT.ThingModels;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Extensions;
using NewLife.Remoting.Models;

namespace IoTZero.Controllers;

/// <summary>设备控制器</summary>
[ApiFilter]
[ApiController]
[Route("[controller]")]
public class DeviceController : BaseDeviceController
{
    /// <summary>当前设备</summary>
    public Device Device { get; set; }

    private readonly QueueService _queue;
    private readonly ThingService _thingService;
    private readonly ITracer _tracer;

    #region 构造
    /// <summary>实例化设备控制器</summary>
    /// <param name="serviceProvider"></param>
    /// <param name="queue"></param>
    /// <param name="deviceService"></param>
    /// <param name="thingService"></param>
    /// <param name="tracer"></param>
    public DeviceController(IServiceProvider serviceProvider, QueueService queue, ThingService thingService, ITracer tracer) : base(serviceProvider)
    {
        _queue = queue;
        _thingService = thingService;
        _tracer = tracer;
    }

    protected override Boolean OnAuthorize(String token)
    {
        if (!base.OnAuthorize(token)) return false;

        Device = _device as Device;

        return true;
    }
    #endregion

    #region 心跳
    /// <summary>设备心跳</summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpPost(nameof(Ping))]
    public override IPingResponse Ping([FromBody] IPingRequest request)
    {
        var rs = base.Ping(request);

        var device = Device;
        if (device != null && rs != null)
        {
            rs.Period = device.Period;
        }

        return rs;
    }
    #endregion

    #region 升级
    /// <summary>升级检查</summary>
    /// <returns></returns>
    [HttpGet(nameof(Upgrade))]
    public override IUpgradeInfo Upgrade()
    {
        var device = Device ?? throw new ApiException(ApiCode.Unauthorized, "节点未登录");

        //throw new NotImplementedException();
        return new UpgradeInfo { };
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
}