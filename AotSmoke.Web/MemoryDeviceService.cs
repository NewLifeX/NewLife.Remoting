using NewLife;
using NewLife.Data;
using NewLife.Remoting;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;

namespace AotSmoke.Web;

/// <summary>内存设备服务。不依赖 XCode，用于验证最小 API 宿主接入链路</summary>
/// <remarks>
/// 覆盖 <see cref="IDeviceService"/> 与 <see cref="IDeviceService2"/>，
/// 使登录鉴权、在线记录、积压命令下发等流程都能在冒烟中走通。
/// </remarks>
public class MemoryDeviceService : IDeviceService2
{
    private readonly MemoryDevice _device = new() { Code = "dev-aot", Name = "AOT设备", Enable = true, Secret = "abc" };
    private readonly Dictionary<String, MemoryOnline> _onlines = [];

    /// <summary>上报的事件</summary>
    public List<EventModel> Events { get; } = [];

    /// <summary>收到的命令回执</summary>
    public List<CommandReplyModel> Replies { get; } = [];

    #region IDeviceService
    /// <summary>查找设备</summary>
    /// <param name="code">设备编码</param>
    /// <returns></returns>
    public IDeviceModel? QueryDevice(String code) => code == _device.Code ? _device : null;

    /// <summary>设备登录</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="request">登录请求</param>
    /// <param name="source">来源</param>
    /// <returns></returns>
    public ILoginResponse Login(DeviceContext context, ILoginRequest request, String source)
    {
        if (request.Secret != _device.Secret) throw new ApiException(ApiCode.Unauthorized, "密钥错误");

        context.Device = _device;

        return new LoginResponse { Code = _device.Code, Name = _device.Name, Secret = _device.Secret };
    }

    /// <summary>设备注销</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="reason">注销原因</param>
    /// <param name="source">来源</param>
    /// <returns></returns>
    public IOnlineModel? Logout(DeviceContext context, String? reason, String source)
    {
        var online = GetOnline(context);
        if (online != null) _onlines.Remove(online.SessionId);

        return online;
    }

    /// <summary>设备心跳</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="request">心跳请求</param>
    /// <param name="response">心跳响应</param>
    /// <returns></returns>
    public IPingResponse Ping(DeviceContext context, IPingRequest? request, IPingResponse? response = null)
    {
        var online = GetOnline(context) as IOnlineModel2 ?? CreateOnline(context) as IOnlineModel2;
        online?.Save(request, null);

        return new PingResponse { ServerTime = DateTime.UtcNow.ToLong(), Period = 60 };
    }

    /// <summary>设置长连接上线/下线</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="online">上线/下线</param>
    public void SetOnline(DeviceContext context, Boolean online) { }

    /// <summary>发送命令并等待响应</summary>
    /// <param name="device">目标设备</param>
    /// <param name="command">命令</param>
    /// <param name="timeout">超时秒数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task<CommandReplyModel?> SendCommand(IDeviceModel device, CommandModel command, Int32 timeout = 0, CancellationToken cancellationToken = default)
        => Task.FromResult<CommandReplyModel?>(new CommandReplyModel { Id = command.Id, Status = CommandStatus.已完成 });

    /// <summary>发送命令</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="model">命令输入模型</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task<CommandReplyModel?> SendCommand(DeviceContext context, CommandInModel model, CancellationToken cancellationToken = default)
        => Task.FromResult<CommandReplyModel?>(new CommandReplyModel { Id = 1, Status = CommandStatus.已完成, Data = model.Command });

    /// <summary>命令响应</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="model">命令响应模型</param>
    /// <returns></returns>
    public Int32 CommandReply(DeviceContext context, CommandReplyModel model)
    {
        Replies.Add(model);

        return 1;
    }

    /// <summary>上报事件</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="events">事件集合</param>
    /// <returns></returns>
    public Int32 PostEvents(DeviceContext context, EventModel[] events)
    {
        Events.AddRange(events);

        return events.Length;
    }

    /// <summary>升级检查</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="channel">更新通道</param>
    /// <returns></returns>
    public IUpgradeInfo? Upgrade(DeviceContext context, String? channel) => new UpgradeInfo
    {
        Version = "9.9.9",
        Source = "/pkg/update.zip",
        FileHash = "HASH-0001",
    };

    /// <summary>写设备历史</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="action">动作名称</param>
    /// <param name="success">是否成功</param>
    /// <param name="remark">备注</param>
    public void WriteHistory(DeviceContext context, String action, Boolean success, String remark) { }
    #endregion

    #region IDeviceService2
    /// <summary>验证设备合法性</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="request">登录请求</param>
    /// <returns></returns>
    public Boolean Authorize(DeviceContext context, ILoginRequest request) => request.Secret == _device.Secret;

    /// <summary>自动注册设备。本冒烟不允许自动注册</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="request">登录请求</param>
    /// <returns></returns>
    public IDeviceModel Register(DeviceContext context, ILoginRequest request) => throw new ApiException(ApiCode.Forbidden, "不自动注册");

    /// <summary>鉴权后的登录处理</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="request">登录请求</param>
    public void OnLogin(DeviceContext context, ILoginRequest request) { }

    /// <summary>获取设备</summary>
    /// <param name="code">设备编码</param>
    /// <returns></returns>
    public IDeviceModel? GetDevice(String code) => QueryDevice(code);

    /// <summary>设备心跳</summary>
    /// <param name="context">设备上下文</param>
    /// <param name="request">心跳请求</param>
    /// <returns></returns>
    public IOnlineModel OnPing(DeviceContext context, IPingRequest? request) => GetOnline(context) ?? CreateOnline(context);

    /// <summary>查找在线</summary>
    /// <param name="sessionId">会话标识</param>
    /// <returns></returns>
    public IOnlineModel? QueryOnline(String sessionId) => _onlines.TryGetValue(sessionId, out var online) ? online : null;

    /// <summary>获取在线</summary>
    /// <param name="context">设备上下文</param>
    /// <returns></returns>
    public IOnlineModel? GetOnline(DeviceContext context) => context.ClientId.IsNullOrEmpty() ? null : QueryOnline(context.ClientId);

    /// <summary>创建在线</summary>
    /// <param name="context">设备上下文</param>
    /// <returns></returns>
    public IOnlineModel CreateOnline(DeviceContext context)
    {
        var online = new MemoryOnline
        {
            SessionId = context.ClientId ?? "client",
            LongLink = true,
        };
        if (!context.ClientId.IsNullOrEmpty()) _onlines[context.ClientId] = online;

        return online;
    }
    /// <summary>删除在线</summary>
    /// <param name="context">设备上下文</param>
    /// <returns></returns>
    public Int32 RemoveOnline(DeviceContext context)
    {
        if (context.ClientId.IsNullOrEmpty()) return 0;

        return _onlines.Remove(context.ClientId) ? 1 : 0;
    }

    /// <summary>获取下行命令</summary>
    /// <param name="context">设备上下文</param>
    /// <returns></returns>
    public CommandModel[] AcquireCommands(DeviceContext context) => [];
    #endregion
}

/// <summary>内存设备</summary>
public class MemoryDevice : IDeviceModel
{
    /// <summary>编码</summary>
    public String Code { get; set; } = null!;

    /// <summary>名称</summary>
    public String Name { get; set; } = null!;

    /// <summary>启用</summary>
    public Boolean Enable { get; set; }

    /// <summary>密钥</summary>
    public String Secret { get; set; } = null!;

    /// <summary>心跳周期</summary>
    public Int32 Period { get; set; } = 60;

    /// <summary>新服务器地址</summary>
    public String? NewServer { get; set; }

    /// <summary>创建历史</summary>
    /// <param name="action">动作名称</param>
    /// <param name="success">是否成功</param>
    /// <param name="content">内容</param>
    /// <returns></returns>
    public IExtend CreateHistory(String action, Boolean success, String content) => throw new NotSupportedException();

    /// <summary>创建在线</summary>
    /// <param name="sessionId">会话标识</param>
    /// <returns></returns>
    public IOnlineModel CreateOnline(String sessionId) => new MemoryOnline { SessionId = sessionId };
}

/// <summary>内存在线记录</summary>
public class MemoryOnline : IOnlineModel2
{
    /// <summary>会话标识</summary>
    public String SessionId { get; set; } = null!;

    /// <summary>登录时间</summary>
    public DateTime LoginTime { get; set; } = DateTime.Now;

    /// <summary>是否长连接</summary>
    public Boolean LongLink { get; set; }

    /// <summary>保存心跳</summary>
    /// <param name="request">心跳请求</param>
    /// <param name="context">上下文</param>
    /// <returns></returns>
    public Int32 Save(IPingRequest? request, Object? context) => 1;
}
