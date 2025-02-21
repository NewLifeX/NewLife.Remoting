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
using TaskEx=System.Threading.Tasks.Task;
#endif

namespace NewLife.Remoting.Services;

/// <summary>命令会话</summary>
public interface IDeviceSession : IDisposable
{
    /// <summary>最后一次通信时间，主要表示会话活跃时间，包括收发</summary>
    DateTime LastTime { get; }

    /// <summary>开始数据交互</summary>
    /// <param name="source"></param>
    void Start(CancellationTokenSource source);

    /// <summary>处理事件</summary>
    Task HandleAsync(CommandModel command);
}

//class DeviceEventContext(IEventBus<String> bus, String code) : EventContext<String>(bus)
//{
//    /// <summary>设备编码</summary>
//    public String Code { get; set; } = code;
//}

/// <summary>会话管理器</summary>
public class SessionManager : DisposeBase
{
    /// <summary>主题</summary>
    public String Topic { get; set; } = "Commands";

    /// <summary>事件总线</summary>
    public IEventBus<String> Bus { get; set; } = null!;

    /// <summary>清理周期。单位毫秒，默认10秒。</summary>
    public Int32 ClearPeriod { get; set; } = 10;

    /// <summary>会话超时时间。默认30秒</summary>
    public Int32 Timeout { get; set; } = 30;

    /// <summary>清理会话计时器</summary>
    private TimerX? _clearTimer;
    private readonly ConcurrentDictionary<String, IDeviceSession> _dic = new();

    private readonly ICache? _cache;
    private readonly ITracer _tracer;
    private readonly ILog _log;

    /// <summary>实例化</summary>
    public SessionManager(ITracer tracer, ILog log, IServiceProvider serviceProvider)
    {
        _tracer = tracer;
        _log = log;
        var cacheProvider = serviceProvider.GetService<ICacheProvider>();
        var cache = cacheProvider?.Cache;
        if (cache != null && cache is not MemoryCache) _cache = cache;
    }

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

            // 事件总线
            if (_cache is not MemoryCache && _cache is Cache cache)
                Bus = cache.GetEventBus<String>(Topic);
            else
                Bus = new EventBus<String>();

            // 订阅总线事件到OnMessage
            Bus.Subscribe(OnMessage);

            //_source = new CancellationTokenSource();

            //_ = Task.Run(() => ConsumeMessage(_queue2, _source));
        }
    }

    /// <summary>创建会话</summary>
    /// <param name="code"></param>
    /// <returns></returns>
    public IDeviceSession Create(String code)
    {
        Init();

        var session = new DeviceSession
        {
            Code = code
        };

        _dic.AddOrUpdate(code, session, (k, s) => s);

        session.OnDisposed += (s, e) =>
        {
            _dic.Remove((s as DeviceSession)?.Code + "");
            //Bus.Unsubscribe(code);
        };

        var p = ClearPeriod * 1000;
        if (p > 0)
            _clearTimer ??= new TimerX(RemoveNotAlive, null, p, p) { Async = true, };

        return session;
    }

    /// <summary>向设备发送消息</summary>
    /// <param name="code"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public Task PublishAsync(String code, String message)
    {
        //// 如果有缓存提供者，则使用缓存提供者的队列，否则直接进入事件总线
        //_queue ??= _cache?.GetQueue<String>(Topic);

        //if (_queue != null)
        //{
        //    _queue.Add($"{code}#{message}");

        //    return TaskEx.CompletedTask;
        //}

        message = $"{code}#{message}";

        return Bus.PublishAsync(message);
    }

    private async Task OnMessage(String message)
    {
        using var span = _tracer?.NewSpan($"mq:Command", message);

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

        // 交由设备会话处理
        if (msg != null && (msg.Expire.Year <= 2000 || msg.Expire >= Runtime.UtcNow))
        {
            var session = Get(code);
            if (session != null) await session.HandleAsync(msg).ConfigureAwait(false);
        }
    }

    /// <summary>获取会话，加锁</summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public IDeviceSession? Get(String key)
    {
        if (!_dic.TryGetValue(key, out var session)) return null;

        return session;
    }

    /// <summary>关闭所有</summary>
    public void CloseAll(String reason)
    {
        if (!_dic.Any()) return;

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
        if (!_dic.Any()) return;

        var timeout = Timeout;
        var keys = new List<String>();
        var values = new List<IDeviceSession>();

        foreach (var elm in _dic)
        {
            var item = elm.Value;
            // 判断是否已超过最大不活跃时间
            if (item == null || item is IDisposable2 ds && ds.Disposed || timeout > 0 && IsNotAlive(item, timeout))
            {
                keys.Add(elm.Key);
                values.Add(elm.Value);
            }
        }
        // 从会话集合里删除这些键值，并行字典操作安全
        foreach (var item in keys)
        {
            _dic.Remove(item);
        }

        // 已经离开了锁，慢慢释放各个会话
        foreach (var item in values)
        {
            if (item is ILogFeature lf)
                lf.Log?.Info("超过{0}秒不活跃销毁 {1}", timeout, item);

            if (item is INetSession ss) ss.Close(nameof(RemoveNotAlive));
            //item.Dispose();
            item.TryDispose();
        }
    }

    private static Boolean IsNotAlive(IDeviceSession session, Int32 timeout) => session.LastTime > DateTime.MinValue && session.LastTime.AddSeconds(timeout) < DateTime.Now;
}
