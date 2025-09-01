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
    IDeviceModel? QueryDevice(String code);

    /// <summary>设备登录</summary>
    /// <param name="context">上下文</param>
    /// <param name="request">登录请求</param>
    /// <param name="source">来源，如Http/Mqtt</param>
    /// <returns>返回响应</returns>
    ILoginResponse Login(DeviceContext context, ILoginRequest request, String source);

    /// <summary>设备注销</summary>
    /// <param name="context">上下文</param>
    /// <param name="reason">注销原因</param>
    /// <param name="source">登录来源</param>
    /// <returns></returns>
    IOnlineModel? Logout(DeviceContext context, String? reason, String source);

    /// <summary>设备心跳。更新在线记录信息</summary>
    /// <param name="context">上下文</param>
    /// <param name="request">心跳请求</param>
    /// <returns></returns>
    IOnlineModel Ping(DeviceContext context, IPingRequest? request);

    /// <summary>设置设备的长连接上线/下线</summary>
    /// <param name="context">上下文</param>
    /// <param name="online">上线/下线</param>
    /// <returns></returns>
    void SetOnline(DeviceContext context, Boolean online);

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
    IUpgradeInfo? Upgrade(DeviceContext context, String? channel);

    /// <summary>写设备历史</summary>
    /// <param name="context">上下文</param>
    /// <param name="action">动作</param>
    /// <param name="success">成功</param>
    /// <param name="remark">备注内容</param>
    void WriteHistory(DeviceContext context, String action, Boolean success, String remark);
}

/// <summary>设备会话服务（扩展）</summary>
public interface IDeviceService2 : IDeviceService
{
    /// <summary>验证设备合法性。验证密钥</summary>
    /// <param name="context">上下文</param>
    /// <param name="request">登录请求</param>
    /// <returns></returns>
    Boolean Authorize(DeviceContext context, ILoginRequest request);

    /// <summary>自动注册设备。验证密钥失败后</summary>
    /// <param name="context">上下文</param>
    /// <param name="request">登录请求</param>
    /// <returns></returns>
    /// <exception cref="ApiException"></exception>
    IDeviceModel Register(DeviceContext context, ILoginRequest request);

    /// <summary>鉴权后的登录处理。修改设备信息、创建在线记录和写日志</summary>
    /// <param name="context">上下文</param>
    /// <param name="request">登录请求</param>
    void OnLogin(DeviceContext context, ILoginRequest request);

    /// <summary>获取设备。先查缓存再查库</summary>
    /// <param name="code">设备编码</param>
    /// <returns></returns>
    IDeviceModel? GetDevice(String code);

    /// <summary>查找在线</summary>
    IOnlineModel? QueryOnline(String sessionId);

    /// <summary>获取在线。先查缓存再查库</summary>
    /// <param name="context">上下文</param>
    /// <returns></returns>
    IOnlineModel? GetOnline(DeviceContext context);

    /// <summary>创建在线。先写数据库再写缓存</summary>
    /// <param name="context">上下文</param>
    /// <returns></returns>
    IOnlineModel CreateOnline(DeviceContext context);

    /// <summary>删除在线。先删数据库再删缓存</summary>
    /// <param name="context">上下文</param>
    /// <returns></returns>
    Int32 RemoveOnline(DeviceContext context);

    /// <summary>获取下行命令</summary>
    /// <param name="nodeId"></param>
    /// <returns></returns>
    CommandModel[] AcquireCommands(Int32 nodeId);
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