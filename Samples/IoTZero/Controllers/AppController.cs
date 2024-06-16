﻿using IoT.Data;
using IoTZero.Services;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.IoT.Models;
using NewLife.IoT.ThingModels;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Extensions;

namespace IoTZero.Controllers;

/// <summary>物模型Api控制器。用于应用系统调用</summary>
[ApiFilter]
[ApiController]
[Route("[controller]")]
public class AppController : BaseController
{
    private readonly ThingService _thingService;
    private readonly ITracer _tracer;

    #region 构造
    /// <summary>
    /// 实例化应用管理服务
    /// </summary>
    /// <param name="queue"></param>
    /// <param name="deviceService"></param>
    /// <param name="thingService"></param>
    /// <param name="tracer"></param>
    public AppController(IServiceProvider serviceProvider, ThingService thingService, ITracer tracer) : base(serviceProvider)
    {
        _thingService = thingService;
        _tracer = tracer;
    }
    #endregion

    #region 物模型
    /// <summary>获取设备属性</summary>
    /// <param name="deviceId">设备编号</param>
    /// <param name="deviceCode">设备编码</param>
    /// <returns></returns>
    [HttpGet(nameof(GetProperty))]
    public PropertyModel[] GetProperty(Int32 deviceId, String deviceCode)
    {
        var dv = Device.FindById(deviceId) ?? Device.FindByCode(deviceCode);
        if (dv == null) return null;

        return _thingService.QueryProperty(dv, null);
    }

    /// <summary>设置设备属性</summary>
    /// <param name="model">数据</param>
    /// <returns></returns>
    [HttpPost(nameof(SetProperty))]
    public Task<ServiceReplyModel> SetProperty(DevicePropertyModel model)
    {
        var dv = Device.FindByCode(model.DeviceCode);
        if (dv == null) return null;

        throw new NotImplementedException();
    }

    /// <summary>调用设备服务</summary>
    /// <param name="service">服务</param>
    /// <returns></returns>
    [HttpPost(nameof(InvokeService))]
    public async Task<ServiceReplyModel> InvokeService(ServiceRequest service)
    {
        Device dv = null;
        if (service.DeviceId > 0) dv = Device.FindById(service.DeviceId);
        if (dv == null)
        {
            if (!service.DeviceCode.IsNullOrWhiteSpace())
                dv = Device.FindByCode(service.DeviceCode);
            else
                throw new ArgumentNullException(nameof(service.DeviceCode));
        }

        if (dv == null) throw new ArgumentException($"找不到该设备：DeviceId={service.DeviceId}，DeviceCode={service.DeviceCode}");

        return await _thingService.InvokeServiceAsync(dv, service.ServiceName, service.InputData, service.Expire, service.Timeout);
    }
    #endregion
}