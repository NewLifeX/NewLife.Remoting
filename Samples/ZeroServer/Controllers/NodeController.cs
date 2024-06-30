using System.Net.WebSockets;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Extensions;
using NewLife.Remoting.Extensions.Services;
using NewLife.Remoting.Models;
using Zero.Data.Nodes;
using ZeroServer.Services;

namespace ZeroServer.Controllers;

/// <summary>设备控制器</summary>
[ApiFilter]
[ApiController]
[Route("[controller]")]
public class NodeController : BaseDeviceController
{
    /// <summary>当前设备</summary>
    public Node? Node { get; set; }

    private readonly NodeService _nodeService;
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
        _nodeService = serviceProvider.GetRequiredService<IDeviceService>() as NodeService;
        _tracer = tracer;
    }

    protected override Boolean OnAuthorize(String token)
    {
        if (!base.OnAuthorize(token)) return false;

        Node = _device as Node;

        return true;
    }
    #endregion

    /// <summary>心跳</summary>
    /// <param name="request"></param>
    /// <returns></returns>
    protected override IPingResponse OnPing(IPingRequest? request)
    {
        var rs = base.OnPing(request);

        var device = Node;
        if (device != null)
            rs.Period = device.Period;

        return rs;
    }

    /// <summary>长连接处理</summary>
    /// <param name="socket"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    protected override async Task HandleNotify(WebSocket socket, String token)
    {
        NodeOnline online = null;
        var node = Node;
        if (node != null)
        {
            // 上线打标记
            online = _nodeService.GetOnline(node, UserHost);
            if (online != null)
            {
                online.WebSocket = true;
                online.Update();
            }
        }

        try
        {
            await base.HandleNotify(socket, token);
        }
        finally
        {
            // 下线清除标记
            if (online != null)
            {
                online.WebSocket = false;
                online.Update();
            }
        }
    }
}