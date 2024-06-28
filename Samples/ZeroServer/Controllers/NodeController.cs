﻿using Microsoft.AspNetCore.Mvc;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Extensions;
using NewLife.Remoting.Models;
using Zero.Data.Nodes;

namespace ZeroServer.Controllers;

/// <summary>设备控制器</summary>
[ApiFilter]
[ApiController]
[Route("[controller]")]
public class NodeController : BaseDeviceController
{
    /// <summary>当前设备</summary>
    public Node Node { get; set; }

    private readonly ITracer _tracer;

    #region 构造
    /// <summary>实例化设备控制器</summary>
    /// <param name="serviceProvider"></param>
    /// <param name="queue"></param>
    /// <param name="deviceService"></param>
    /// <param name="thingService"></param>
    /// <param name="tracer"></param>
    public NodeController(IServiceProvider serviceProvider, ITracer tracer) : base(serviceProvider)
    {
        _tracer = tracer;
    }

    protected override Boolean OnAuthorize(String token)
    {
        if (!base.OnAuthorize(token)) return false;

        Node = _device as Node;

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

        var device = Node;
        if (device != null && rs != null)
            rs.Period = device.Period;

        return rs;
    }
    #endregion
}