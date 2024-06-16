using NewLife.Caching;
using NewLife.Remoting.Models;

namespace NewLife.Remoting.Extensions.Services;

/// <summary>设备会话服务</summary>
/// <remarks>
/// 星尘的Node节点；IoT的Device设备；
/// </remarks>
public interface IDeviceService
{
    /// <summary>查找设备</summary>
    /// <param name="code"></param>
    /// <returns></returns>
    IDeviceModel QueryDevice(String code);

    /// <summary>设备登录</summary>
    /// <param name="request"></param>
    /// <param name="source"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    LoginResponse Login(ILoginRequest request, String source, String ip);

    /// <summary>设备注销</summary>
    /// <param name="device"></param>
    /// <param name="reason"></param>
    /// <param name="source"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    IOnlineModel Logout(IDeviceModel device, String reason, String source, String ip);

    /// <summary>设备心跳</summary>
    /// <param name="device"></param>
    /// <param name="request"></param>
    /// <param name="token"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    IOnlineModel Ping(IDeviceModel device, IPingRequest? request, String? token, String ip);

    /// <summary>验证并重新颁发令牌</summary>
    /// <param name="deviceCode"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    TokenModel ValidAndIssueToken(String deviceCode, String? token);

    /// <summary>获取指定设备的命令队列</summary>
    /// <param name="deviceCode"></param>
    /// <returns></returns>
    IProducerConsumer<String> GetQueue(String deviceCode);

    /// <summary>写入历史</summary>
    /// <param name="device"></param>
    /// <param name="action"></param>
    /// <param name="success"></param>
    /// <param name="remark"></param>
    /// <param name="ip"></param>
    void WriteHistory(IDeviceModel device, String action, Boolean success, String remark, String ip);
}