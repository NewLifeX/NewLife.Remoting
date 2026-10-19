using NewLife.Collections;
using NewLife.Data;
using NewLife.Log;
using NewLife.Messaging;
using NewLife.Model;
using NewLife.Net;
using NewLife.Remoting.Http;
using NewLife.Serialization;
using NewLife.Threading;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
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

    /// <summary>Http服务端口。默认-1表示不启用Http服务；0表示自动选择（RPC 端口+1，被占用时回退系统随机端口）；大于0表示指定端口</summary>
    /// <remarks>
    /// 拆端口设计：SRMP 独占 <see cref="Port"/>，HTTP 使用独立端口，由 NewLife.Core 的 HttpServer 承载。
    /// 启用后通过 <see cref="HttpPort"/> 可读取实际端口（0 与自动端口场景）。
    /// </remarks>
    public Int32 HttpPort { get; set; } = -1;

    /// <summary>Http服务器。承载 HTTP 的独立端口，仅当 <see cref="HttpPort"/> 不小于0时创建</summary>
    public IApiServer? HttpServer { get; private set; }

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
        HttpServer.TryDispose();

        // ApiController可能注册在容器里面，这里需要解耦，避免当前ApiServer对象无法回收
        var controller = Manager?.Services.Values.FirstOrDefault(e => e.Type == typeof(ApiController))?.Controller as ApiController;
        if (controller != null) controller.Host = null;
    }
    #endregion

    #region 控制器管理
    /// <summary>接口动作管理器</summary>
    public IApiManager Manager { get; }

    /// <summary>注册服务提供类。该类的所有公开方法将直接暴露</summary>
    /// <typeparam name="TService">服务控制器类型。AOT 与裁剪下，该类型的公开/非公开方法与公开构造将按需保留</typeparam>
    public void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] TService>() => Manager.Register<TService>();

    /// <summary>注册服务</summary>
    /// <param name="controller">控制器对象</param>
    /// <param name="method">动作名称。为空时遍历控制器所有公有成员方法</param>
    /// <remarks>运行时类型入口，裁剪与 AOT 下无法静态保证动作方法被保留，请优先使用泛型入口 <see cref="Register{TService}()"/></remarks>
    [RequiresUnreferencedCode("控制器类型在运行时确定，裁剪与 AOT 下无法静态保留其动作方法，请改用泛型入口 Register<TService>()")]
    public void Register(Object controller, String? method) => Manager.Register(controller, method);

    /// <summary>注册内置诊断控制器。类型在编译期已知，其公开方法由 <see cref="ApiController"/> 的类型级动态依赖保留</summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "ApiController 为本程序集内置类型，其公开方法已由类型级 DynamicDependency 保留")]
    private void RegisterDefaultController() => Register(new ApiController { Host = this }, null);

    /// <summary>显示可用服务</summary>
    protected virtual void ShowService()
    {
        var ms = Manager.Services;
        if (ms.Count > 0)
        {
            Log.Info("{0} has {1} available services:", Name, ms.Count);
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
        IApiServer? svr = null;
        if (uri.Type == NetType.Http)
        {
            // Http专用模式：主服务器即 HTTP 入口，基于 Core 的 HttpServer 承载
            var http = new ApiHttpServer
            {
                // 此时设置用处不大，因为可能是构造函数里调用Use，还没有指定Name等属性
                Name = Name,
                Log = Log,
                Tracer = Tracer,
                ReuseAddress = ReuseAddress,
                ServiceProvider = this,
            };

            if (http.Init(uri, this)) svr = http;
        }
        else
        {
            var net = new ApiNetServer
            {
                Name = Name,
                Log = Log,
                SessionLog = Log,
                Tracer = Tracer,
                ReuseAddress = ReuseAddress,
                ServiceProvider = this,
            };

            if (net.Init(uri, this)) svr = net;
        }

        if (svr == null) return null;

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

        if (Port < 0) throw new ArgumentNullException(nameof(Server), L.T("Neither Server nor Port is specified!"));

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
            RegisterDefaultController();

        var json = ServiceProvider?.GetService<IJsonHost>() ?? JsonHelper.Default;
        Encoder ??= new JsonEncoder { JsonHost = json };
        Handler ??= new ApiHandler { Host = this };

        Encoder.Log = EncoderLog;

        Log.Info("Starting [{0}]", Name);
        Log.Info("Encoder: {0}", Encoder);
        Log.Info("Handler: {0}", Handler);

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
        if (Port == 0)
        {
            if (svr is NetServer netServer && netServer.Port > 0)
                Port = netServer.Port;
            else if (svr is ApiHttpServer httpServer && httpServer.Port > 0)
                Port = httpServer.Port;
        }

        // 拆端口设计：主服务器提供 SRMP，HTTP 在独立端口由 Core 的 HttpServer 提供
        EnsureHttpServer();

        ShowService();

        var ms = StatPeriod * 1000;
        if (ms > 0)
        {
            StatProcess ??= new PerfCounter();

            _Timer = new TimerX(DoStat, null, ms, ms) { Async = true };
        }

        Active = true;
    }

    /// <summary>创建并启动Http服务器</summary>
    /// <remarks>
    /// 拆端口设计下，主服务器只提供 SRMP，HTTP 使用独立端口（默认关闭，需显式设置 <see cref="HttpPort"/>）。
    /// HTTP 入口基于 NewLife.Core 的 HttpServer，不再自研帧栈，也不再承载 WebSocket。
    /// </remarks>
    /// <returns>Http服务器实例；未启用时返回 null</returns>
    protected virtual IApiServer? EnsureHttpServer()
    {
        // 已创建过：Stop 后重新 Start 时把实例重新拉起来，否则 HTTP 端口会静默失效
        if (HttpServer is ApiHttpServer hs)
        {
            if (!hs.Active) hs.Start();

            return hs;
        }
        if (HttpServer != null) return HttpServer;
        if (HttpPort < 0) return null;

        // 主服务器就是 Http 服务器（Http 专用模式）时无需第二个端口
        if (Server is ApiHttpServer) return null;

        var port = HttpPort;
        if (port == 0) port = Port > 0 ? Port + 1 : 0;

        var svr = CreateHttpServer(port);
        if (svr == null) return null;

        try
        {
            svr.Start();
        }
        catch (Exception ex) when (port > 0 && HttpPort == 0)
        {
            // 自动端口被占用时退回系统随机端口
            Log.Warn("HTTP port [{0}] failed to start, falling back to a random port: {1}", port, ex.Message);

            svr.TryDispose();

            svr = CreateHttpServer(0) ?? throw new InvalidOperationException(L.T("Cannot create HTTP server on port [{0}]", port));
            svr.Start();
        }

        HttpServer = svr;
        HttpPort = svr.Port;

        Log.Info("HTTP service port: {0}", HttpPort);

        return svr;
    }

    private ApiHttpServer? CreateHttpServer(Int32 port)
    {
        var svr = new ApiHttpServer
        {
            Name = Name,
            Host = this,
            ServiceProvider = this,

            Log = Log,
            Tracer = Tracer,
            ReuseAddress = ReuseAddress,
        };

        return svr.Init(new NetUri(NetType.Http, "*", port), this) ? svr : null;
    }

    /// <summary>停止服务</summary>
    /// <param name="reason">关闭原因。便于日志分析</param>
    /// <remarks>
    /// 停止底层 <see cref="IApiServer"/> 与 <see cref="HttpServer"/>，关闭统计定时器，将 <see cref="Active"/> 置为 false。
    /// 服务器对象本身保留，重新 <see cref="Start"/> 时会重新拉起监听。
    /// </remarks>
    public virtual void Stop(String? reason)
    {
        if (!Active) return;

        _Timer.TryDispose();

        Log.Info("Stopping {0} {1}", GetType().Name, reason);
        Server.Stop(reason ?? (GetType().Name + "Stop"));
        HttpServer?.Stop(reason ?? (GetType().Name + "Stop"));

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
        => ProcessAsync(session, msg, serviceProvider).ConfigureAwait(false).GetAwaiter().GetResult();

    /// <summary>异步处理会话收到的消息，并返回结果消息。不阻塞调用线程</summary>
    /// <remarks>
    /// 与 <see cref="Process"/> 逻辑一致，但异步等待处理器结果，避免 async 动作阻塞线程池线程。
    /// - 忽略 <c>Reply=true</c> 的消息；
    /// - 使用会话级或服务器级 <see cref="IEncoder"/> 解码 <see cref="IMessage"/> 为 <see cref="ApiMessage"/>；
    /// - 捕获执行异常并转换为错误码/消息（数据库异常脱敏）；
    /// - 单向请求（<c>OneWay=true</c>）不返回响应；
    /// - 流式返回（IAsyncEnumerable&lt;T&gt;）逐条推送后返回 null；
    /// - 在 finally 记录超过 <see cref="ApiHost.SlowTrace"/> 的慢处理日志（Action/Code/耗时ms）；
    /// - 若 <see cref="UseHttpStatus"/> 为 true，则使用 HTTP 状态码表达结果。
    /// 
    /// 注意：result 的缓冲区所有权会转移给返回的 response，由上层 using IMessage 级联释放。
    /// </remarks>
    /// <param name="session">网络会话</param>
    /// <param name="msg">消息</param>
    /// <param name="serviceProvider">当前作用域的服务提供者</param>
    /// <returns>要应答对方的消息，为空表示不应答</returns>
    public virtual async Task<IMessage?> ProcessAsync(IApiSession session, IMessage msg, IServiceProvider serviceProvider)
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

                result = await OnProcessAsync(session, request.Action, request.Data, msg, serviceProvider).ConfigureAwait(false);

                // 流式返回：IAsyncEnumerable<T> 逐条推送
                // 判定依据取注册期缓存的 ApiAction.IsStreaming（由动作返回类型静态判定），
                // 避免运行时枚举 result 类型的接口列表——编译器生成的具体类型（如 <Range>d__0）
                // 在裁剪与 AOT 下无法静态保证其接口元数据完整
                if (result != null && Manager.Find(request.Action)?.IsStreaming == true)
                {
                    await ProcessStreamWithCancelAsync(session, msg, request.Action, result, enc).ConfigureAwait(false);
                    return null;
                }
            }
            catch (Exception ex)
            {
                ex = ex.GetTrue();

                if (ShowError) WriteLog("{0}", ex.ToString());

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
                        ? L.T("Database SQL error")
                        : ex.Message;
                }

                span?.SetError(ex, request.Data?.ToStr());
            }

            // 单向请求无需响应，释放可能的 IOwnerPacket 归还 ArrayPool
            if (msg.Kind == MessageKinds.OneWay)
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
                WriteLog("Slow processing [{0}], Code={1}, elapsed {2:n0}ms", request?.Action, code, msCost);

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

    /// <summary>处理器类型是否走异步路径的缓存。避免每请求反射检测</summary>
    private static readonly ConcurrentDictionary<Type, Boolean> _asyncHandlerCache = new();

    /// <summary>判断处理器类型是否走异步路径。仅重写同步 Execute 而未重写 ExecuteAsync 的旧子类回退同步路径</summary>
    /// <param name="handlerType">处理器运行时类型</param>
    /// <returns>是否走异步路径</returns>
    /// <remarks>只查询 Execute/ExecuteAsync 两个虚方法的声明类型；二者均在本程序集被静态调用，元数据必然保留</remarks>
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Execute/ExecuteAsync 为本类型静态引用到的虚方法，其元数据必然保留；此处只查询声明类型")]
    private static Boolean IsAsyncHandler(Type handlerType)
    {
        var mExecute = handlerType.GetMethod(nameof(ApiHandler.Execute), BindingFlags.Public | BindingFlags.Instance);
        var mExecuteAsync = handlerType.GetMethod(nameof(ApiHandler.ExecuteAsync), BindingFlags.Public | BindingFlags.Instance);
        return mExecute?.DeclaringType == typeof(ApiHandler) || mExecuteAsync?.DeclaringType != typeof(ApiHandler);
    }

    /// <summary>异步执行消息处理，交给Handler。实现 IAsyncApiHandler 的处理器走异步路径，旧处理器回退同步 Execute</summary>
    /// <param name="session">会话</param>
    /// <param name="action">动作</param>
    /// <param name="args">参数</param>
    /// <param name="msg">消息</param>
    /// <param name="serviceProvider">当前作用域的服务提供者</param>
    /// <returns>处理结果；异常将由 <see cref="ProcessAsync"/> 捕获并转换为错误码</returns>
    protected virtual async Task<Object?> OnProcessAsync(IApiSession session, String action, IPacket? args, IMessage msg, IServiceProvider serviceProvider)
    {
        // 实现异步接口的处理器走异步路径，避免 async 动作阻塞线程
        if (Handler is IAsyncApiHandler ah)
        {
            // 缓存类型级决策（反射仅首次）
            var useAsync = _asyncHandlerCache.GetOrAdd(ah.GetType(), IsAsyncHandler);
            if (useAsync)
                return await ah.ExecuteAsync(session, action, args, msg, serviceProvider).ConfigureAwait(false);
        }

        return Handler?.Execute(session, action, args, msg, serviceProvider);
    }
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
        if (pf2 != null && pf2.Value > 0) sb.AppendFormat(L.T("Processed: {0} ", pf2));

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
    /// <summary>异步处理流式调用。逐条迭代 IAsyncEnumerable，编码并发送每个数据块，最后发送结束标记</summary>
    /// <param name="session">会话</param>
    /// <param name="msg">原始请求消息</param>
    /// <param name="action">动作名</param>
    /// <param name="streamResult">IAsyncEnumerable 实例</param>
    /// <param name="enc">编码器</param>
    private async Task ProcessStreamWithCancelAsync(IApiSession session, IMessage msg, String action, Object streamResult, IEncoder enc)
    {
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
            }, CancellationToken.None);
        }

        try
        {
            await ProcessStreamAsync(session, msg, action, streamResult, enc, cts.Token).ConfigureAwait(false);
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
                await SendValueTypeStreamAsync(streamResult, session, msg, action, enc, cancellationToken).ConfigureAwait(false);
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
            WriteLog("Streaming call canceled (client disconnected)");
        }
    }

    /// <summary>发送值类型元素流。IAsyncEnumerable&lt;T&gt; 的泛型实参运行期才确定</summary>
    /// <param name="streamResult">IAsyncEnumerable&lt;T&gt; 实例</param>
    /// <param name="session">会话</param>
    /// <param name="msg">原始请求消息</param>
    /// <param name="action">动作名</param>
    /// <param name="enc">编码器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    /// <remarks>
    /// 引用类型元素已由 <see cref="IAsyncEnumerable{T}"/> 的协变转为 IAsyncEnumerable&lt;Object?&gt; 处理，
    /// 此处只剩值类型元素。运行期泛型实例化（MakeGenericMethod）在 AOT 下不可用，
    /// 故由源生成器为动作预生成的流式泵优先承接，未命中时回退运行期泛型实例化。
    /// </remarks>
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "流式返回实例已实例化，其 IAsyncEnumerable<T> 接口实现随实例可达而保留；此处只读取泛型实参")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "回退路径由 DynamicCodeHelper.IsSupported 守卫，AOT 下优先走源生成器预生成的流式泵")]
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "ApiStreamPump.PumpAsync 为本程序集公开静态方法，元数据必然保留")]
    private static async Task SendValueTypeStreamAsync(Object streamResult, IApiSession session, IMessage msg, String action, IEncoder enc, CancellationToken cancellationToken)
    {
        var resultType = streamResult.GetType();
        var iface = resultType.GetInterfaces().FirstOrDefault(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));
        if (iface == null)
            throw new InvalidOperationException(L.T("Streaming return type {0} does not implement IAsyncEnumerable<T>", resultType.FullName));

        var elementType = iface.GetGenericArguments()[0];

        // 优先使用源生成器为动作预生成的流式泵（编译期已静态扎根，AOT 下零泛型实例化）
        if (ApiActionTable.TryGetStreamPump(elementType, out var pump) && pump != null)
        {
            await pump(streamResult, session, msg, action, enc, cancellationToken).ConfigureAwait(false);
            return;
        }

        // 回退：运行期泛型实例化，仅动态代码可用时成立
        DynamicCode.ThrowIfNotSupported(L.T("value type streaming return IAsyncEnumerable<{0}>", elementType.Name));

        var helperMethod = typeof(ApiStreamPump).GetMethod(nameof(ApiStreamPump.PumpAsync), BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException(L.T("Cannot find the PumpAsync method"));
        var genericHelper = helperMethod.MakeGenericMethod(elementType);
        var invokeTask = (Task)genericHelper.Invoke(null, [streamResult, session, msg, action, enc, cancellationToken])!;
        await invokeTask.ConfigureAwait(false);
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