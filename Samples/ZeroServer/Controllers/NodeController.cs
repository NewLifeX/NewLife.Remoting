using Microsoft.AspNetCore.Mvc;
using NewLife.Remoting.Extensions;
using NewLife.Remoting.Models;
using Zero.Data.Nodes;

namespace ZeroServer.Controllers;

/// <summary>设备控制器</summary>
/// <param name="serviceProvider"></param>
[ApiFilter]
[ApiController]
[Route("[controller]")]
public class NodeController(IServiceProvider serviceProvider) : BaseDeviceController(serviceProvider)
{
    /// <summary>心跳</summary>
    /// <param name="request"></param>
    /// <returns></returns>
    protected override IPingResponse OnPing(IPingRequest request)
    {
        var rs = base.OnPing(request);

        if (Context.Device is Node node && rs != null)
        {
            rs.Period = node.Period;

            if (rs is PingResponse rs2)
                rs2.NewServer = node.NewServer;
        }

        return rs;
    }
}