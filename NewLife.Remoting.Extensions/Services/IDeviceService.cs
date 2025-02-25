using NewLife.Remoting.Models;
using NewLife.Web;

namespace NewLife.Remoting.Extensions.Services;

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
    /// <param name="device"></param>
    /// <param name="reason"></param>
    /// <param name="source"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    IOnlineModel Logout(IDeviceModel device, String? reason, String source, String ip);

    /// <summary>设备心跳</summary>
    /// <param name="device"></param>
    /// <param name="request"></param>
    /// <param name="token"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    IOnlineModel Ping(IDeviceModel device, IPingRequest? request, String? token, String ip);

    /// <summary>设置设备的长连接上线/下线</summary>
    /// <param name="device"></param>
    /// <param name="online"></param>
    /// <param name="token"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    IOnlineModel SetOnline(IDeviceModel device, Boolean online, String token, String ip);

    /// <summary>发送命令</summary>
    /// <param name="device"></param>
    /// <param name="command"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Int32> SendCommand(IDeviceModel device, CommandModel command, CancellationToken cancellationToken = default);

    /// <summary>命令响应</summary>
    /// <param name="device"></param>
    /// <param name="model"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    Int32 CommandReply(IDeviceModel device, CommandReplyModel model, String ip);

    /// <summary>上报事件</summary>
    /// <param name="device"></param>
    /// <param name="events"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    Int32 PostEvents(IDeviceModel device, EventModel[] events, String ip);

    /// <summary>升级检查</summary>
    /// <param name="device"></param>
    /// <param name="channel">更新通道</param>
    /// <param name="ip"></param>
    /// <returns></returns>
    IUpgradeInfo Upgrade(IDeviceModel device, String? channel, String ip);

    /// <summary>验证并重新颁发令牌</summary>
    /// <param name="deviceCode"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    TokenModel ValidAndIssueToken(String deviceCode, String? token);

    /// <summary>写入历史</summary>
    /// <param name="device"></param>
    /// <param name="action"></param>
    /// <param name="success"></param>
    /// <param name="remark"></param>
    /// <param name="ip"></param>
    void WriteHistory(IDeviceModel device, String action, Boolean success, String remark, String ip);
}