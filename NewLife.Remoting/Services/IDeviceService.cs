using NewLife.Remoting.Models;

namespace NewLife.Remoting.Services;

/// <summary>设备会话服务</summary>
/// <remarks>
/// 星尘的Node节点；IoT的Device设备；
/// </remarks>
public interface IDeviceService
{
    /// <summary>查找设备</summary>
    /// <param name="code">编码</param>
    /// <returns></returns>
    IDeviceModel QueryDevice(String code);

    /// <summary>设备登录</summary>
    /// <param name="context">上下文</param>
    /// <param name="request">登录请求参数</param>
    /// <param name="source">来源，如Http/Mqtt</param>
    /// <returns>返回响应</returns>
    ILoginResponse Login(DeviceContext context, ILoginRequest request, String source);

    /// <summary>设备注销</summary>
    /// <param name="context">上下文</param>
    /// <param name="reason">注销原因</param>
    /// <param name="source">登录来源</param>
    /// <returns></returns>
    IOnlineModel Logout(DeviceContext context, String? reason, String source);

    /// <summary>设备心跳</summary>
    /// <param name="context">上下文</param>
    /// <param name="request">心跳请求</param>
    /// <returns></returns>
    IOnlineModel Ping(DeviceContext context, IPingRequest? request);

    /// <summary>设置设备的长连接上线/下线</summary>
    /// <param name="context">上下文</param>
    /// <param name="online">上线/下线</param>
    /// <returns></returns>
    IOnlineModel SetOnline(DeviceContext context, Boolean online);

    /// <summary>发送命令</summary>
    /// <param name="device">设备</param>
    /// <param name="command">命令</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<Int32> SendCommand(IDeviceModel device, CommandModel command, CancellationToken cancellationToken = default);

    /// <summary>命令响应</summary>
    /// <param name="context">上下文</param>
    /// <param name="model">响应模型</param>
    /// <returns></returns>
    Int32 CommandReply(DeviceContext context, CommandReplyModel model);

    /// <summary>上报事件</summary>
    /// <param name="context">上下文</param>
    /// <param name="events">事件集合</param>
    /// <returns></returns>
    Int32 PostEvents(DeviceContext context, EventModel[] events);

    /// <summary>升级检查</summary>
    /// <param name="context">上下文</param>
    /// <param name="channel">更新通道</param>
    /// <returns></returns>
    IUpgradeInfo Upgrade(DeviceContext context, String? channel);

    /// <summary>写设备历史</summary>
    /// <param name="context">上下文</param>
    /// <param name="action">动作</param>
    /// <param name="success">成功</param>
    /// <param name="remark">备注内容</param>
    void WriteHistory(DeviceContext context, String action, Boolean success, String remark);
}

/// <summary>设备服务扩展</summary>
public static class DeviceServiceExtensions
{
    /// <summary>写设备历史</summary>
    /// <param name="deviceService">设备服务</param>
    /// <param name="device">设备</param>
    /// <param name="action">动作</param>
    /// <param name="success">成功</param>
    /// <param name="remark">备注内容</param>
    /// <param name="clientId">客户端标识</param>
    /// <param name="ip">远程IP</param>
    public static void WriteHistory(this IDeviceService deviceService, IDeviceModel device, String action, Boolean success, String remark, String? clientId, String? ip)
    {
        if (device == null) throw new ArgumentNullException(nameof(device));
        var ctx = new DeviceContext
        {
            Device = device,
            ClientId = clientId,
            UserHost = ip
        };
        deviceService.WriteHistory(ctx, action, success, remark);
    }
}