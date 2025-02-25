using NewLife.Remoting.Models;

namespace NewLife.Remoting.Services;

/// <summary>命令会话</summary>
public interface ICommandSession : IDisposable
{
    /// <summary>设备编码。用于识别指令归属</summary>
    String Code { get; set; }

    /// <summary>是否活动中</summary>
    Boolean Active { get; }

    /// <summary>处理事件</summary>
    Task HandleAsync(CommandModel command, String message, CancellationToken cancellationToken);
}

/// <summary>会话管理器接口</summary>
public interface ISessionManager
{
    /// <summary>添加会话</summary>
    /// <param name="session"></param>
    void Add(ICommandSession session);

    /// <summary>销毁会话</summary>
    void Remove(ICommandSession session);

    /// <summary>获取会话</summary>
    /// <param name="key"></param>
    /// <returns></returns>
    ICommandSession? Get(String key);

    /// <summary>向设备发送消息</summary>
    /// <param name="code"></param>
    /// <param name="command"></param>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Int32> PublishAsync(String code, CommandModel command, String message, CancellationToken cancellationToken);
}