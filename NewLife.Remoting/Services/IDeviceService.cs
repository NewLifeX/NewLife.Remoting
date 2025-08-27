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
    /// <param name="request">登录请求参数</param>
    /// <param name="source">来源，如Http/Mqtt</param>
    /// <param name="ip">来源IP</param>
    /// <returns>返回元组：设备、在线、响应</returns>
    (IDeviceModel, IOnlineModel, ILoginResponse) Login(ILoginRequest request, String source, String ip);

    /// <summary>设备注销</summary>
    /// <param name="device">设备</param>
    /// <param name="reason">注销原因</param>
    /// <param name="source">登录来源</param>
    /// <param name="clientId">客户端标识</param>
    /// <param name="ip">远程IP</param>
    /// <returns></returns>
    IOnlineModel Logout(IDeviceModel device, String? reason, String source, String clientId, String ip);

    /// <summary>设备心跳</summary>
    /// <param name="device">设备</param>
    /// <param name="request">心跳请求</param>
    /// <param name="token">令牌</param>
    /// <param name="clientId">客户端标识</param>
    /// <param name="ip">远程IP</param>
    /// <returns></returns>
    IOnlineModel Ping(IDeviceModel device, IPingRequest? request, String token, String clientId, String ip);

    /// <summary>设置设备的长连接上线/下线</summary>
    /// <param name="device">设备</param>
    /// <param name="online">上线/下线</param>
    /// <param name="token">令牌</param>
    /// <param name="clientId">客户端标识</param>
    /// <param name="ip">远程IP</param>
    /// <returns></returns>
    IOnlineModel SetOnline(IDeviceModel device, Boolean online, String token, String clientId, String ip);

    /// <summary>发送命令</summary>
    /// <param name="device">设备</param>
    /// <param name="command">命令</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<Int32> SendCommand(IDeviceModel device, CommandModel command, CancellationToken cancellationToken = default);

    /// <summary>命令响应</summary>
    /// <param name="device">设备</param>
    /// <param name="model">响应模型</param>
    /// <param name="ip">远程IP</param>
    /// <returns></returns>
    Int32 CommandReply(IDeviceModel device, CommandReplyModel model, String ip);

    /// <summary>上报事件</summary>
    /// <param name="device">设备</param>
    /// <param name="events">事件集合</param>
    /// <param name="ip">远程IP</param>
    /// <returns></returns>
    Int32 PostEvents(IDeviceModel device, EventModel[] events, String ip);

    /// <summary>升级检查</summary>
    /// <param name="device">设备</param>
    /// <param name="channel">更新通道</param>
    /// <param name="ip">远程IP</param>
    /// <returns></returns>
    IUpgradeInfo Upgrade(IDeviceModel device, String? channel, String ip);

    /// <summary>写设备历史</summary>
    /// <param name="device">设备</param>
    /// <param name="action">动作</param>
    /// <param name="success">成功</param>
    /// <param name="remark">备注内容</param>
    /// <param name="clientId">客户端标识</param>
    /// <param name="ip">远程IP</param>
    void WriteHistory(IDeviceModel device, String action, Boolean success, String remark, String clientId, String ip);
}