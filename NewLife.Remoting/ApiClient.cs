using NewLife.Collections;
using NewLife.Data;
using NewLife.Log;
using NewLife.Messaging;
using NewLife.Model;
using NewLife.Net;
using NewLife.Reflection;
using NewLife.Remoting.Http;
using NewLife.Serialization;
using NewLife.Threading;
#if !NET40
using TaskEx = System.Threading.Tasks.Task;
#endif

namespace NewLife.Remoting;

/// <summary>应用接口客户端</summary>
/// <remarks>
/// 保持到服务端直接的长连接RPC通信。
/// 常用于向服务端发送请求并接收应答，也可以接收服务端主动发送的单向消息。
/// 
/// 文档 https://newlifex.com/core/srmp
/// </remarks>
public class ApiClient : ApiHost, IApiClient
{
    #region 属性
    /// <summary>是否已打开</summary>
    public Boolean Active { get; protected set; }

    /// <summary>服务端地址集合。负载均衡</summary>
    public String[]? Servers { get; set; }

    /// <summary>本地地址。在本地有多个IP时，可以指定使用哪一个IP地址</summary>
    public NetUri? Local { get; set; }

    /// <summary>客户端连接集群</summary>
    public ICluster<String, ISocketClient>? Cluster { get; set; }

    /// <summary>是否使用连接池。true时建立多个到服务端的连接（高吞吐），默认false使用单一连接（低延迟）</summary>
    public Boolean UsePool { get; set; }

    /// <summary>令牌。每次请求携带</summary>
    public String? Token { get; set; }

    /// <summary>最后活跃时间</summary>
    public DateTime LastActive { get; set; }

    /// <summary>收到请求或响应时</summary>
    public event EventHandler<ApiReceivedEventArgs>? Received;

    /// <summary>Json序列化主机</summary>
    public IJsonHost? JsonHost { get; set; }

    /// <summary>服务提供者。创建控制器实例时使用，可实现依赖注入。务必在注册控制器之前设置该属性</summary>
    public IServiceProvider? ServiceProvider { get; set; }

    /// <summary>调用统计</summary>
    public ICounter? StatInvoke { get; set; }

    /// <summary>重试策略（可选）。默认 null 表示不启用重试</summary>
    public IRetryPolicy? RetryPolicy { get; set; }

    /// <summary>最大重试次数（不含首次尝试）。默认 0，不重试</summary>
    public Int32 MaxRetries { get; set; }
    #endregion

    #region 构造
    /// <summary>实例化应用接口客户端</summary>
    public ApiClient()
    {
        var type = GetType();
        Name = type.GetDisplayName() ?? type.Name.TrimEnd("Client");
    }

    /// <summary>实例化应用接口客户端</summary>
    /// <param name="uris">服务端地址集合，逗号分隔</param>
    public ApiClient(String uris) : this() => SetServer(uris);

    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        _Timer.TryDispose();

        Close(Name + (disposing ? "Dispose" : "GC"));
    }
    #endregion

    #region 打开关闭
    /// <summary>打开客户端</summary>
    /// <remarks>校验 `Servers` 并初始化 `Encoder`/`Cluster`。首次开启统计定时器。</remarks>
    /// <returns>已处于打开状态或本次打开返回 true</returns>
    public virtual Boolean Open()
    {
        if (Active) return true;
        lock (this)
        {
            if (Active) return true;

            var ss = Servers;
            if (ss == null || ss.Length == 0) throw new ArgumentNullException(nameof(Servers), "未指定服务端地址");

            JsonHost ??= ServiceProvider?.GetService<IJsonHost>() ?? JsonHelper.Default;
            Encoder ??= new JsonEncoder { JsonHost = JsonHost };

            // 集群
            Cluster = InitCluster();
            WriteLog("集群模式：{0}", Cluster?.GetType().GetDisplayName() ?? Cluster?.ToString());

            Encoder.Log = EncoderLog;

            // 控制性能统计信息
            var ms = StatPeriod * 1000;
            if (ms > 0)
            {
                _Timer = new TimerX(DoWork, null, ms, ms) { Async = true };
            }

            return Active = true;
        }
    }

    /// <summary>关闭</summary>
    /// <param name="reason">关闭原因。便于日志分析</param>
    /// <remarks>会调用 `Cluster.Close(reason)` 并将 `Active=false`。</remarks>
    public virtual void Close(String reason)
    {
        if (!Active) return;

        Cluster?.Close(reason ?? (GetType().Name + "Close"));

        Active = false;
    }

    private String? _lastUrls;
    /// <summary>设置服务端地址。如果新地址跟旧地址不同，将会替换旧地址构造的Servers</summary>
    /// <param name="uris">服务端地址集合字符串，逗号/分号分隔</param>
    /// <remarks>如果已存在集群，调用 `Cluster.Reset()` 以便下次获取连接时生效，尽量平滑切换。</remarks>
    public void SetServer(String uris)
    {
        if (!uris.IsNullOrEmpty() && uris != _lastUrls)
        {
            Servers = uris.Split(",", ";");
            _lastUrls = uris;

            Cluster?.Reset();
        }
    }

    /// <summary>初始化集群</summary>
    /// <remarks>根据 `UsePool` 决定单连接或连接池；为 `OnCreate` 提供连接工厂。</remarks>
    /// <returns>创建或复用的集群实例</returns>
    protected virtual ICluster<String, ISocketClient> InitCluster()
    {
        var cluster = Cluster;
        cluster ??= UsePool ?
                new ClientPoolCluster<ISocketClient> { Name = Name, Log = Log } :
                new ClientSingleCluster<ISocketClient> { Name = Name, Log = Log };

        if (cluster is ClientSingleCluster<ISocketClient> sc && sc.OnCreate == null) sc.OnCreate = OnCreate;
        if (cluster is ClientPoolCluster<ISocketClient> pc && pc.OnCreate == null) pc.OnCreate = OnCreate;

        cluster.GetItems ??= () => Servers ?? [];
        cluster.Open();

        return cluster;
    }
    #endregion

    #region 远程调用
    /// <summary>异步调用，等待返回结果</summary>
    /// <typeparam name="TResult">返回类型</typeparam>
    /// <param name="action">服务操作</param>
    /// <param name="args">参数</param>
    /// <param name="cancellationToken">取消通知</param>
    /// <remarks>
    /// - 自动确保已 <see cref="Open"/>。
    /// - 发送失败若返回 401（Unauthorized），将调用 <see cref="OnLoginAsync"/> 进行一次登录后重发（同一连接）。
    /// - 若配置了 <see cref="RetryPolicy"/> 且 <see cref="MaxRetries"/> &gt; 0，则对非 401 的异常按策略进行可选重试（默认不重试）。
    /// - 超时会将 <see cref="TaskCanceledException"/> 截断为短消息："[action]超时[Timeout]取消"。
    /// </remarks>
    /// <returns>解码后的返回值，或默认值</returns>
    public virtual async Task<TResult?> InvokeAsync<TResult>(String action, Object? args = null, CancellationToken cancellationToken = default)
    {
        // 让上层异步到这直接返回，后续代码在另一个线程执行
        //!!! Task.Yield会导致强制捕获上下文，虽然会在另一个线程执行，但在UI线程中可能无法抢占上下文导致死锁
        //await Task.Yield();

        Open();

        if (Cluster == null) throw new ArgumentNullException(nameof(Cluster));

        var attempts = 0;
        while (true)
        {
            ISocketClient? client = null;
            try
            {
                client = Cluster.Get();

                try
                {
                    return await InvokeWithClientAsync<TResult>(client, action, args, 0, cancellationToken).ConfigureAwait(false);
                }
                catch (ApiException ex)
                {
                    // 这个连接没有鉴权，重新登录后再次调用（不计入重试次数）
                    if (ex.Code == ApiCode.Unauthorized)
                    {
                        await OnLoginAsync(client, true, cancellationToken).ConfigureAwait(false);

                        return await InvokeWithClientAsync<TResult>(client, action, args, 0, cancellationToken).ConfigureAwait(false);
                    }

                    throw;
                }
            }
            catch (Exception ex)
            {
                // 针对 TaskCanceledException 输出短消息（除非策略决定重试）
                if (RetryPolicy != null && attempts < MaxRetries && RetryPolicy.ShouldRetry(ex, attempts + 1, out var delay, out var refreshClient))
                {
                    attempts++;
                    // 需要更换连接时，归还当前（如有）
                    // 注意：这里 client 可能为 null（Get() 之前失败）
                    if (refreshClient && client != null)
                    {
                        Cluster.Return(client);
                        client = null;
                    }
                    if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (ex is TaskCanceledException)
                    throw new TaskCanceledException($"[{action}]超时[{Timeout:n0}ms]取消，Server={client?.Remote}");

                throw;
            }
            finally
            {
                if (client != null) Cluster.Return(client);
            }
        }
    }

    /// <summary>同步调用，阻塞等待</summary>
    /// <param name="action">服务操作</param>
    /// <param name="args">参数</param>
    /// <returns>解码后的返回值，或默认值</returns>
    public virtual TResult? Invoke<TResult>(String action, Object? args = null)
    {
        using var source = new CancellationTokenSource(Timeout);
        return InvokeAsync<TResult>(action, args, source.Token).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    /// <summary>单向发送。同步调用，不等待返回</summary>
    /// <param name="action">服务操作</param>
    /// <param name="args">参数</param>
    /// <param name="flag">标识</param>
    /// <remarks>设置消息 <c>OneWay=true</c>，不等待服务端应答，仅返回底层发送结果码。</remarks>
    /// <returns>底层发送结果码，负数表示失败</returns>
    public virtual Int32 InvokeOneWay(String action, Object? args = null, Byte flag = 0)
    {
        if (!Open()) return -1;
        if (Cluster == null) throw new ArgumentNullException(nameof(Cluster));

        return Cluster.Invoke(client => InvokeWithClient(client, action, args, flag));
    }

    /// <summary>指定客户端的异步调用，等待返回结果</summary>
    /// <remarks>
    /// 常用于在 <see cref="OnLoginAsync"/> 中实现连接后登录功能。
    /// 将在 finally 记录超过 <see cref="ApiHost.SlowTrace"/> 的慢调用日志（包含 Action/消息/耗时ms）。
    /// </remarks>
    /// <typeparam name="TResult">返回类型</typeparam>
    /// <param name="client">客户端</param>
    /// <param name="action">服务操作</param>
    /// <param name="args">参数</param>
    /// <param name="flag">标识</param>
    /// <param name="cancellationToken">取消通知</param>
    /// <returns>解码后的返回值，或默认值</returns>
    public virtual async Task<TResult?> InvokeWithClientAsync<TResult>(ISocketClient client, String action, Object? args = null, Byte flag = 0, CancellationToken cancellationToken = default)
    {
        // 性能计数器，次数、TPS、平均耗时
        var st = StatInvoke;
        var sw = st.StartCount();

        LastActive = DateTime.Now;

        // 令牌
        if (!Token.IsNullOrEmpty() && args != null)
        {
            var dic = args.ToDictionary();
            if (!dic.ContainsKey("Token")) dic["Token"] = Token;
            args = dic;
        }

        // 埋点，注入traceParent到参数集合
        var span = Tracer?.NewSpan("rpc:" + action, args);
        if (args != null && span != null) args = span.Attach(args);

        // 编码请求，构造消息
        var enc = Encoder;
        using var msg = enc.CreateRequest(action, args);
        if (flag > 0 && msg is DefaultMessage dm) dm.Flag = flag;

        var invoker = client.Remote + "";
        IMessage? rs = null;
        try
        {
            // 发起异步请求，等待返回
            rs = (await client.SendMessageAsync(msg, cancellationToken).ConfigureAwait(false)) as IMessage;

            if (rs == null) return default;
        }
        catch (AggregateException aggex)
        {
            var ex = aggex.GetTrue();

            // 跟踪异常
            span?.SetError(ex, args);

            if (ex is TaskCanceledException)
            {
                if (Log != null && Log.Enable && Log.Level <= LogLevel.Debug)
                    throw new TimeoutException($"请求[{action}]超时({msg})！", ex);
                else
                    throw new TimeoutException($"请求[{action}]超时({msg})！");
            }
            throw;
        }
        catch (TaskCanceledException ex)
        {
            span?.SetError(ex, args);
            if (Log != null && Log.Enable && Log.Level <= LogLevel.Debug)
                throw new TimeoutException($"请求[{action}]超时({msg})！", ex);
            else
                throw new TimeoutException($"请求[{action}]超时({msg})！");
        }
        catch (Exception ex)
        {
            span?.SetError(ex, args);
            throw;
        }
        finally
        {
            var msCost = st.StopCount(sw) / 1000;
            // 慢调用日志：包含 Action、请求消息（部分重要标识）、耗时ms。用于定位网络/服务端慢点。
            if (SlowTrace > 0 && msCost >= SlowTrace) WriteLog($"慢调用[{action}]({msg})，耗时{msCost:n0}ms");

            span?.Dispose();
        }

        // 特殊返回类型
        var resultType = typeof(TResult);
        if (resultType == typeof(IMessage)) return (TResult)rs;
        //if (resultType == typeof(Packet)) return rs.Payload;
        if (rs.Payload == null) return default;

        try
        {
            // 解码响应得到SRMP报文
            var message = enc.Decode(rs) ?? throw new InvalidOperationException();

            // 是否成功
            if (message.Code is not ApiCode.Ok and not ApiCode.Ok200)
                throw new ApiException(message.Code, message.Data?.ToStr().Trim('\"') ?? "") { Source = invoker + "/" + action };

            if (message.Data == null) return default;
            if (resultType == typeof(IPacket)) return (TResult)(Object)message.Data;
#pragma warning disable CS0618 // 类型或成员已过时
            if (resultType == typeof(Packet))
            {
                if (message.Data is Packet) return (TResult)(Object)message.Data;
                if (message.Data.TryGetArray(out var segment)) return (TResult)(Object)new Packet(segment);

                return (TResult)(Object)new Packet(message.Data.ToArray());
            }
#pragma warning restore CS0618 // 类型或成员已过时

            // 二进制序列化
            if (resultType.As<IAccessor>())
            {
                var result = ServiceProvider?.GetService(resultType) ?? resultType.CreateInstance();
                if (result is IAccessor accessor && accessor.Read(message.Data.GetStream(), message))
                    return (TResult)result;
            }

            try
            {
                // 解码结果
                var result = enc.DecodeResult(action, message.Data, rs, resultType);
                if (result == null) return default;
                if (resultType == typeof(Object)) return (TResult)result;

                // 返回
                //return (TResult?)enc.Convert(result, resultType);
                return (TResult?)result;
            }
            finally
            {
                message.TryDispose();
            }
        }
        finally
        {
            rs.Payload.TryDispose();
        }
    }

    /// <summary>单向调用，不等待返回</summary>
    /// <param name="client">客户端</param>
    /// <param name="action">服务操作</param>
    /// <param name="args">参数</param>
    /// <param name="flag">标识</param>
    /// <returns>底层发送结果码，负数表示失败</returns>
    public Int32 InvokeWithClient(ISocketClient client, String action, Object? args, Byte flag = 0)
    {
        if (client == null) return -1;

        // 性能计数器，次数、TPS、平均耗时
        var st = StatInvoke;
        var sw = st.StartCount();

        LastActive = DateTime.Now;

        // 令牌
        if (!Token.IsNullOrEmpty() && args != null)
        {
            var dic = args.ToDictionary();
            if (!dic.ContainsKey("Token")) dic["Token"] = Token;
            args = dic;
        }

        using var span = Tracer?.NewSpan("rpc:" + action, args);
        if (args != null && span != null) args = span.Attach(args);

        // 编码请求
        using var msg = Encoder.CreateRequest(action, args);
        if (msg is DefaultMessage dm)
        {
            dm.OneWay = true;
            if (flag > 0) dm.Flag = flag;
        }

        try
        {
            return client.SendMessage(msg);
        }
        catch (Exception ex)
        {
            // 跟踪异常
            span?.SetError(ex, args);

            throw;
        }
        finally
        {
            var msCost = st.StopCount(sw) / 1000;
            // 慢调用日志：包含 Action 和耗时ms（单向调用不涉及解码过程）。
            if (SlowTrace > 0 && msCost >= SlowTrace) WriteLog($"慢调用[{action}]，耗时{msCost:n0}ms");
        }
    }
    #endregion

    #region 异步接收
    /// <summary>客户端收到服务端主动下发消息</summary>
    /// <param name="message">原始消息</param>
    /// <param name="e">底层接收事件参数</param>
    protected virtual void OnReceive(IMessage message, ApiReceivedEventArgs e) => Received?.Invoke(this, e);

    private void Client_Received(Object? sender, ReceivedEventArgs e)
    {
        LastActive = DateTime.Now;

        // Api解码消息得到Action和参数
        if (e.Message is not IMessage msg) return;

        using var apiMessage = Encoder.Decode(msg);
        var e2 = new ApiReceivedEventArgs
        {
            Remote = sender as ISocketRemote,
            Message = msg,
            ApiMessage = apiMessage,
            UserState = e,
        };

        OnReceive(msg, e2);
    }
    #endregion

    #region 登录
    /// <summary>新会话。客户端每次连接或断线重连后，触发自动登录（异步，不阻塞网络线程）</summary>
    /// <param name="client">会话</param>
    /// <remarks>登录失败会记录错误日志，不影响网络线程继续工作。</remarks>
    public virtual void OnNewSession(ISocketClient client)
    {
        // Fire & forget，避免同步阻塞导致线程池/IO 线程被占用。异常写日志。
        try
        {
            var task = OnLoginAsync(client, true);
            if (task != null && !task.IsCompleted)
            {
                task.ContinueWith(t =>
                {
                    if (t.Exception != null) Log?.Error("[{0}]自动登录失败：{1}", Name, t.Exception.GetTrue().Message);
                }, TaskScheduler.Default);
            }
        }
        catch (Exception ex)
        {
            Log?.Error("[{0}]自动登录触发失败：{1}", Name, ex.Message);
        }
    }

    /// <summary>连接后自动登录</summary>
    /// <param name="client">客户端</param>
    /// <param name="force">强制登录</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实现方可返回登录结果，否则返回 null</returns>
    protected virtual Task<Object?> OnLoginAsync(ISocketClient client, Boolean force, CancellationToken cancellationToken = default) => TaskEx.FromResult<Object?>(null);

    /// <summary>登录</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实现方可返回登录结果，否则返回 null</returns>
    public virtual async Task<Object?> LoginAsync(CancellationToken cancellationToken = default)
    {
        if (Cluster == null) throw new ArgumentNullException(nameof(Cluster));

        return await Cluster.InvokeAsync(client => OnLoginAsync(client, false, cancellationToken)).ConfigureAwait(false);
    }
    #endregion

    #region 连接池
    /// <summary>创建客户端之后，打开连接之前</summary>
    /// <param name="svr">目标服务端地址</param>
    /// <remarks>
    /// - WebSocket：清空默认管线并注入 `WebSocketClientCodec`。
    /// - 超时：网络层采用消息层的 <see cref="ApiHost.Timeout"/>。
    /// - Trace：按日志级别为 Debug 时开启底层 Socket Trace（详见下方注释）。
    /// </remarks>
    /// <returns>新建并已打开的 <see cref="ISocketClient"/></returns>
    protected virtual ISocketClient OnCreate(String svr)
    {
        var uri = new NetUri(svr);
        var client = uri.Type == NetType.WebSocket ?
            new Uri(svr).CreateRemote() :
            uri.CreateRemote();

        if (uri.Type == NetType.WebSocket)
        {
            client.Pipeline?.Clear();
            client.Add(new WebSocketClientCodec { UserPacket = true });
        }

        // 网络层采用消息层超时
        client.Timeout = Timeout;
        // 仅在日志级别为 Debug 时开启底层 Socket 的 Trace，以减少常规运行期间的埋点数据量和性能开销；
        // 需要详细排障时，将日志提升到 Debug 即可自动打开底层 Trace，无需单独的开关。
        client.Tracer = (Log != null && Log.Level <= LogLevel.Debug || SocketLog != null && SocketLog.Level <= LogLevel.Debug) ? Tracer : null;
        client.Log = SocketLog!;

        if (Local != null) client.Local = Local;

        client.Add(GetMessageCodec());

        client.Opened += (s, e) => OnNewSession((s as ISocketClient)!);
        client.Received += Client_Received;

        client.Open();

        return client;
    }
    #endregion

    #region 统计
    private TimerX? _Timer;
    private String? _Last;

    /// <summary>显示统计信息的周期。默认600秒，0表示不显示统计信息</summary>
    public Int32 StatPeriod { get; set; } = 600;

    private void DoWork(Object? state)
    {
        var sb = Pool.StringBuilder.Get();
        var pf1 = StatInvoke;
        if (pf1 != null && pf1.Value > 0) sb.AppendFormat("请求：{0} ", pf1);

        var msg = sb.Return(true);
        if (msg.IsNullOrEmpty() || msg == _Last) return;
        _Last = msg;

        WriteLog(msg);
    }
    #endregion

    #region 日志
    /// <summary>Socket层日志</summary>
    public ILog SocketLog { get; set; } = Logger.Null;
    #endregion
}