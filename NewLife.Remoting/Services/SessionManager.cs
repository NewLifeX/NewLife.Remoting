using NewLife.Caching;
using NewLife.Log;
using NewLife.Messaging;
using NewLife.Model;
using NewLife.Remoting.Models;
using NewLife.Serialization;
#if !NET45
using TaskEx=System.Threading.Tasks.Task;
#endif

namespace NewLife.Remoting.Services;

/// <summary>命令会话</summary>
public interface IDeviceSession : IDisposable
{
    void Start(CancellationTokenSource source);
}

class DeviceEventContext(IEventBus<String> bus, String code) : EventContext<String>(bus)
{
    /// <summary>设备编码</summary>
    public String Code { get; set; } = code;
}

/// <summary>会话管理器</summary>
public class SessionManager : DisposeBase
{
    /// <summary>主题</summary>
    public String Topic { get; set; } = "Commands";

    /// <summary>事件总线</summary>
    public EventBus<String> Bus { get; set; } = new EventBus<String>();

    private readonly ITracer _tracer;
    private readonly ILog _log;
    ICache? _cache;
    IProducerConsumer<String>? _queue;
    IProducerConsumer<String>? _queue2;
    CancellationTokenSource? _source;

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

        _source?.TryDispose();
    }

    /// <summary>创建会话</summary>
    /// <param name="code"></param>
    /// <returns></returns>
    public IDeviceSession Create(String code)
    {
        var session = new DeviceSession
        {
            Code = code
        };

        Bus.Subscribe(session, code);
        session.OnDisposed += (s, e) => Bus.Unsubscribe(code);

        // 如果有缓存提供者，则使用缓存提供者的队列，否则直接进入事件总线
        if (_queue2 == null)
        {
            _queue2 ??= _cache?.GetQueue<String>(Topic);
            if (_queue2 != null)
            {
                _source = new CancellationTokenSource();

                _ = Task.Run(() => ConsumeMessage(_queue2, _source));
            }
        }

        return session;
    }

    /// <summary>向设备发送消息</summary>
    /// <param name="code"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public Task PublishAsync(String code, String message)
    {
        // 如果有缓存提供者，则使用缓存提供者的队列，否则直接进入事件总线
        _queue ??= _cache?.GetQueue<String>(Topic);

        if (_queue != null)
        {
            _queue.Add($"{code}#{message}");

            return TaskEx.CompletedTask;
        }

        return Bus.PublishAsync(message, new DeviceEventContext(Bus, code));
    }

    /// <summary>从队列中消费消息，经事件总线送给设备会话</summary>
    /// <param name="queue"></param>
    /// <param name="source"></param>
    /// <returns></returns>
    private async Task ConsumeMessage(IProducerConsumer<String> queue, CancellationTokenSource source)
    {
        DefaultSpan.Current = null;
        var cancellationToken = source.Token;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ISpan? span = null;
                var mqMsg = await queue.TakeOneAsync(15, cancellationToken).ConfigureAwait(false);
                if (mqMsg != null)
                {
                    // 埋点
                    span = _tracer?.NewSpan($"mq:Command", mqMsg);

                    var code = "";
                    var p = mqMsg.IndexOf('#');
                    if (p > 0 && p < 32)
                    {
                        code = mqMsg[..p];
                        mqMsg = mqMsg[(p + 1)..];
                    }

                    // 解码
                    var dic = JsonParser.Decode(mqMsg)!;
                    var msg = JsonHelper.Convert<CommandModel>(dic);
                    span?.Detach(dic);

                    // 修正时间
                    if (msg != null)
                    {
                        if (msg.StartTime.Year < 2000) msg.StartTime = DateTime.MinValue;
                        if (msg.Expire.Year < 2000) msg.Expire = DateTime.MinValue;
                    }

                    // 发布到事件总线，交由设备会话处理
                    if (msg != null && (msg.Expire.Year <= 2000 || msg.Expire >= DateTime.Now))
                    {
                        await Bus.PublishAsync(mqMsg, new DeviceEventContext(Bus, code)).ConfigureAwait(false);
                    }

                    span?.Dispose();
                }
                else
                {
                    await Task.Delay(1_000, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (TaskCanceledException) { }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log?.Error(ex.ToString());
        }
        finally
        {
            source.Cancel();
        }
    }
}
