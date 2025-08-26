using IoT.Data;
using Microsoft.AspNetCore.Mvc;
using NewLife.IoT.Drivers;
using NewLife.IoT.Models;
using NewLife.IoT.ThingModels;
using NewLife.Remoting;
using NewLife.Remoting.Extensions;
using NewLife.Remoting.Models;

namespace IoTZero.Controllers;

/// <summary>设备控制器</summary>
/// <remarks>实例化设备控制器</remarks>
/// <param name="serviceProvider"></param>
[ApiFilter]
[ApiController]
[Route("[controller]")]
public class DeviceController(IServiceProvider serviceProvider) : BaseDeviceController(serviceProvider)
{
    /// <summary>当前设备</summary>
    public Device Device { get; set; }

    #region 构造
    protected override Boolean OnAuthorize(String token, ActionContext context)
    {
        if (!base.OnAuthorize(token, context)) return false;

        Device = _device as Device;

        return true;
    }
    #endregion

    #region 心跳
    /// <summary>心跳</summary>
    /// <param name="request"></param>
    /// <returns></returns>
    protected override IPingResponse OnPing(IPingRequest request)
    {
        var rs = base.OnPing(request);

        var device = Device;
        if (device != null && rs != null)
        {
            rs.Period = device.Period;
        }

        return rs;
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