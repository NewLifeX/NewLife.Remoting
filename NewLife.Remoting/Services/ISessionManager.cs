using NewLife.Remoting.Models;

namespace NewLife.Remoting.Services;

/// <summary>命令会话</summary>
public interface ICommandSession : IDisposable
{
    /// <summary>会话编码。用于识别指令归属</summary>
    String Code { get; set; }

    /// <summary>是否活动中</summary>
    Boolean Active { get; }

    /// <summary>处理事件</summary>
    Task HandleAsync(CommandModel command, String? message, CancellationToken cancellationToken);
}

/// <summary>会话管理器接口</summary>
public interface ISessionManager
{
    /// <summary>添加会话</summary>
    /// <param name="session"></param>
    void Add(ICommandSession session);

    /// <summary>删除会话</summary>
    void Remove(ICommandSession session);

    /// <summary>获取会话</summary>
    /// <param name="key"></param>
    /// <returns></returns>
    ICommandSession? Get(String key);

    /// <summary>向目标会话发送事件。进程内转发，或通过Redis队列</summary>
    /// <param name="code">设备编码</param>
    /// <param name="command">命令模型</param>
    /// <param name="message">原始命令消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<Int32> PublishAsync(String code, CommandModel command, String? message, CancellationToken cancellationToken);
}