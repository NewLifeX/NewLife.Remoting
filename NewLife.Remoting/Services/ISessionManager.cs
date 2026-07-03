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

    /// <summary>向目标会话发送命令，支持同步等待响应</summary>
    /// <remarks>
    /// timeout=0 时 fire-and-forget，立即返回 null；timeout>0 时阻塞等待设备 CommandReply。
    /// 内部优先使用 ICommandResponseBus 事件总线等待，未注册时降级到 Redis 队列。
    /// </remarks>
    /// <param name="code">设备编码</param>
    /// <param name="command">命令模型</param>
    /// <param name="message">原始命令消息</param>
    /// <param name="timeout">超时秒数。0 不等待（fire-and-forget），大于 0 阻塞等待 CommandReply</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>命令响应，超时或 fire-and-forget 时返回 null</returns>
    Task<CommandReplyModel?> PublishAsync(String code, CommandModel command, String? message, Int32 timeout = 0, CancellationToken cancellationToken = default);
}