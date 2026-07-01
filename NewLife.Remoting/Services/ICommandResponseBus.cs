using NewLife.Remoting.Models;

namespace NewLife.Remoting.Services;

/// <summary>命令响应总线。基于事件总线广播命令响应，替代 cmdreply 队列机制</summary>
/// <remarks>
/// 解决原 cmdreply:{id} Redis 队列的空队列残留问题和内存模式下的跨实例响应丢失问题。
/// 
/// <para>工作原理：</para>
/// <list type="number">
/// <item>SendCommand 发起方调用 <see cref="WaitResponseAsync"/> 注册本地回调（TaskCompletionSource）</item>
/// <item>设备 CommandReply 调用 <see cref="PublishResponseAsync"/> 通过事件总线广播响应</item>
/// <item>事件总线触发各实例的 OnCommandResponse，按 CommandId 匹配并完成本地回调</item>
/// <item>超时后自动清理回调注册，避免内存泄漏</item>
/// </list>
/// </remarks>
public interface ICommandResponseBus
{
    /// <summary>等待命令响应。阻塞直到收到响应或超时</summary>
    /// <param name="commandId">命令 Id</param>
    /// <param name="timeout">超时时间（秒）。0 表示不等待</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>命令响应，超时返回 null</returns>
    Task<CommandReplyModel?> WaitResponseAsync(Int64 commandId, Int32 timeout, CancellationToken cancellationToken);

    /// <summary>发布命令响应。由 CommandReply 调用，通过事件总线广播到所有实例</summary>
    /// <param name="reply">命令响应</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<Int32> PublishResponseAsync(CommandReplyModel reply, CancellationToken cancellationToken);
}
