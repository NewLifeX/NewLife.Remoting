using System.Collections.Concurrent;
using NewLife.Caching;
using NewLife.Log;
using NewLife.Messaging;
using NewLife.Model;
using NewLife.Net;
using NewLife.Remoting.Models;
using NewLife.Serialization;
using NewLife.Threading;

namespace NewLife.Remoting.Services;

/// <summary>会话管理器。管理命令会话生命周期，内置命令下发、响应等待、响应广播的完整闭环</summary>
/// <remarks>
/// 主要用于管理 WebSocket 等长连接会话（如 <see cref="CommandSession"/>），
/// 实现服务端向客户端下发命令并同步等待设备响应的能力，无需额外依赖独立响应总线服务。
/// 
/// <para>四大核心流程：</para>
/// <list type="number">
/// <item>
/// <b>命令下发管线</b>：<see cref="PublishAsync"/> → <see cref="Init"/>/<see cref="Create"/>（事件总线 <see cref="Topic"/>）→ 广播 code#json →
/// <see cref="OnMessage"/>（匹配 Code 找到 Session）→ session.HandleAsync（WebSocket 下发）
/// </item>
/// <item>
/// <b>响应等待管线</b>：<see cref="PublishAsync"/> timeout&gt;0 → <see cref="WaitResponseAsync"/>（注册 <see cref="CallbackEntry"/> 到 _callbacks 字典）→
/// 阻塞等待 TCS → 超时/取消/收到响应后返回
/// </item>
/// <item>
/// <b>响应广播管线</b>：设备 CommandReply → <see cref="PublishResponseAsync"/> → <see cref="InitResponse"/>（事件总线 <see cref="ResponseTopic"/>）→
/// 广播 JSON → <see cref="OnCommandResponse"/>（按 CommandId 匹配 _callbacks）→ 完成 TCS
/// </item>
/// <item>
/// <b>会话生命周期</b>：<see cref="Add"/> → <see cref="Remove"/> → <see cref="CloseAll"/> → 定时器 <see cref="RemoveNotAlive"/>
/// </item>
/// </list>
/// 
/// <para>集群多实例工作流（ABC 三实例典型场景）：</para>
/// <list type="bullet">
/// <item>A 实例调用 <see cref="PublishAsync"/> 发起命令，通过事件总线「广播」命令到全部实例</item>
/// <item>B 实例的 <see cref="OnMessage"/> 匹配到设备会话，通过 WebSocket 下发给设备</item>
/// <item>设备执行完毕，HTTP 上报 CommandReply 到 C 实例的接口</item>
/// <item>C 实例调用 <see cref="PublishResponseAsync"/> 通过事件总线「广播」响应到全部实例</item>
/// <item>A 实例的 <see cref="OnCommandResponse"/> 按 CommandId 匹配 _callbacks 中的等待回调，完成 TCS</item>
/// <item>超时或取消时自动清理回调注册（finally + 定时器兜底），防止内存泄漏</item>
/// </list>
/// 
/// <para>事件总线模式：</para>
/// <list type="bullet">
/// <item>单机模式：使用内存 <see cref="EventBus{T}"/>，仅支持进程内消息分发</item>
/// <item>集群模式：通过 <see cref="IEventBusFactory"/> 创建 Redis EventBus，支持跨进程消息分发</item>
/// </list>
/// 
/// <para>消息格式：</para>
/// 命令消息为 "code#json"；响应消息为纯 JSON（<see cref="CommandReplyModel"/>），code 内不能包含 #。
/// </remarks>
public class SessionManager(IServiceProvider serviceProvider) : DisposeBase, ISessionManager
{
    #region 属性
    /// <summary>主题。事件总线的队列名称，默认 Commands</summary>
    public String Topic { get; set; } = "Commands";

    /// <summary>客户端标识。机器名+进程号</summary>
    /// <remarks>在事件总线中，用做 Redis 队列的消费组标识，用于区分不同进程实例</remarks>
    public String ClientId { get; set; } = Runtime.ClientId;

    /// <summary>事件总线。用于跨进程消息分发，单机模式使用内存总线，集群模式使用 Redis 总线</summary>
    public IEventBus<String> Bus { get; set; } = null!;

    /// <summary>清理周期。单位秒，默认10秒。定时清理不活跃的会话</summary>
    public Int32 ClearPeriod { get; set; } = 10;

    private readonly ConcurrentDictionary<String, ICommandSession> _dic = new();
    /// <summary>会话集合。以 Code 为键的并发字典</summary>
    public IDictionary<String, ICommandSession> Sessions => _dic;

    /// <summary>事件总线工厂</summary>
    public IEventBusFactory? Factory { get; set; }

    /// <summary>响应主题。事件总线的响应频道名称，默认从 Topic 派生</summary>
    public String ResponseTopic => $"{Topic}Replies";

    /// <summary>命令响应回调注册表。Key=CommandId，Value=TaskCompletionSource</summary>
    private readonly ConcurrentDictionary<Int64, CallbackEntry> _callbacks = new();

    /// <summary>命令响应事件总线。用于跨进程响应广播</summary>
    private IEventBus<String>? _responseBus;

    /// <summary>清理会话计时器</summary>
    private TimerX? _clearTimer;

    private readonly ICacheProvider? _cacheProvider = serviceProvider.GetService<ICacheProvider>();
    private readonly ITracer? _tracer = serviceProvider.GetService<ITracer>();

#if NET45
    private static readonly Task _completedTask = TaskEx.FromResult(0);
#else
    private static readonly Task _completedTask = Task.CompletedTask;
#endif
    #endregion

    #region 构造/销毁
    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        CloseAll(disposing ? "Dispose" : "GC");
    }
    #endregion

    #region 命令下发管线
    /// <summary>向目标会话发送命令，支持同步等待响应。进程内转发或通过 Redis 事件总线</summary>
    /// <remarks>实际发送的消息是 code#message，因此 code 内不能带有 #</remarks>
    /// <param name="code">设备编码</param>
    /// <param name="command">命令模型</param>
    /// <param name="message">原始命令消息</param>
    /// <param name="timeout">超时时间（秒），0 表示不超时</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>命令响应，超时或 fire-and-forget 时返回 null</returns>
    public virtual async Task<CommandReplyModel?> PublishAsync(String code, CommandModel command, String? message, Int32 timeout = 0, CancellationToken cancellationToken = default)
    {
        if (command == null && message.IsNullOrEmpty()) throw new ArgumentNullException(nameof(command), "命令和消息不能同时为空");

        if (command != null && command.TraceId.IsNullOrEmpty()) command.TraceId = DefaultSpan.Current?.TraceId;
        if (message.IsNullOrEmpty() && command != null)
        {
            var jsonHost = serviceProvider.GetService<IJsonHost>();
            if (jsonHost != null)
                message = jsonHost.Write(command);
            else
                message = command.ToJson();
        }

        message = $"{code}#{message}";

        using var span = _tracer?.NewSpan($"cmd:{Topic}:Publish", message);

        Init();

        // 发布到命令总线
        await Bus.PublishAsync(message, null, cancellationToken).ConfigureAwait(false);

        if (timeout <= 0) return null;

        // 通过内置事件总线回调机制等待设备响应
        return await WaitResponseAsync(command!.Id, timeout, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>初始化命令事件总线。延迟初始化，首次添加会话或发布消息时触发</summary>
    private void Init()
    {
        if (Bus != null) return;
        lock (this)
        {
            if (Bus != null) return;

            Bus = Create();

            // 进程退出（如 Ctrl+C）时，主动销毁会话管理器，尽快打断会话的 Receive 等待
            Host.RegisterExit(() => this.TryDispose());
        }
    }

    /// <summary>创建命令事件总线，用于转发会话消息</summary>
    /// <returns></returns>
    protected virtual IEventBus<String> Create()
    {
        using var span = _tracer?.NewSpan($"cmd:{Topic}:CreateBus", ClientId);
        try
        {
            // 创建事件总线，指定队列消费组
            var bus = Factory?.CreateEventBus<String>(Topic, ClientId);
            bus ??= _cacheProvider?.CreateEventBus<String>(Topic, ClientId);
            bus ??= new EventBus<String>();

            // 订阅总线事件到 OnMessage
            span?.AppendTag($"订阅[{Topic}]的总线事件到OnMessage，再根据消息头的设备号分发给各Session，一般是WebSocket下发指令");
            bus.Subscribe(OnMessage);

            return bus;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }

    /// <summary>从事件总线收到命令消息。解析 code#message 格式，找到对应会话并下发给设备</summary>
    /// <param name="message">原始命令消息</param>
    /// <param name="context">事件上下文。用于在发布者、订阅者及中间处理器之间传递协调数据，如 Handler、ClientId 等</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    protected virtual async Task OnMessage(String message, IEventContext context, CancellationToken cancellationToken)
    {
        using var span = _tracer?.NewSpan($"cmd:{Topic}", message);

        // 解析消息格式 code#message
        var code = "";
        var p = message.IndexOf('#');
        if (p > 0 && p < 256)
        {
            code = message[..p];
            message = message[(p + 1)..];
        }

        // 解码命令模型。即使失败，也要继续处理
        CommandModel? msg = null;
        try
        {
            var dic = JsonParser.Decode(message)!;
            span?.Detach(dic);
            msg = JsonHelper.Convert<CommandModel>(dic);
        }
        catch (Exception ex)
        {
            span?.SetError(ex);
        }

        // 修正时间
        if (msg != null)
        {
            if (msg.StartTime.Year < 2000) msg.StartTime = DateTime.MinValue;
            if (msg.Expire.Year < 2000) msg.Expire = DateTime.MinValue;
        }

        // 交由命令会话处理，包括过期处理，因其内部可能需要写过期日志
        //if (msg != null && (msg.Expire.Year <= 2000 || msg.Expire >= Runtime.UtcNow))
        {
            var session = Get(code);
            if (session != null)
            {
                await session.HandleAsync(msg!, message, cancellationToken).ConfigureAwait(false);

                span?.Value = 1;
            }
            else
                span?.AppendTag($"未找到编号为[{code}]的会话，无法处理消息。");
        }
    }
    #endregion

    #region 响应等待管线
    /// <summary>等待命令响应。注册 CallbackEntry 并阻塞直到收到响应或超时</summary>
    /// <remarks>
    /// 内部通过 ConcurrentDictionary{CommandId, CallbackEntry} 维护回调注册表。
    /// 每个回调生命周期 ≤ timeout 秒，finally 保证清理，定时器兜底清理过期项，不会内存泄漏。
    /// </remarks>
    /// <param name="commandId">命令 Id</param>
    /// <param name="timeout">超时时间（秒）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>命令响应，超时返回 null</returns>
    protected virtual async Task<CommandReplyModel?> WaitResponseAsync(Int64 commandId, Int32 timeout, CancellationToken cancellationToken)
    {
        if (timeout <= 0) return null;

        InitResponse();

        using var span = _tracer?.NewSpan($"cmd:{ResponseTopic}:Wait", commandId);
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

            // 超时清理
            var timeoutMs = timeout * 1000;
            var delayTask = Task.Delay(timeoutMs, cancellationToken);
            var completedTask = await Task.WhenAny(tcs.Task, delayTask).ConfigureAwait(false);

            if (completedTask == delayTask)
            {
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

            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            _callbacks.TryRemove(commandId, out _);
        }
    }

    /// <summary>发布命令响应。由 CommandReply 入口调用，通过响应事件总线广播到所有实例</summary>
    /// <param name="reply">命令响应</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public virtual async Task<Int32> PublishResponseAsync(CommandReplyModel reply, CancellationToken cancellationToken)
    {
        if (reply == null) throw new ArgumentNullException(nameof(reply));

        InitResponse();

        // 转字典注入 TraceId，用于调用链关联。不污染 CommandReplyModel 的数据契约
        var dic = reply.ToDictionary();
        dic["TraceId"] = DefaultSpan.Current?.ToString();

        var jsonHost = serviceProvider.GetService<IJsonHost>();
        var message = jsonHost != null ? jsonHost.Write(dic) : dic.ToJson();

        using var span = _tracer?.NewSpan($"cmd:{ResponseTopic}:Publish", message);
        try
        {
            return await _responseBus!.PublishAsync(message, null, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }

    /// <summary>初始化命令响应事件总线。延迟初始化，首次发送带超时的命令或发布响应时触发</summary>
    private void InitResponse()
    {
        if (_responseBus != null) return;
        lock (this)
        {
            if (_responseBus != null) return;

            var topic = ResponseTopic;
            using var span = _tracer?.NewSpan($"cmd:{topic}:CreateResponseBus", ClientId);
            try
            {
                // 优先使用工厂创建跨进程总线（如 Redis），降级到内存总线
                _responseBus = Factory?.CreateEventBus<String>(topic, ClientId);
                _responseBus ??= _cacheProvider?.CreateEventBus<String>(topic, ClientId);
                _responseBus ??= new EventBus<String>();

                span?.AppendTag($"订阅[{topic}]的总线事件到OnCommandResponse，用于匹配命令响应回调");
                _responseBus.Subscribe(OnCommandResponse);
            }
            catch (Exception ex)
            {
                span?.SetError(ex, null);
                throw;
            }
        }
    }

    /// <summary>收到命令响应广播。按 CommandId 匹配本地 _callbacks 字典中的等待回调并完成 TCS</summary>
    /// <param name="message">响应 JSON</param>
    /// <param name="context">事件上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    protected virtual Task OnCommandResponse(String message, IEventContext context, CancellationToken cancellationToken)
    {
        using var span = _tracer?.NewSpan($"cmd:{ResponseTopic}:OnResponse", message);

        CommandReplyModel? reply = null;
        try
        {
            var dic = JsonParser.Decode(message)!;
            span?.Detach(dic);
            reply = JsonHelper.Convert<CommandReplyModel>(dic);
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

    #region 会话生命周期
    /// <summary>添加会话到管理器</summary>
    /// <param name="session">命令会话</param>
    public virtual void Add(ICommandSession session)
    {
        using var span = _tracer?.NewSpan($"cmd:{Topic}:Add", session.Code);

        Init();

        _dic.AddOrUpdate(session.Code, session, (k, s) => s);

        // 监听会话销毁事件，自动从管理器移除
        if (session is IDisposable2 ds)
            ds.OnDisposed += (s, e) => Remove((s as ICommandSession)!);

        // 启动清理定时器
        var p = ClearPeriod * 1000;
        if (p > 0)
            _clearTimer ??= new TimerX(RemoveNotAlive, null, p, p) { Async = true, };
    }

    /// <summary>从管理器删除会话</summary>
    /// <param name="session">命令会话</param>
    public virtual void Remove(ICommandSession session)
    {
        if (session == null) return;

        using var span = _tracer?.NewSpan($"cmd:{Topic}:Remove", session.Code);

        if (_dic.TryRemove(session.Code, out _))
            span?.AppendTag(null!, 1);
    }

    /// <summary>根据标识获取会话</summary>
    /// <param name="key">会话标识（Code）</param>
    /// <returns></returns>
    public virtual ICommandSession? Get(String key)
    {
        if (key.IsNullOrEmpty()) return null;

        if (!_dic.TryGetValue(key, out var session)) return null;

        return session;
    }

    /// <summary>关闭所有会话并释放资源</summary>
    /// <param name="reason">关闭原因</param>
    public virtual void CloseAll(String reason)
    {
        // 停止清理定时器
        _clearTimer.TryDispose();
        _clearTimer = null;

        // 清理响应回调
        if (!_callbacks.IsEmpty)
        {
            foreach (var kv in _callbacks)
            {
                kv.Value.Tcs.TrySetCanceled();
            }
            _callbacks.Clear();
        }

        // 释放事件总线
        Bus.TryDispose();
        _responseBus.TryDispose();
        _responseBus = null;

        if (_dic.IsEmpty) return;

        using var span = _tracer?.NewSpan($"cmd:{Topic}:CloseAll", reason, _dic.Count);

        var arr = _dic.ToValueArray();
        _dic.Clear();

        foreach (var item in arr)
        {
            if (item is IDisposable2 ds && !ds.Disposed)
            {
                if (item is INetSession ss) ss.Close(reason);

                item.TryDispose();
            }
        }
    }
    #endregion

    #region 定时清理
    /// <summary>移除不活动的会话</summary>
    private void RemoveNotAlive(Object? state)
    {
        if (_dic.IsEmpty) return;

        var todel = new List<KeyValuePair<String, ICommandSession>>();

        foreach (var elm in _dic)
        {
            // 判断是否活跃
            var session = elm.Value;
            if (session == null || session is IDisposable2 ds && ds.Disposed || !session.Active)
            {
                todel.Add(elm);
            }
        }

        if (todel.Count == 0) return;

        using var span = _tracer?.NewSpan($"cmd:{Topic}:RemoveNotAlive", todel.Join(",", e => e.Key), todel.Count);

        // 从会话集合里删除并释放各个会话
        foreach (var item in todel)
        {
            // 从字典移除
            _dic.TryRemove(item.Key, out _);

            // 记录日志
            if (item.Value is ILogFeature lf)
                lf.Log?.Info("[{0}]不活跃销毁", item.Key);

            // 关闭并释放会话
            if (item.Value is INetSession ss) ss.Close(nameof(RemoveNotAlive));
            item.Value.TryDispose();
        }

        // 清理过期的命令响应回调
        CleanupExpiredCallbacks();
    }

    /// <summary>清理过期的命令响应回调。定时器兜底，防止极端情况下 callback 泄漏</summary>
    private void CleanupExpiredCallbacks()
    {
        if (_callbacks.IsEmpty) return;

        var now = Runtime.TickCount64;
        var count = 0;

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

        if (count > 0)
        {
            using var span = _tracer?.NewSpan($"cmd:{Topic}:CleanupCallbacks", count);
        }
    }
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
}
