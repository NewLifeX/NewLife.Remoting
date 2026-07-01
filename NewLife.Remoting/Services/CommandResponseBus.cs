using System.Collections.Concurrent;
using NewLife.Log;
using NewLife.Messaging;
using NewLife.Model;
using NewLife.Remoting.Models;
using NewLife.Serialization;

namespace NewLife.Remoting.Services;

/// <summary>命令响应总线实现。基于事件总线广播命令响应，替代 cmdreply 队列</summary>
/// <remarks>
/// 内部使用 ConcurrentDictionary 维护本地回调注册表，通过事件总线订阅响应广播频道。
/// 
/// <para>单机模式：使用内存 EventBus，进程内回调匹配</para>
/// <para>集群模式：通过 IEventBusFactory 创建 Redis EventBus，跨进程回调匹配</para>
/// </remarks>
public class CommandResponseBus : DisposeBase, ICommandResponseBus
{
    #region 属性
    /// <summary>响应主题。事件总线的频道名称，默认 CommandReplies</summary>
    public String Topic { get; set; } = "CommandReplies";

    /// <summary>回调注册表。Key=CommandId，Value=TaskCompletionSource</summary>
    private readonly ConcurrentDictionary<Int64, CallbackEntry> _callbacks = new();

    /// <summary>事件总线。用于跨进程响应广播</summary>
    private IEventBus<String>? _bus;

    /// <summary>事件总线工厂。注入后可创建 Redis 等跨进程事件总线</summary>
    public IEventBusFactory? Factory { get; set; }

    private readonly IServiceProvider _serviceProvider;
    private readonly ITracer? _tracer;
    #endregion

    #region 构造
    /// <summary>实例化命令响应总线</summary>
    /// <param name="serviceProvider">服务提供者</param>
    public CommandResponseBus(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _tracer = serviceProvider.GetService<ITracer>();
    }
    #endregion

    #region 方法
    /// <summary>初始化事件总线。延迟初始化，首次使用时触发</summary>
    private void Init()
    {
        if (_bus != null) return;
        lock (this)
        {
            if (_bus != null) return;

            // 优先使用工厂创建跨进程总线（如 Redis），降级到内存总线
            _bus = Factory?.CreateEventBus<String>(Topic, "ResponseBus");
            _bus ??= new EventBus<String>();

            _bus.Subscribe(OnCommandResponse);
        }
    }

    /// <summary>等待命令响应</summary>
    /// <param name="commandId">命令 Id</param>
    /// <param name="timeout">超时时间（秒）。0 表示不等待</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>命令响应，超时返回 null</returns>
    public virtual async Task<CommandReplyModel?> WaitResponseAsync(Int64 commandId, Int32 timeout, CancellationToken cancellationToken)
    {
        if (timeout <= 0) return null;

        // 快速路径：令牌已取消则立即抛出
        cancellationToken.ThrowIfCancellationRequested();

        Init();

        using var span = _tracer?.NewSpan($"cmd:{Topic}:Wait", commandId);
        try
        {
#if NET45
            var tcs = new TaskCompletionSource<CommandReplyModel>();
#else
            var tcs = new TaskCompletionSource<CommandReplyModel>(TaskCreationOptions.RunContinuationsAsynchronously);
#endif
            var entry = new CallbackEntry
            {
                Tcs = tcs,
                CreatedAt = Runtime.TickCount64,
                Timeout = timeout * 1000,
                Span = span,
            };

            _callbacks.TryAdd(commandId, entry);

            // 使用取消令牌注册清理
            using var ctr = cancellationToken.Register(() =>
            {
                if (_callbacks.TryRemove(commandId, out var e))
                {
#if NET45
                    e.Tcs.TrySetCanceled();
#else
                    e.Tcs.TrySetCanceled(cancellationToken);
#endif
                    e.Span?.AppendTag($"cancelled: commandId={commandId}");
                }
            });

            // 超时清理（通过 Task.Delay 实现）
            var timeoutMs = timeout * 1000;
            if (timeoutMs > 0)
            {
                var delayTask = Task.Delay(timeoutMs, cancellationToken);
                var completedTask = await Task.WhenAny(tcs.Task, delayTask).ConfigureAwait(false);

                if (completedTask == delayTask)
                {
                    // 超时或外部取消。区分是主动取消还是超时
                    if (cancellationToken.IsCancellationRequested)
                    {
                        span?.AppendTag($"cancelled: commandId={commandId}");
                        throw new OperationCanceledException(cancellationToken);
                    }

                    if (_callbacks.TryRemove(commandId, out var e))
                    {
                        e.Tcs.TrySetResult(null!);
                        span?.AppendTag($"timeout: commandId={commandId}, timeout={timeout}s");
                        return null;
                    }
                }
            }

            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            _callbacks.TryRemove(commandId, out _);
        }
    }

    /// <summary>发布命令响应。广播到所有实例</summary>
    /// <param name="reply">命令响应</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public virtual async Task<Int32> PublishResponseAsync(CommandReplyModel reply, CancellationToken cancellationToken)
    {
        if (reply == null) throw new ArgumentNullException(nameof(reply));

        Init();

        var jsonHost = _serviceProvider.GetService<IJsonHost>();
        var message = jsonHost != null ? jsonHost.Write(reply) : reply.ToJson();

        using var span = _tracer?.NewSpan($"cmd:{Topic}:Publish", message);
        try
        {
            return await _bus!.PublishAsync(message, null, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }

    /// <summary>收到命令响应广播</summary>
    /// <param name="message">响应 JSON</param>
    /// <param name="context">事件上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    protected virtual Task OnCommandResponse(String message, IEventContext context, CancellationToken cancellationToken)
    {
        using var span = _tracer?.NewSpan($"cmd:{Topic}:OnResponse", message);

        CommandReplyModel? reply = null;
        try
        {
            var dic = JsonParser.Decode(message)!;
            reply = JsonHelper.Convert<CommandReplyModel>(dic);
            span?.Detach(dic);
        }
        catch (Exception ex)
        {
            span?.SetError(ex);
            return _completedTask;
        }

        if (reply == null || reply.Id == 0) return _completedTask;

        // 按 CommandId 匹配本地回调
        if (_callbacks.TryGetValue(reply.Id, out var entry))
        {
            span?.AppendTag($"matched: commandId={reply.Id}, status={reply.Status}");
            entry.Tcs.TrySetResult(reply);
            entry.Span?.AppendTag($"resolved: status={reply.Status}");
        }
        else
            span?.AppendTag($"no callback for commandId={reply.Id} on this node");

        return _completedTask;
    }
    #endregion

    #region 辅助
#if NET45
    private static readonly Task _completedTask = TaskEx.FromResult(0);
#else
    private static readonly Task _completedTask = Task.CompletedTask;
#endif
    #endregion

    #region 内部类
    private class CallbackEntry
    {
        public TaskCompletionSource<CommandReplyModel> Tcs { get; set; } = null!;
        public Int64 CreatedAt { get; set; }
        public Int32 Timeout { get; set; }
        public ISpan? Span { get; set; }
    }
    #endregion

    #region 定时清理
    /// <summary>清理过期的回调注册。可定期调用防止内存泄漏</summary>
    /// <returns>清理的回调数量</returns>
    public virtual Int32 CleanupExpiredCallbacks()
    {
        var count = 0;
        var now = Runtime.TickCount64;

        foreach (var kv in _callbacks)
        {
            if (now - kv.Value.CreatedAt > kv.Value.Timeout)
            {
                if (_callbacks.TryRemove(kv.Key, out var entry))
                {
                    entry.Tcs.TrySetResult(null!);
                    count++;
                }
            }
        }

        return count;
    }
    #endregion

    #region 日志
    /// <summary>链路追踪</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;
    #endregion
}
