﻿using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using NewLife.Caching;
using NewLife.Log;
using NewLife.Model;
using NewLife.Net;
using NewLife.Reflection;
using NewLife.Remoting.Models;
using NewLife.Security;
using NewLife.Serialization;
using NewLife.Threading;
#if !NET40
using TaskEx = System.Threading.Tasks.Task;
#endif

namespace NewLife.Remoting.Clients;

/// <summary>应用客户端基类。实现对接目标平台的登录、心跳、更新和指令下发等场景操作</summary>
/// <remarks>
/// 典型应用架构：
/// 1，RPC应用架构
///     客户端ApiClient通过Tcp/Udp等协议连接服务端ApiServer，进行登录、心跳和更新等操作，服务端直接下发指令。
///     例如蚂蚁调度，客户端使用应用编码和密钥登录后，获得令牌，后续无需验证令牌，直到令牌过期，重新登录。
/// 2，Http应用架构
///     客户端ApiHttpClient通过Http/Https协议连接服务端WebApi，进行登录、心跳和更新等操作，服务端通过WebSocket下发指令。
///     例如ZeroIot，客户端使用设备编码和密钥登录后，获得令牌，后续每次请求都需要带上令牌，在心跳时维持WebSocket长连接。
/// 3，OAuth应用架构
///     客户端ApiHttpClient通过Http/Https协议连接服务端WebApi，进行OAuth登录，获得令牌，后续每次请求都需要带上令牌。
///     例如星尘AppClient，AppId和AppSecret进行OAuth登录后，获得令牌，后续每次请求都需要带上令牌。
/// </remarks>
public abstract class ClientBase : DisposeBase, IApiClient, ICommandClient, IEventProvider, ITracerFeature, ILogFeature
{
    #region 属性
    /// <summary>服务端地址。支持http/tcp/udp，多地址逗号分隔</summary>
    public String? Server { get; set; }

    /// <summary>编码。设备编码DeviceCode，或应用标识AppId</summary>
    public String? Code { get; set; }

    /// <summary>密钥。设备密钥DeviceSecret，或应用密钥AppSecret</summary>
    public String? Secret { get; set; }

    /// <summary>密码提供者</summary>
    /// <remarks>
    /// 用于保护密码传输，默认提供者为空，密码将明文传输。
    /// 推荐使用SaltPasswordProvider。
    /// </remarks>
    public IPasswordProvider? PasswordProvider { get; set; }

    /// <summary>服务提供者</summary>
    public IServiceProvider? ServiceProvider { get; set; }

    private IApiClient? _client;
    /// <summary>Api客户端</summary>
    public IApiClient? Client { get => _client; set => _client = value; }

    String? IApiClient.Token { get => _client?.Token; set { if (_client != null) _client.Token = value; } }

    /// <summary>是否已登录</summary>
    public Boolean Logined { get; set; }

    /// <summary>登录完成后触发</summary>
    public event EventHandler<LoginEventArgs>? OnLogined;

    /// <summary>请求到服务端并返回的延迟时间。单位ms</summary>
    public Int32 Delay { get; set; }

    /// <summary>最大失败数。超过该数时，新的数据将被抛弃，默认120</summary>
    public Int32 MaxFails { get; set; } = 1 * 24 * 60;

    private readonly ConcurrentDictionary<String, Delegate> _commands = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>命令集合</summary>
    public IDictionary<String, Delegate> Commands => _commands;

    /// <summary>收到命令时触发</summary>
    public event EventHandler<CommandEventArgs>? Received;

    /// <summary>客户端功能特性。默认登录注销心跳，可添加更新等</summary>
    public Features Features { get; set; } = Features.Login | Features.Logout | Features.Ping;

    /// <summary>各功能的命令集合</summary>
    public IDictionary<Features, String> Actions { get; set; } = null!;

    /// <summary>客户端设置</summary>
    public IClientSetting? Setting { get; set; }

    /// <summary>协议版本</summary>
    private readonly static String _version;
    private readonly static String _name;
    private TimeSpan _span;
    private readonly ConcurrentQueue<IPingRequest> _fails = new();
    private readonly ICache _cache = new MemoryCache();
    #endregion

    #region 构造
    static ClientBase()
    {
        var asm = AssemblyX.Entry ?? AssemblyX.Create(Assembly.GetExecutingAssembly());
        _version = asm?.FileVersion + "";
        _name = asm?.Name ?? "NewLifeRemoting";
    }

    /// <summary>实例化</summary>
    public ClientBase() => SetActions("Device/");

    /// <summary>通过客户端设置实例化</summary>
    /// <param name="setting"></param>
    public ClientBase(IClientSetting setting) : this()
    {
        Setting = setting;

        Server = setting.Server;
        Code = setting.Code;
        Secret = setting.Secret;
    }

    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        Logout(disposing ? "Dispose" : "GC").Wait(1_000);

        StopTimer();

        Logined = false;
    }
    #endregion

    #region 方法
    /// <summary>设置各功能接口名</summary>
    /// <param name="prefix"></param>
    protected virtual void SetActions(String prefix)
    {
        Actions = new Dictionary<Features, String>
        {
            [Features.Login] = prefix + "Login",
            [Features.Logout] = prefix + "Logout",
            [Features.Ping] = prefix + "Ping",
            [Features.Notify] = prefix + "Notify",
            [Features.Upgrade] = prefix + "Upgrade",
            [Features.PostEvent] = prefix + "PostEvents"
        };
    }

    /// <summary>初始化</summary>
    [MemberNotNull(nameof(_client))]
    private void Init()
    {
        if (_client != null) return;

        OnInit();
    }

    /// <summary>初始化</summary>
    [MemberNotNull(nameof(_client))]
    protected virtual void OnInit()
    {
        var provider = ServiceProvider ??= ObjectContainer.Provider;

        // 找到容器，注册默认的模型实现，供后续InvokeAsync时自动创建正确的模型对象
        var container = provider?.GetService<IObjectContainer>() ?? ObjectContainer.Current;
        if (container != null)
        {
            container.TryAddTransient<ILoginRequest, LoginRequest>();
            container.TryAddTransient<ILoginResponse, LoginResponse>();
            container.TryAddTransient<ILogoutResponse, LogoutResponse>();
            container.TryAddTransient<IPingRequest, PingRequest>();
            container.TryAddTransient<IPingResponse, PingResponse>();
            container.TryAddTransient<IUpgradeInfo, UpgradeInfo>();
        }

        //PasswordProvider ??= GetService<IPasswordProvider>() ?? new SaltPasswordProvider { Algorithm = "md5", SaltTime = 60 };
        PasswordProvider ??= GetService<IPasswordProvider>();

        if (_client == null)
        {
            var urls = Server ?? Setting?.Server;
            if (urls.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Setting), "未指定服务端地址");

            _client = urls.StartsWithIgnoreCase("http", "https") ? CreateHttp(urls) : CreateRpc(urls);
        }
    }

    /// <summary>创建Http客户端</summary>
    /// <param name="urls"></param>
    /// <returns></returns>
    protected virtual ApiHttpClient CreateHttp(String urls) => new(urls) { Log = Log, DefaultUserAgent = $"{_name}/v{_version}" };

    /// <summary>创建RPC客户端</summary>
    /// <param name="urls"></param>
    /// <returns></returns>
    protected virtual ApiClient CreateRpc(String urls)
    {
        var client = new MyApiClient { Client = this, Servers = urls.Split(","), Log = Log };
        client.Received += (s, e) =>
        {
            var msg = e.Message;
            var api = e.ApiMessage;
            if (msg != null && !msg.Reply && api != null && api.Action == "Notify")
            {
                var cmd = api.Data?.ToStr().ToJsonEntity<CommandModel>();
                if (cmd != null)
                    _ = ReceiveCommand(cmd, client.Local?.Type + "");
            }
        };

        return client;
    }

    class MyApiClient : ApiClient
    {
        public ClientBase Client { get; set; } = null!;

        protected override async Task<Object?> OnLoginAsync(ISocketClient client, Boolean force) => await InvokeWithClientAsync<Object>(client, Client.Actions[Features.Login], Client.BuildLoginRequest());
    }

    /// <summary>异步调用</summary>
    /// <param name="action">动作</param>
    /// <param name="args">参数</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public virtual async Task<TResult> OnInvokeAsync<TResult>(String action, Object? args, CancellationToken cancellationToken)
    {
        if (Log != null && Log.Level <= LogLevel.Debug) WriteLog("[{0}]=>{1}", action, args?.ToJson());

        Init();

        TResult? rs = default;
        if (_client is ApiHttpClient http)
        {
            var method = System.Net.Http.HttpMethod.Post;
            if (args == null || action.StartsWithIgnoreCase("Get") || action.ToLower().Contains("/get"))
                method = System.Net.Http.HttpMethod.Get;

            rs = await http.InvokeAsync<TResult>(method, action, args, null, cancellationToken);
        }
        else
            rs = await _client.InvokeAsync<TResult>(action, args, cancellationToken);

        if (Log != null && Log.Level <= LogLevel.Debug) WriteLog("[{0}]<={1}", action, rs?.ToJson());

        return rs!;
    }

    /// <summary>远程调用拦截，支持重新登录</summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="action"></param>
    /// <param name="args"></param>
    /// <param name="cancellationToken">取消通知</param>
    /// <returns></returns>
    public virtual async Task<TResult?> InvokeAsync<TResult>(String action, Object? args = null, CancellationToken cancellationToken = default)
    {
        var needLogin = !Actions[Features.Login].EqualIgnoreCase(action);
        if (!Logined && needLogin && Features.HasFlag(Features.Login)) await Login();

        try
        {
            return await OnInvokeAsync<TResult>(action, args, cancellationToken);
        }
        catch (Exception ex)
        {
            var ex2 = ex.GetTrue();
            if (ex2 is ApiException aex)
            {
                if (Logined && aex.Code == ApiCode.Unauthorized && needLogin && Features.HasFlag(Features.Login))
                {
                    Log?.Debug("{0}", ex);
                    WriteLog("重新登录！");
                    await Login();

                    return await OnInvokeAsync<TResult>(action, args, cancellationToken);
                }

                throw new ApiException(aex.Code, $"[{action}]{aex.Message}");
            }

            throw;
        }
    }

    /// <summary>同步调用</summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="action"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    [return: MaybeNull]
    public virtual TResult Invoke<TResult>(String action, Object? args = null) => TaskEx.Run(() => InvokeAsync<TResult>(action, args)).Result;

    /// <summary>设置令牌。派生类可重定义逻辑</summary>
    /// <param name="token"></param>
    protected virtual void SetToken(String? token)
    {
        if (_client != null) _client.Token = token;
    }

    /// <summary>获取相对于服务器的当前时间，避免两端时间差</summary>
    /// <returns></returns>
    public DateTime GetNow() => DateTime.Now.Add(_span);
    #endregion

    #region 登录
    /// <summary>登录</summary>
    /// <returns></returns>
    public virtual async Task<ILoginResponse?> Login()
    {
        Init();

        using var span = Tracer?.NewSpan(nameof(Login), Code);
        WriteLog("登录：{0}", Code);
        try
        {
            var request = BuildLoginRequest();

            // 登录前清空令牌，避免服务端使用上一次信息
            SetToken(null);
            Logined = false;

            var rs = await LoginAsync(request);
            if (rs == null) return null;

            if (!rs.Code.IsNullOrEmpty() && !rs.Secret.IsNullOrEmpty())
            {
                WriteLog("下发密钥：{0}/{1}", rs.Code, rs.Secret);
                Code = rs.Code;
                Secret = rs.Secret;

                var set = Setting;
                if (set != null)
                {
                    set.Code = rs.Code;
                    set.Secret = rs.Secret;
                    set.Save();
                }
            }

            FixTime(rs.Time, rs.Time);

            // 登录后设置用于用户认证的token
            SetToken(rs.Token);
            Logined = true;

            OnLogined?.Invoke(this, new(request, rs));

            StartTimer();

            return rs;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }

    /// <summary>计算客户端到服务端的网络延迟，以及相对时间差</summary>
    /// <param name="startTime"></param>
    /// <param name="serverTime"></param>
    protected void FixTime(Int64 startTime, Int64 serverTime)
    {
        var dt = startTime.ToDateTime();
        if (dt.Year > 2000)
        {
            // 计算延迟
            var ts = DateTime.UtcNow - dt;
            var ms = (Int32)ts.TotalMilliseconds;
            if (Delay > 0)
                Delay = (Delay + ms) / 2;
            else
                Delay = ms;
        }

        // 时间偏移
        dt = serverTime.ToDateTime();
        if (dt.Year > 2000) _span = dt.AddMilliseconds(Delay / 2) - DateTime.UtcNow;
    }

    /// <summary>获取登录信息</summary>
    /// <returns></returns>
    public virtual ILoginRequest BuildLoginRequest()
    {
        Init();

        var info = GetService<ILoginRequest>() ?? new LoginRequest();
        info.Code = Code;
        info.ClientId = $"{NetHelper.MyIP()}@{Process.GetCurrentProcess().Id}";

        if (!Secret.IsNullOrEmpty())
            info.Secret = PasswordProvider?.Hash(Secret) ?? Secret;

        if (info is LoginRequest request)
        {
            request.Version = _version;
            request.Time = DateTime.UtcNow.ToLong();
        }

        return info;
    }

    /// <summary>注销</summary>
    /// <param name="reason"></param>
    /// <returns></returns>
    public virtual async Task<ILogoutResponse?> Logout(String reason)
    {
        if (!Logined) return null;

        using var span = Tracer?.NewSpan(nameof(Logout), reason);
        WriteLog("注销：{0} {1}", Code, reason);

        try
        {
            var rs = await LogoutAsync(reason);

            // 更新令牌
            SetToken(rs?.Token);

            StopTimer();

            Logined = false;

            return rs;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            XTrace.WriteException(ex);

            return null;
        }
    }

    /// <summary>登录</summary>
    /// <param name="request">登录信息</param>
    /// <returns></returns>
    protected virtual Task<ILoginResponse?> LoginAsync(ILoginRequest request) => InvokeAsync<ILoginResponse>(Actions[Features.Login], request);

    /// <summary>注销</summary>
    /// <returns></returns>
    protected virtual Task<ILogoutResponse?> LogoutAsync(String reason) => InvokeAsync<ILogoutResponse>(Actions[Features.Logout], new { reason });
    #endregion

    #region 心跳
    /// <summary>心跳</summary>
    /// <returns></returns>
    public virtual async Task<IPingResponse?> Ping()
    {
        Init();

        //if (Tracer != null) DefaultSpan.Current = null;
        using var span = Tracer?.NewSpan(nameof(Ping));
        try
        {
            var request = BuildPingRequest();

            // 如果网络不可用，直接保存到队列
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                if (_fails.Count < MaxFails) _fails.Enqueue(request);
                return null;
            }

            IPingResponse? rs = null;
            try
            {
                rs = await PingAsync(request);
                if (rs != null)
                {
                    // 由服务器改变采样频率
                    if (rs.Period > 0 && _timer != null) _timer.Period = rs.Period * 1000;

                    FixTime(rs.Time, rs.ServerTime);

                    // 更新令牌。即将过期时，服务端会返回新令牌
                    if (!rs.Token.IsNullOrEmpty()) SetToken(rs.Token);

                    // 推队列
                    if (rs.Commands != null && rs.Commands.Length > 0)
                    {
                        foreach (var model in rs.Commands)
                        {
                            await ReceiveCommand(model, "Pong");
                        }
                    }
                }
            }
            catch
            {
                if (_fails.Count < MaxFails) _fails.Enqueue(request);

                throw;
            }

            // 上报正常，处理历史，失败则丢弃
            while (_fails.TryDequeue(out var info))
            {
                await PingAsync(info);
            }

            return rs;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);

            var ex2 = ex.GetTrue();
            if (ex2 is ApiException aex && (aex.Code == ApiCode.Unauthorized || aex.Code == ApiCode.Forbidden) && Features.HasFlag(Features.Login))
            {
                WriteLog("重新登录");
                await Login();

                return null;
            }

            WriteLog("心跳异常 {0}", ex.GetTrue().Message);

            throw;
        }
    }

    /// <summary>获取心跳信息</summary>
    public virtual IPingRequest BuildPingRequest()
    {
        Init();

        var request = GetService<IPingRequest>() ?? new PingRequest();
        request.Time = DateTime.UtcNow.ToLong();

        if (request is PingRequest req)
        {
            var path = ".".GetFullPath();
            var driveInfo = DriveInfo.GetDrives().FirstOrDefault(e => path.StartsWithIgnoreCase(e.Name));
            var mi = MachineInfo.GetCurrent();
            mi.Refresh();

            req.Memory = mi.Memory;
            req.AvailableMemory = mi.AvailableMemory;
            req.TotalSize = (UInt64)(driveInfo?.TotalSize ?? 0);
            req.AvailableFreeSpace = (UInt64)(driveInfo?.AvailableFreeSpace ?? 0);
            req.CpuRate = Math.Round(mi.CpuRate, 3);
            req.Temperature = Math.Round(mi.Temperature, 1);
            req.Battery = Math.Round(mi.Battery, 3);
            req.UplinkSpeed = mi.UplinkSpeed;
            req.DownlinkSpeed = mi.DownlinkSpeed;

            var ip = NetHelper.GetIPs().Where(ip => ip.IsIPv4() && !IPAddress.IsLoopback(ip) && ip.GetAddressBytes()[0] != 169).Join();
            req.IP = ip;

            req.Delay = Delay;
            req.Uptime = Environment.TickCount / 1000;

            // 开始时间 Environment.TickCount 很容易溢出，导致开机24天后变成负数。
            // 后来在 netcore3.0 增加了Environment.TickCount64
            // 现在借助 Stopwatch 来解决
            if (Stopwatch.IsHighResolution) req.Uptime = (Int32)(Stopwatch.GetTimestamp() / Stopwatch.Frequency);
        }

        return request;
    }

    /// <summary>心跳</summary>
    /// <param name="request"></param>
    /// <returns></returns>
    protected virtual Task<IPingResponse?> PingAsync(IPingRequest request) => InvokeAsync<IPingResponse>(Actions[Features.Ping], request);
    #endregion

    #region 下行通知
    private TimerX? _timer;
    private TimerX? _timerUpgrade;
    /// <summary>开始心跳定时器</summary>
    protected virtual void StartTimer()
    {
        if (_timer == null)
        {
            lock (this)
            {
                if (_timer == null)
                {
                    if (Features.HasFlag(Features.Ping) || Features.HasFlag(Features.Notify))
                        _timer = new TimerX(OnPing, null, 1000, 60_000, "Client") { Async = true };

                    if (Features.HasFlag(Features.Upgrade))
                        _timerUpgrade = new TimerX(s => Upgrade(), null, 5_000, 600_000, "Client") { Async = true };
                }
            }
        }
    }

    /// <summary>停止心跳定时器</summary>
    protected virtual void StopTimer()
    {
        _timer.TryDispose();
        _timer = null;
        _timerUpgrade.TryDispose();
        _timerUpgrade = null;
        _eventTimer.TryDispose();
        _eventTimer = null;

        _ws.TryDispose();
        _ws = null;
    }

    private WsChannel? _ws;
    /// <summary>定时心跳</summary>
    /// <param name="state"></param>
    /// <returns></returns>
    protected virtual async Task OnPing(Object state)
    {
        //DefaultSpan.Current = null;
        using var span = Tracer?.NewSpan("ClientPing");
        try
        {
            if (Features.HasFlag(Features.Ping)) await Ping();

            if (_client is ApiHttpClient http && Features.HasFlag(Features.Notify))
            {
#if NETCOREAPP
                _ws ??= new WsChannelCore(this);
#else
                _ws ??= new WsChannel(this);
#endif
                if (_ws != null) await _ws.ValidWebSocket(http);
            }
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            Log?.Debug("{0}", ex);
        }
    }

    /// <summary>收到命令</summary>
    /// <param name="model"></param>
    /// <param name="source"></param>
    /// <returns></returns>
    public async Task ReceiveCommand(CommandModel model, String source)
    {
        if (model == null) return;

        // 去重，避免命令被重复执行
        if (!_cache.Add($"cmd:{model.Id}", model, 3600)) return;

        // 埋点，建立调用链
        using var span = Tracer?.NewSpan("cmd:" + model.Command, model);
        if (!model.TraceId.IsNullOrEmpty()) span?.Detach(model.TraceId);
        try
        {
            // 有效期判断前把UTC转为本地时间
            var now = GetNow();
            var expire = model.Expire.ToLocalTime();
            XTrace.WriteLine("[{0}] Got Command: {1}", source, model.ToJson());
            if (model.Expire.Year < 2000 || model.Expire > now)
            {
                // 延迟执行
                var startTime = model.StartTime.ToLocalTime();
                var ts = startTime - now;
                if (ts.TotalMilliseconds > 0)
                {
                    TimerX.Delay(s =>
                    {
                        _ = OnReceiveCommand(model);
                    }, (Int32)ts.TotalMilliseconds);

                    var reply = new CommandReplyModel
                    {
                        Id = model.Id,
                        Status = CommandStatus.处理中,
                        Data = $"已安排计划执行 {startTime.ToFullString()}"
                    };
                    await CommandReply(reply);
                }
                else
                    await OnReceiveCommand(model);
            }
            else
            {
                var reply = new CommandReplyModel { Id = model.Id, Status = CommandStatus.取消 };
                await CommandReply(reply);
            }
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
        }
    }

    /// <summary>触发收到命令的动作</summary>
    /// <param name="model"></param>
    protected virtual async Task OnReceiveCommand(CommandModel model)
    {
        var e = new CommandEventArgs { Model = model };
        Received?.Invoke(this, e);

        var rs = await this.ExecuteCommand(model);
        e.Reply ??= rs;

        if (e.Reply != null && e.Reply.Id > 0) await CommandReply(e.Reply);
    }

    /// <summary>向命令引擎发送命令，触发指定已注册动作</summary>
    /// <param name="command"></param>
    /// <param name="argument"></param>
    /// <returns></returns>
    public async Task SendCommand(String command, String argument) => await OnReceiveCommand(new CommandModel { Command = command, Argument = argument });

    /// <summary>上报命令调用结果</summary>
    /// <param name="model"></param>
    /// <returns></returns>
    public virtual Task<Object?> CommandReply(CommandReplyModel model) => InvokeAsync<Object>(Actions[Features.CommandReply], model);
    #endregion

    #region 上报
    private readonly ConcurrentQueue<EventModel> _events = new();
    private readonly ConcurrentQueue<EventModel> _failEvents = new();
    private TimerX? _eventTimer;
    private String? _eventTraceId;

    void InitEvent() => _eventTimer ??= new TimerX(DoPostEvent, null, 3_000, 60_000, "Client") { Async = true };

    /// <summary>批量上报事件</summary>
    /// <param name="events"></param>
    /// <returns></returns>
    public virtual Task<Int32> PostEvents(params EventModel[] events) => InvokeAsync<Int32>(Actions[Features.PostEvent], events);

    async Task DoPostEvent(Object state)
    {
        if (!NetworkInterface.GetIsNetworkAvailable()) return;

        DefaultSpan.Current = null;
        var tid = _eventTraceId;
        _eventTraceId = null;

        // 正常队列为空，异常队列有数据，给它一次机会
        if (_events.IsEmpty && !_failEvents.IsEmpty)
        {
            while (_failEvents.TryDequeue(out var ev))
            {
                _events.Enqueue(ev);
            }
        }

        while (!_events.IsEmpty)
        {
            var max = 100;
            var list = new List<EventModel>();
            while (_events.TryDequeue(out var model) && max-- > 0) list.Add(model);

            using var span = Tracer?.NewSpan("PostEvent", list.Count);
            if (tid != null) span?.Detach(tid);
            try
            {
                if (list.Count > 0) await PostEvents(list.ToArray());

                // 成功后读取本地缓存
                while (_failEvents.TryDequeue(out var ev))
                {
                    _events.Enqueue(ev);
                }
            }
            catch (Exception ex)
            {
                span?.SetError(ex, null);

                // 失败后进入本地缓存
                foreach (var item in list)
                {
                    _failEvents.Enqueue(item);
                }
            }
        }
    }

    /// <summary>写事件</summary>
    /// <param name="type"></param>
    /// <param name="name"></param>
    /// <param name="remark"></param>
    public virtual Boolean WriteEvent(String type, String name, String? remark)
    {
        // 使用时才创建定时器
        InitEvent();

        // 记录追踪标识，上报的时候带上，尽可能让源头和下游串联起来
        _eventTraceId = DefaultSpan.Current?.ToString();

        var now = GetNow().ToUniversalTime();
        var ev = new EventModel { Time = now.ToLong(), Type = type, Name = name, Remark = remark };
        _events.Enqueue(ev);

        _eventTimer?.SetNext(1000);

        return true;
    }
    #endregion

    #region 更新
    private String? _lastVersion;
    /// <summary>获取更新信息</summary>
    /// <returns></returns>
    public async Task<IUpgradeInfo?> Upgrade()
    {
        using var span = Tracer?.NewSpan(nameof(Upgrade));
        WriteLog("检查更新");

        // 清理
        var ug = new Upgrade { Log = XTrace.Log };
        ug.DeleteBackup(".");

        var info = await UpgradeAsync();
        if (info != null && info.Version != _lastVersion)
        {
            WriteLog("发现更新：{0}", info.ToJson(true));

            ug.Url = info.Source;
            await ug.Download();

            // 检查文件完整性
            if (info.FileHash.IsNullOrEmpty() || ug.CheckFileHash(info.FileHash))
            {
                // 执行更新，解压缩覆盖文件
                var rs = ug.Update();
                if (rs && !info.Executor.IsNullOrEmpty()) ug.Run(info.Executor);
                _lastVersion = info.Version;

                // 强制更新时，马上重启
                if (rs && info.Force) Restart(ug);
            }
        }

        return info;
    }

    /// <summary>更新完成，重启自己</summary>
    /// <param name="upgrade"></param>
    protected virtual void Restart(Upgrade upgrade)
    {
        var asm = Assembly.GetEntryAssembly();

        // 重新拉起进程
        var rs = upgrade.Run("dotnet", $"{asm!.Location} -upgrade {Environment.CommandLine}");

        if (rs) upgrade.KillSelf();
    }

    /// <summary>更新</summary>
    /// <returns></returns>
    protected virtual Task<IUpgradeInfo?> UpgradeAsync() => InvokeAsync<IUpgradeInfo>(Actions[Features.Upgrade]);
    #endregion

    #region 辅助
    /// <summary>从服务提供者（对象容器）创建模型对象</summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public virtual T? GetService<T>() where T : class => ServiceProvider?.GetService<T>() ?? ObjectContainer.Current.Resolve<T>();
    #endregion

    #region 日志
    /// <summary>链路追踪</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>写日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object?[] args) => Log?.Info(format, args);
    #endregion
}