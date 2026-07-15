using NewLife.Collections;
using NewLife.Data;
using NewLife.Log;
using NewLife.Messaging;
using NewLife.Model;
using NewLife.Net;
using NewLife.Remoting.Http;
using NewLife.Serialization;
using NewLife.Threading;
using System.Reflection;

namespace NewLife.Remoting;

/// <summary>应用接口服务器</summary>
public class ApiServer : ApiHost, IServer, IServiceProvider
{
    #region 属性
    /// <summary>是否正在工作</summary>
    public Boolean Active { get; private set; }

    /// <summary>端口</summary>
    public Int32 Port { get; set; }

    /// <summary>处理器</summary>
    public IApiHandler? Handler { get; set; }

    /// <summary>服务器</summary>
    public IApiServer Server { get; set; } = null!;

    /// <summary>连接复用。默认true，单个Tcp连接在处理某个请求未完成时，可以接收并处理新的请求</summary>
    public Boolean Multiplex { get; set; } = true;

    /// <summary>地址重用，主要应用于网络服务器重启交替。默认false</summary>
    /// <remarks>
    /// 一个端口释放后会等待两分钟之后才能再被使用，SO_REUSEADDR是让端口释放后立即就可以被再次使用。
    /// SO_REUSEADDR用于对TCP套接字处于TIME_WAIT状态下的socket(TCP连接中, 先调用close() 的一方会进入TIME_WAIT状态)，才可以重复绑定使用。
    /// 
    /// 如果启用，多进程可以共同监听一个端口，都能收到数据，星尘代理多进程监听5500端口测试通过。
    /// </remarks>
    public Boolean ReuseAddress { get; set; }

    ///// <summary>启用WebSocket。普通Tcp/Udp版服务端，默认支持Http请求，启用WebSocket后将不再支持Http请求，默认false</summary>
    //public Boolean UseWebSocket { get; set; }

    /// <summary>是否使用Http状态。默认false，使用json包装响应码</summary>
    public Boolean UseHttpStatus { get; set; }

    /// <summary>收到请求或响应时触发。优先于内部处理</summary>
    public event EventHandler<ApiReceivedEventArgs>? Received;

    /// <summary>服务提供者。创建控制器实例时使用，可实现依赖注入。务必在注册控制器之前设置该属性</summary>
    public IServiceProvider? ServiceProvider { get; set; }

    /// <summary>处理统计</summary>
    public ICounter? StatProcess { get; set; }
    #endregion

    #region 构造
    /// <summary>实例化一个应用接口服务器</summary>
    public ApiServer()
    {
        var type = GetType();
        Name = type.GetDisplayName() ?? type.Name.TrimSuffix("Server");

        Manager = new ApiManager(this);
    }

    /// <summary>使用指定端口实例化网络服务应用接口提供者</summary>
    /// <param name="port">端口</param>
    public ApiServer(Int32 port) : this() => Port = port;

    /// <summary>实例化</summary>
    /// <param name="uri">监听地址</param>
    public ApiServer(NetUri uri) : this() => Use(uri);

    /// <summary>销毁时停止服务</summary>
    /// <param name="disposing">是否由托管代码显示释放</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        _Timer.TryDispose();

        Stop(GetType().Name + (disposing ? "Dispose" : "GC"));

        Server.TryDispose();

        // ApiController可能注册在容器里面，这里需要解耦，避免当前ApiServer对象无法回收
        var controller = Manager?.Services.Values.FirstOrDefault(e => e.Type == typeof(ApiController))?.Controller as ApiController;
        if (controller != null) controller.Host = null;
    }
    #endregion

    #region 控制器管理
    /// <summary>接口动作管理器</summary>
    public IApiManager Manager { get; }

    /// <summary>注册服务提供类。该类的所有公开方法将直接暴露</summary>
    /// <typeparam name="TService">服务控制器类型</typeparam>
    public void Register<TService>() => Manager.Register<TService>();

    /// <summary>注册服务</summary>
    /// <param name="controller">控制器对象</param>
    /// <param name="method">动作名称。为空时遍历控制器所有公有成员方法</param>
    public void Register(Object controller, String? method) => Manager.Register(controller, method);

    /// <summary>显示可用服务</summary>
    protected virtual void ShowService()
    {
        var ms = Manager.Services;
        if (ms.Count > 0)
        {
            Log.Info("{0} 可用服务{1}个：", Name, ms.Count);
            var max = ms.Max(e => e.Key.Length);
            foreach (var item in ms)
            {
                Log.Info("\t{0,-" + (max + 1) + "}{1}", item.Key, item.Value);
            }
        }
    }
    #endregion

    #region 启动停止
    /// <summary>添加服务器</summary>
    /// <param name="uri">监听地址</param>
    /// <returns>创建的服务器实例，若初始化失败返回 null</returns>
    public IApiServer? Use(NetUri uri)
    {
        var svr = uri.Type == NetType.Http ? new ApiHttpServer() : new ApiNetServer();

        // 此时设置用处不大，因为可能是构造函数里调用Use，还没有指定Name等属性
        svr.Name = Name;
        svr.Log = Log;
        svr.SessionLog = Log;
        svr.Tracer = Tracer;
        svr.ReuseAddress = ReuseAddress;
        svr.ServiceProvider = this;

        if (!svr.Init(uri, this)) return null;

        Server = svr;

        return svr;
    }

    /// <summary>确保已创建服务器对象</summary>
    /// <returns>已存在的服务器或新建的 <see cref="ApiNetServer"/></returns>
    /// <exception cref="ArgumentNullException">未指定 <see cref="Server"/> 且 <see cref="Port"/> 未设置</exception>
    /// <remarks>支持 Port=0，此时由系统分配可用端口，启动后通过 <see cref="Port"/> 获取实际端口</remarks>
    public IApiServer EnsureCreate()
    {
        var svr = Server;
        if (svr != null) return svr;

        if (Port < 0) throw new ArgumentNullException(nameof(Server), "未指定服务器Server，且未指定端口Port！");

        var server = new ApiNetServer
        {
            Name = Name,
            Host = this,
            ServiceProvider = this,

            Log = Log,
            SessionLog = Log,
            Tracer = Tracer,
        };
        server.Init(new NetUri(NetType.Unknown, "*", Port), this);

        // 升级核心库以后不需要反射
        server.ReuseAddress = ReuseAddress;
        //server.SetValue("ReuseAddress", ReuseAddress);

        return Server = server;
    }

    /// <summary>开始服务</summary>
    /// <remarks>
    /// 初始化 <see cref="ApiHost.Encoder"/> 与 <see cref="Handler"/>，创建并启动底层 <see cref="IApiServer"/>。
    /// 若 <see cref="StatPeriod"/> 大于 0，启动统计定时器输出处理统计与网络状态。
    /// </remarks>
    public virtual void Start()
    {
        if (Active) return;

        // 注册默认服务控制器
        if (Manager.Find("Api/Info") == null)
            Register(new ApiController { Host = this }, null);

        var json = ServiceProvider?.GetService<IJsonHost>() ?? JsonHelper.Default;
        Encoder ??= new JsonEncoder { JsonHost = json };
        Handler ??= new ApiHandler { Host = this };

        Encoder.Log = EncoderLog;

        Log.Info("启动[{0}]", Name);
        Log.Info("编码：{0}", Encoder);
        Log.Info("处理：{0}", Handler);

        var svr = EnsureCreate();

        if (svr is ApiNetServer ns)
        {
            ns.Name = Name;
            ns.SessionLog = Log;
            ns.Tracer = Tracer;
            ns.ReuseAddress = ReuseAddress;
        }

        svr.Host = this;
        svr.Log = Log;
        svr.Start();

        // 如果是动态端口(Port=0)，从底层服务器回写实际端口
        if (Port == 0 && svr is NetServer netServer && netServer.Port > 0) Port = netServer.Port;

        ShowService();

        var ms = StatPeriod * 1000;
        if (ms > 0)
        {
            StatProcess ??= new PerfCounter();

            _Timer = new TimerX(DoStat, null, ms, ms) { Async = true };
        }

        Active = true;
    }

    /// <summary>停止服务</summary>
    /// <param name="reason">关闭原因。便于日志分析</param>
    /// <remarks>停止底层 <see cref="IApiServer"/> 并关闭统计定时器，将 <see cref="Active"/> 置为 false。</remarks>
    public virtual void Stop(String? reason)
    {
        if (!Active) return;

        _Timer.TryDispose();

        Log.Info("停止{0} {1}", GetType().Name, reason);
        Server.Stop(reason ?? (GetType().Name + "Stop"));

        Active = false;
    }
    #endregion

    #region 请求处理
    /// <summary>处理会话收到的消息，并返回结果消息</summary>
    /// <remarks>
    /// - 忽略 <c>Reply=true</c> 的消息；
    /// - 使用会话级或服务器级 <see cref="IEncoder"/> 解码 <see cref="IMessage"/> 为 <see cref="ApiMessage"/>；
    /// - 捕获执行异常并转换为错误码/消息（数据库异常脱敏）；
    /// - 单向请求（<c>OneWay=true</c>）不返回响应；
    /// - 在 finally 记录超过 <see cref="ApiHost.SlowTrace"/> 的慢处理日志（Action/Code/耗时ms）；
    /// - 若 <see cref="UseHttpStatus"/> 为 true，则使用 HTTP 状态码表达结果。
    /// 
    /// 注意：result 的缓冲区所有权会转移给返回的 response，由上层 using IMessage 级联释放。
    /// </remarks>
    /// <param name="session">网络会话</param>
    /// <param name="msg">消息</param>
    /// <param name="serviceProvider">当前作用域的服务提供者</param>
    /// <returns>要应答对方的消息，为空表示不应答</returns>
    public virtual IMessage? Process(IApiSession session, IMessage msg, IServiceProvider serviceProvider)
    {
        if (msg.Reply) return null;

        var enc = session["Encoder"] as IEncoder ?? Encoder;

        // 解码得到请求报文。不能 using request，因为 request.Data 是 msg.Payload 的切片，
        // 当处理器直接返回 IPacket（如 Echo）时，result 就是 request.Data 的同一引用，
        // CreateResponse 会将 result 直接挂载到 response.Payload 链上，
        // 若此处 using 提前释放 request.Data，response 发送时将引用已释放的缓冲区
        var request = enc.Decode(msg);
        if (request == null || request.Action.IsNullOrEmpty()) return null;

        // 动作名必须是 ASCII，跳过乱码
        if (!IsAscii(request.Action)) return null;

        using var span = Tracer?.NewSpan("rps:" + request.Action, request.Data);

        var code = 0;
        var st = StatProcess;
        var sw = st.StartCount();
        Object? result = null;
        IMessage? response = null;
        try
        {
            // 执行请求
            try
            {
                Received?.Invoke(this, new ApiReceivedEventArgs { Session = session, Message = msg, ApiMessage = request });

                result = OnProcess(session, request.Action, request.Data, msg, serviceProvider);

                // 流式返回：IAsyncEnumerable<T> 逐条推送
                // 注意：result.GetType() 返回编译器生成的具体类型（如 <Range>d__0），
                // 需通过接口检测而非具体类型
                if (result != null)
                {
                    var resultType = result.GetType();
                    if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>)
                        || resultType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>)))
                    {
                        ProcessStream(session, msg, request.Action, result, enc);
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                ex = ex.GetTrue();

                if (ShowError) WriteLog("{0}", ex);

                // 支持自定义错误码
                if (ex is ApiException aex)
                {
                    code = aex.Code;
                    result = ex.Message;
                }
                else
                {
                    code = 500;
                    // 数据库异常脱敏，避免泄漏 SQL 语句
                    result = ex.GetType().FullName == "XCode.Exceptions.XSqlException"
                        ? "数据库SQL错误"
                        : ex.Message;
                }

                span?.SetError(ex, request.Data?.ToStr());
            }

            // 单向请求无需响应，释放可能的 IOwnerPacket 归还 ArrayPool
            if (msg.OneWay)
            {
                result.TryDispose();
                result = null;
                return null;
            }

            if (enc is HttpEncoder httpEncoder) httpEncoder.UseHttpStatus = UseHttpStatus;

            // 编码响应，result 所有权转移到 response.Payload 链
            response = enc.CreateResponse(msg, request.Action, code, result);
            return response;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
        finally
        {
            var msCost = st.StopCount(sw) / 1000;
            if (SlowTrace > 0 && msCost >= SlowTrace)
                WriteLog($"慢处理[{request?.Action}]，Code={code}，耗时{msCost:n0}ms");

            // CreateResponse 异常时 result 未纳入响应，需要释放归还 ArrayPool
            if (result != null && response == null) result.TryDispose();
        }
    }

    /// <summary>执行消息处理，交给Handler</summary>
    /// <param name="session">会话</param>
    /// <param name="action">动作</param>
    /// <param name="args">参数</param>
    /// <param name="msg">消息</param>
    /// <param name="serviceProvider">当前作用域的服务提供者</param>
    /// <returns>处理结果；异常将由 <see cref="Process"/> 捕获并转换为错误码</returns>
    protected virtual Object? OnProcess(IApiSession session, String action, IPacket? args, IMessage msg, IServiceProvider serviceProvider) => Handler?.Execute(session, action, args, msg, serviceProvider);
    #endregion

    #region 广播
    /// <summary>广播消息给所有会话客户端</summary>
    /// <param name="action">服务操作</param>
    /// <param name="args">参数</param>
    /// <returns>实际广播成功的会话数</returns>
    public virtual Int32 InvokeAll(String action, Object? args = null)
    {
        var count = 0;
        foreach (var item in Server.AllSessions)
        {
            item.InvokeOneWay(action, args);

            count++;
        }

        return count;
    }
    #endregion

    #region 统计
    private TimerX? _Timer;
    private String? _Last;

    /// <summary>显示统计信息的周期。默认600秒，0表示不显示统计信息</summary>
    public Int32 StatPeriod { get; set; } = 600;

    private void DoStat(Object? state)
    {
        var sb = Pool.StringBuilder.Get();
        var pf2 = StatProcess;
        if (pf2 != null && pf2.Value > 0) sb.AppendFormat("处理：{0} ", pf2);

        if (Server is NetServer ns)
            sb.Append(ns.GetStat());

        var msg = sb.Return(true);
        //var msg = this.GetStat();
        if (msg.IsNullOrEmpty() || msg == _Last) return;
        _Last = msg;

        WriteLog(msg);
    }
    #endregion

    #region 流式处理
    /// <summary>处理流式调用。逐条迭代 IAsyncEnumerable，编码并发送每个数据块，最后发送结束标记</summary>
    /// <param name="session">会话</param>
    /// <param name="msg">原始请求消息</param>
    /// <param name="action">动作名</param>
    /// <param name="streamResult">IAsyncEnumerable 实例</param>
    /// <param name="enc">编码器</param>
    private void ProcessStream(IApiSession session, IMessage msg, String action, Object streamResult, IEncoder enc)
    {
        // 在线程池线程中同步等待异步流完成
        // Process 在以下两种上下文中被调用，均不会阻塞 UI 或造成死锁：
        //   1. OnReceive 投递到 ThreadPool.UnsafeQueueUserWorkItem（默认 Multiplex=true）
        //   2. OnReceive 直接调用（Multiplex=false，网络 IO 线程）
        // 网络 IO 线程虽非线程池线程，但 IAsyncEnumerable 流式迭代不含同步上下文捕获，
        // 因此 GetAwaiter().GetResult() 不会死锁。
        // 若将来 Process 改为 async 方法，应移除该同步包装，直接 await ProcessStreamAsync。

        // 创建取消令牌链接到会话生命周期。当客户端断开时，底层 Session 被释放，
        // ApiNetSession.Session.Disposed 变为 true，轮询检测到后取消令牌，
        // ProcessStreamAsync 的 await foreach 通过 WithCancellation 感知并停止枚举
        using var cts = new CancellationTokenSource();
        if (session is ApiNetSession netSession)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!netSession.Session.Disposed && !cts.IsCancellationRequested)
                        await Task.Delay(200, cts.Token).ConfigureAwait(false);
                    cts.Cancel();
                }
                catch (OperationCanceledException) { }
            });
        }

        try
        {
            ProcessStreamAsync(session, msg, action, streamResult, enc, cts.Token).GetAwaiter().GetResult();
        }
        finally
        {
            cts.Cancel();
        }
    }

    private async Task ProcessStreamAsync(IApiSession session, IMessage msg, String action, Object streamResult, IEncoder enc, CancellationToken cancellationToken = default)
    {
        try
        {
            // IAsyncEnumerable<out T> 是协变的，引用类型可转为 IAsyncEnumerable<Object?>
            if (streamResult is IAsyncEnumerable<Object?> refStream)
            {
#pragma warning disable CA2007 // 库内部线程池调用，无需 ConfigureAwait
                await foreach (var item in refStream.WithCancellation(cancellationToken))
                {
                    using var rs = enc.CreateStreamResponse(msg, action, 0, item, false);
                    session.SendMessage(rs);
                }
#pragma warning restore CA2007
            }
            else
            {
                // 值类型 IAsyncEnumerable（如 IAsyncEnumerable<Int32>），通过泛型 Helper 迭代
                var resultType = streamResult.GetType();
                var iface = resultType.GetInterfaces().FirstOrDefault(i =>
                    i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));
                if (iface == null)
                    throw new InvalidOperationException($"流式返回类型 {resultType.FullName} 未实现 IAsyncEnumerable<T>");

                var elementType = iface.GetGenericArguments()[0];
                var helperMethod = typeof(ApiServer).GetMethod(nameof(SendStreamItems), BindingFlags.NonPublic | BindingFlags.Static)
                    ?? throw new InvalidOperationException("找不到 SendStreamItems 方法");
                var genericHelper = helperMethod.MakeGenericMethod(elementType);
                var invokeTask = (Task)genericHelper.Invoke(null, [streamResult, session, msg, action, enc, cancellationToken])!;
                await invokeTask.ConfigureAwait(false);
            }

            // 发送结束标记
            using var endRs = enc.CreateStreamResponse(msg, action, 0, null, true);
            session.SendMessage(endRs);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // 非取消异常时发送错误帧并结束
            using var errRs = enc.CreateStreamResponse(msg, action, 500, ex.GetTrue().Message, true);
            try { session.SendMessage(errRs); } catch { }
        }
        catch (OperationCanceledException)
        {
            // 客户端断开导致的取消，静默结束
            WriteLog("流式调用已取消（客户端断开）");
        }
    }

    /// <summary>泛型 Helper：逐条迭代 IAsyncEnumerable&lt;T&gt; 并发送流式帧</summary>
    /// <typeparam name="T">元素类型（值类型或引用类型）</typeparam>
    /// <param name="streamObj">IAsyncEnumerable&lt;T&gt; 实例</param>
    /// <param name="session">会话</param>
    /// <param name="msg">原始请求消息</param>
    /// <param name="action">动作名</param>
    /// <param name="enc">编码器</param>
    /// <param name="cancellationToken">取消令牌</param>
    private static async Task SendStreamItems<T>(Object streamObj, IApiSession session, IMessage msg, String action, IEncoder enc, CancellationToken cancellationToken = default)
    {
        var stream = (IAsyncEnumerable<T>)streamObj;
#pragma warning disable CA2007
        await foreach (var item in stream.WithCancellation(cancellationToken))
        {
            using var rs = enc.CreateStreamResponse(msg, action, 0, item, false);
            session.SendMessage(rs);
        }
#pragma warning restore CA2007
    }
    #endregion

    #region 辅助
    Object IServiceProvider.GetService(Type serviceType)
    {
        if (serviceType == typeof(ApiServer)) return this;
        if (serviceType == typeof(IApiManager)) return Manager;

        return ServiceProvider?.GetService(serviceType)!;
    }

    private static Boolean IsAscii(String str)
    {
        for (var i = 0; i < str.Length; i++)
        {
            if (str[i] >= 127) return false;
        }
        return true;
    }
    #endregion
}