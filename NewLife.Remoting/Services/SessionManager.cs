using System.Collections.Concurrent;
using NewLife.Caching;
using NewLife.Log;
using NewLife.Messaging;
using NewLife.Model;
using NewLife.Net;
using NewLife.Remoting.Models;
using NewLife.Serialization;
using NewLife.Threading;
#if !NET45
using TaskEx = System.Threading.Tasks.Task;
#endif

namespace NewLife.Remoting.Services;

/// <summary>会话管理器。管理命令会话的生命周期，支持进程内或跨进程（Redis队列）消息分发</summary>
/// <remarks>
/// 主要用于管理 WebSocket 等长连接会话（如 <see cref="CommandSession"/>），实现服务端向客户端下发命令的能力。
/// 
/// <para>核心工作流程：</para>
/// <list type="number">
/// <item>客户端建立连接时，调用 <see cref="Add"/> 将会话加入管理器，以 Code 为唯一标识</item>
/// <item>服务端需要下发命令时，调用 <see cref="PublishAsync"/> 发布消息到事件总线</item>
/// <item>事件总线触发 <see cref="OnMessage"/>，根据 Code 找到对应会话并交由其处理（如通过 WebSocket 发送给客户端）</item>
/// <item>客户端断开连接时，调用 <see cref="Remove"/> 从管理器移除会话</item>
/// <item>内置定时器自动清理不活跃的会话</item>
/// </list>
/// 
/// <para>事件总线模式：</para>
/// <list type="bullet">
/// <item>单机模式：使用内存事件总线 <see cref="EventBus{T}"/>，仅支持进程内消息分发</item>
/// <item>集群模式：使用 Redis 事件总线，支持跨进程消息分发，适用于多实例部署场景</item>
/// </list>
/// 
/// <para>消息格式：</para>
/// 实际发送的消息格式为 "code#message"，其中 code 为会话标识，message 为 JSON 序列化的命令内容，因此 code 内不能包含 # 字符。
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

    /// <summary>清理会话计时器</summary>
    private TimerX? _clearTimer;

    private readonly ICache? _cache = serviceProvider.GetService<ICacheProvider>()?.Cache;
    private readonly ITracer? _tracer = serviceProvider.GetService<ITracer>();
    //private readonly ILog? _log = serviceProvider.GetService<ILog>();
    #endregion

    #region 方法
    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        CloseAll(disposing ? "Dispose" : "GC");
    }

    /// <summary>初始化事件总线。延迟初始化，首次添加会话或发布消息时触发</summary>
    private void Init()
    {
        if (Bus != null) return;
        lock (this)
        {
            if (Bus != null) return;

            Bus = Create();

            // 进程退出（如 Ctrl+C）时，主动销毁会话管理器，尽快打断会话的Receive等待
            Host.RegisterExit(() => this.TryDispose());
        }
    }

    /// <summary>创建事件总线，用于转发会话消息</summary>
    /// <returns></returns>
    protected virtual IEventBus<String> Create()
    {
        using var span = _tracer?.NewSpan($"cmd:{Topic}:Create", ClientId);

        // 创建事件总线，指定队列消费组
        IEventBus<String> bus;
        if (_cache is not MemoryCache && _cache is Cache cache)
            bus = cache.CreateEventBus<String>(Topic, ClientId);
        else
            bus = new EventBus<String>();

        // 订阅总线事件到OnMessage
        bus.Subscribe(OnMessage);

        return bus;
    }

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

        if (_dic.TryRemove(session.Code, out _)) span?.AppendTag(null!, 1);
    }

    /// <summary>向目标会话发送事件。进程内转发，或通过Redis队列</summary>
    /// <remarks>实际发送的消息是 code#message，因此code内不能带有#</remarks>
    /// <param name="code">设备编码</param>
    /// <param name="command">命令模型</param>
    /// <param name="message">原始命令消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public virtual Task<Int32> PublishAsync(String code, CommandModel command, String? message, CancellationToken cancellationToken)
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

        return Bus.PublishAsync(message, null, cancellationToken);
    }

    /// <summary>从事件总线收到事件</summary>
    /// <remarks>实际发送消息是 code#message，因此需要先解码消息找到code</remarks>
    /// <param name="message">原始命令消息</param>
    /// <param name="context">上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    protected virtual async Task OnMessage(String message, IEventContext<String> context, CancellationToken cancellationToken)
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
            msg = JsonHelper.Convert<CommandModel>(dic);
            span?.Detach(dic);
        }
        catch { }

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

                if (span != null) span.Value = 1;
            }
        }
    }

    /// <summary>根据标识获取会话</summary>
    /// <param name="key">会话标识（Code）</param>
    /// <returns></returns>
    public virtual ICommandSession? Get(String key)
    {
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

        // 释放事件总线
        Bus.TryDispose();

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
    }
    #endregion
}
