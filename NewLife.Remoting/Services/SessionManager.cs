using System.Collections.Concurrent;
using System.Diagnostics;
using NewLife.Caching;
using NewLife.Log;
using NewLife.Messaging;
using NewLife.Model;
using NewLife.Net;
using NewLife.Remoting.Models;
using NewLife.Serialization;
using NewLife.Threading;
#if !NET45
using TaskEx=System.Threading.Tasks.Task;
#endif

namespace NewLife.Remoting.Services;

/// <summary>会话管理器</summary>
/// <remarks>实例化</remarks>
public class SessionManager(IServiceProvider serviceProvider) : DisposeBase, ISessionManager
{
    /// <summary>主题</summary>
    public String Topic { get; set; } = "Commands";

    /// <summary>事件总线</summary>
    public IEventBus<String> Bus { get; set; } = null!;

    /// <summary>清理周期。单位毫秒，默认10秒。</summary>
    public Int32 ClearPeriod { get; set; } = 10;

    private readonly ConcurrentDictionary<String, ICommandSession> _dic = new();
    /// <summary>会话集合</summary>
    public IDictionary<String, ICommandSession> Sessions => _dic;

    /// <summary>清理会话计时器</summary>
    private TimerX? _clearTimer;

    private readonly ICache? _cache = serviceProvider.GetService<ICacheProvider>()?.Cache;
    private readonly ITracer? _tracer = serviceProvider.GetService<ITracer>();
    private readonly ILog? _log = serviceProvider.GetService<ILog>();

    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        CloseAll(disposing ? "Dispose" : "GC");
    }

    private void Init()
    {
        if (Bus != null) return;
        lock (this)
        {
            if (Bus != null) return;

            // 创建事件总线，指定队列消费组
            if (_cache is not MemoryCache && _cache is Cache cache)
                Bus = cache.GetEventBus<String>(Topic, $"{Environment.MachineName}-{Process.GetCurrentProcess().Id}");
            else
                Bus = new EventBus<String>();

            // 订阅总线事件到OnMessage
            Bus.Subscribe(OnMessage);
        }
    }

    /// <summary>创建会话</summary>
    /// <param name="session"></param>
    public virtual void Add(ICommandSession session)
    {
        Init();

        _dic.AddOrUpdate(session.Code, session, (k, s) => s);

        if (session is IDisposable2 ds)
            ds.OnDisposed += (s, e) => _dic.Remove((s as ICommandSession)?.Code + "");

        var p = ClearPeriod * 1000;
        if (p > 0)
            _clearTimer ??= new TimerX(RemoveNotAlive, null, p, p) { Async = true, };
    }

    /// <summary>销毁会话</summary>
    public virtual void Remove(ICommandSession session)
    {
        if (session != null) _dic.Remove(session.Code);
    }

    /// <summary>向设备发送消息</summary>
    /// <param name="code"></param>
    /// <param name="command"></param>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public virtual Task<Int32> PublishAsync(String code, CommandModel command, String message, CancellationToken cancellationToken)
    {
        if (command == null && message.IsNullOrEmpty()) throw new ArgumentNullException(nameof(command), "命令和消息不能同时为空");

        if (command != null && command.TraceId.IsNullOrEmpty()) command.TraceId = DefaultSpan.Current?.TraceId;
        if (message.IsNullOrEmpty()) message = command!.ToJson();

        message = $"{code}#{message}";

        Init();

        return Bus.PublishAsync(message, null, cancellationToken);
    }

    /// <summary>从事件总线收到事件</summary>
    /// <param name="message"></param>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected virtual async Task OnMessage(String message, IEventContext<String> context, CancellationToken cancellationToken)
    {
        using var span = _tracer?.NewSpan($"cmd:{Topic}", message);

        var code = "";
        var p = message.IndexOf('#');
        if (p > 0 && p < 32)
        {
            code = message[..p];
            message = message[(p + 1)..];
        }

        // 解码
        var dic = JsonParser.Decode(message)!;
        var msg = JsonHelper.Convert<CommandModel>(dic);
        span?.Detach(dic);

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
            if (session != null) await session.HandleAsync(msg, message, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>获取会话</summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public virtual ICommandSession? Get(String key)
    {
        if (!_dic.TryGetValue(key, out var session)) return null;

        return session;
    }

    /// <summary>关闭所有</summary>
    public virtual void CloseAll(String reason)
    {
        if (_dic.IsEmpty) return;

        foreach (var item in _dic.ToValueArray())
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

        var todel = new Dictionary<String, ICommandSession>();

        foreach (var elm in _dic)
        {
            var session = elm.Value;
            // 判断是否活跃
            if (session == null || session is IDisposable2 ds && ds.Disposed || !session.Active)
            {
                todel.Add(elm.Key, elm.Value);
            }
        }
        // 从会话集合里删除这些键值，并行字典操作安全
        foreach (var item in todel)
        {
            _dic.Remove(item.Key);
        }

        // 慢慢释放各个会话
        foreach (var item in todel)
        {
            if (item.Value is ILogFeature lf)
                lf.Log?.Info("[{0}]不活跃销毁", item.Key);

            if (item.Value is INetSession ss) ss.Close(nameof(RemoveNotAlive));
            item.Value.TryDispose();
        }
    }
}
