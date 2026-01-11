using NewLife.Remoting.Models;

namespace NewLife.Remoting.Services;

/// <summary>设备会话服务</summary>
/// <remarks>
/// 星尘的Node节点；IoT的Device设备；
/// 提供设备登录、注销、心跳、命令下发等核心功能。
/// </remarks>
public interface IDeviceService
{
    /// <summary>查找设备</summary>
    /// <param name="code">设备编码</param>
    /// <returns>设备信息，不存在时返回null</returns>
    IDeviceModel? QueryDevice(String code);

    /// <summary>设备登录</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="request">登录请求</param>
    /// <param name="source">来源，如Http/Mqtt</param>
    /// <returns>登录响应</returns>
    ILoginResponse Login(DeviceContext context, ILoginRequest request, String source);

    /// <summary>设备注销</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="reason">注销原因</param>
    /// <param name="source">登录来源，如Http/Mqtt</param>
    /// <returns>在线信息，不存在时返回null</returns>
    IOnlineModel? Logout(DeviceContext context, String? reason, String source);

    /// <summary>设备心跳。更新在线记录信息</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="request">心跳请求</param>
    /// <param name="response">心跳响应。如果未传入则内部实例化</param>
    /// <returns>心跳响应</returns>
    IPingResponse Ping(DeviceContext context, IPingRequest? request, IPingResponse? response = null);

    /// <summary>设置设备的长连接上线/下线</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="online">上线/下线状态，true表示上线，false表示下线</param>
    void SetOnline(DeviceContext context, Boolean online);

    /// <summary>发送命令。内部调用</summary>
    /// <param name="device">目标设备</param>
    /// <param name="command">命令对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送结果</returns>
    Task<Int32> SendCommand(IDeviceModel device, CommandModel command, CancellationToken cancellationToken = default);

    /// <summary>发送命令。外部平台级接口调用</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="model">命令输入模型</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>命令响应，超时或未等待时返回null</returns>
    Task<CommandReplyModel?> SendCommand(DeviceContext context, CommandInModel model, CancellationToken cancellationToken = default);

    /// <summary>命令响应</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="model">命令响应模型</param>
    /// <returns>处理结果</returns>
    Int32 CommandReply(DeviceContext context, CommandReplyModel model);

    /// <summary>上报事件</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="events">事件集合</param>
    /// <returns>处理的事件数量</returns>
    Int32 PostEvents(DeviceContext context, EventModel[] events);

    /// <summary>升级检查</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="channel">更新通道</param>
    /// <returns>升级信息，无升级时返回null</returns>
    IUpgradeInfo? Upgrade(DeviceContext context, String? channel);

    /// <summary>写设备历史</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="action">动作名称</param>
    /// <param name="success">是否成功</param>
    /// <param name="remark">备注内容</param>
    void WriteHistory(DeviceContext context, String action, Boolean success, String remark);
}

/// <summary>设备会话服务（扩展）</summary>
/// <remarks>
/// 扩展接口，提供更细粒度的设备服务控制，包括鉴权、注册、在线状态管理等。
/// </remarks>
public interface IDeviceService2 : IDeviceService
{
    /// <summary>验证设备合法性。验证密钥</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="request">登录请求</param>
    /// <returns>验证是否通过</returns>
    Boolean Authorize(DeviceContext context, ILoginRequest request);

    /// <summary>自动注册设备。验证密钥失败后调用</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="request">登录请求</param>
    /// <returns>注册后的设备信息</returns>
    /// <exception cref="ApiException">注册失败时抛出</exception>
    IDeviceModel Register(DeviceContext context, ILoginRequest request);

    /// <summary>鉴权后的登录处理。修改设备信息、创建在线记录和写日志</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="request">登录请求</param>
    void OnLogin(DeviceContext context, ILoginRequest request);

    /// <summary>获取设备。先查缓存再查库</summary>
    /// <param name="code">设备编码</param>
    /// <returns>设备信息，不存在时返回null</returns>
    IDeviceModel? GetDevice(String code);

    /// <summary>设备心跳。更新在线记录信息</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="request">心跳请求</param>
    /// <returns>在线信息</returns>
    IOnlineModel OnPing(DeviceContext context, IPingRequest? request);

    /// <summary>查找在线</summary>
    /// <param name="sessionId">会话标识</param>
    /// <returns>在线信息，不存在时返回null</returns>
    IOnlineModel? QueryOnline(String sessionId);

    /// <summary>获取在线。先查缓存再查库</summary>
    /// <param name="context">设备上下文</param>
    /// <returns>在线信息，不存在时返回null</returns>
    IOnlineModel? GetOnline(DeviceContext context);

    /// <summary>创建在线。先写数据库再写缓存</summary>
    /// <param name="context">设备上下文</param>
    /// <returns>新创建的在线信息</returns>
    IOnlineModel CreateOnline(DeviceContext context);

    /// <summary>删除在线。先删数据库再删缓存</summary>
    /// <param name="context">设备上下文</param>
    /// <returns>删除的记录数</returns>
    Int32 RemoveOnline(DeviceContext context);

    /// <summary>获取下行命令</summary>
    /// <param name="context">设备上下文</param>
    /// <returns>待下发的命令列表</returns>
    CommandModel[] AcquireCommands(DeviceContext context);
}

/// <summary>设备服务扩展</summary>
public static class DeviceServiceExtensions
{
    /// <summary>写设备历史</summary>
    /// <param name="deviceService">设备服务</param>
    /// <param name="device">设备信息</param>
    /// <param name="action">动作名称</param>
    /// <param name="success">是否成功</param>
    /// <param name="remark">备注内容</param>
    /// <param name="clientId">客户端标识</param>
    /// <param name="ip">远程IP地址</param>
    public static void WriteHistory(this IDeviceService deviceService, IDeviceModel device, String action, Boolean success, String remark, String? clientId, String? ip)
    {
        if (deviceService == null) throw new ArgumentNullException(nameof(deviceService));

        var ctx = new DeviceContext
        {
            Device = device,
            ClientId = clientId,
            UserHost = ip
        };
        deviceService.WriteHistory(ctx, action, success, remark);
    }
}