using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Reflection;
using NewLife.Caching;
using NewLife.Data;
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
    /// <summary>客户端名称。例如Device/Node/App</summary>
    public String Name { get; set; } = null!;

    /// <summary>服务端地址。支持http/tcp/udp，支持客户端负载均衡，多地址逗号分隔</summary>
    public String? Server { get; set; }

    /// <summary>编码。设备编码DeviceCode，或应用标识AppId</summary>
    public String? Code { get; set; }

    /// <summary>密钥。设备密钥DeviceSecret，或应用密钥AppSecret</summary>
    public String? Secret { get; set; }

    /// <summary>调用超时时间。请求发出后，等待响应的最大时间，默认15_000ms</summary>
    public Int32 Timeout { get; set; } = 15_000;

    /// <summary>密码提供者</summary>
    /// <remarks>
    /// 用于保护密码传输，默认提供者为空，密码将明文传输。
    /// 推荐使用SaltPasswordProvider。
    /// </remarks>
    public IPasswordProvider? PasswordProvider { get; set; }

    /// <summary>服务提供者</summary>
    /// <remarks>借助对象容器，解析各基本接口的请求响应模型</remarks>
    public IServiceProvider? ServiceProvider { get; set; }

    private IApiClient? _client;
    /// <summary>Api客户端。ApiClient或ApiHttpClient</summary>
    public IApiClient? Client { get => _client; set => _client = value; }

    String? IApiClient.Token { get => _client?.Token; set { if (_client != null) _client.Token = value; } }

    /// <summary>登录状态</summary>
    public LoginStatus Status { get; set; }

    /// <summary>是否已登录</summary>
    public Boolean Logined => Status == LoginStatus.LoggedIn;

    /// <summary>登录完成后触发</summary>
    public event EventHandler<LoginEventArgs>? OnLogined;

    /// <summary>请求到服务端并返回的延迟时间。单位ms</summary>
    public Int32 Delay { get; set; }

    private TimeSpan _span;
    /// <summary>时间差。服务器时间减去客户端时间</summary>
    public TimeSpan Span => _span;

    /// <summary>最大失败数。心跳上报失败时进入失败队列，并稍候重试。重试超过该数时，新的数据将被抛弃，默认1440次，约24小时</summary>
    public Int32 MaxFails { get; set; } = 1 * 24 * 60;

    private readonly ConcurrentDictionary<String, Delegate> _commands = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>命令集合。注册到客户端的命令与委托</summary>
    public IDictionary<String, Delegate> Commands => _commands;

    /// <summary>收到下行命令时触发</summary>
    public event EventHandler<CommandEventArgs>? Received;

    /// <summary>客户端功能特性。默认登录注销心跳，可添加更新等</summary>
    public Features Features { get; set; } = Features.Login | Features.Logout | Features.Ping;

    /// <summary>各功能的动作集合。记录每一种功能所对应的动作接口路径。</summary>
    public IDictionary<Features, String> Actions { get; set; } = null!;

    /// <summary>Json主机。提供序列化能力</summary>
    public IJsonHost JsonHost { get; set; } = null!;

    /// <summary>客户端设置</summary>
    public IClientSetting? Setting { get; set; }

    /// <summary>协议版本</summary>
    private readonly static String _version;
    private readonly static String _name;
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
    public ClientBase()
    {
        Name = GetType().Name.TrimEnd("Client");
    }

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

        if (Features.HasFlag(Features.Logout))
            Logout(disposing ? "Dispose" : "GC").Wait(1_000);

        StopTimer();

        Status = LoginStatus.Ready;

        _timerLogin.TryDispose();
        _timerLogin = null;
    }
    #endregion

    #region 方法
    /// <summary>设置各功能接口路径</summary>
    /// <param name="prefix"></param>
    protected virtual void SetActions(String prefix)
    {
        Actions = new Dictionary<Features, String>
        {
            [Features.Login] = prefix + "Login",
            [Features.Logout] = prefix + "Logout",
            [Features.Ping] = prefix + "Ping",
            [Features.Upgrade] = prefix + "Upgrade",
            [Features.Notify] = prefix + "Notify",
            [Features.CommandReply] = prefix + "CommandReply",
            [Features.PostEvent] = prefix + "PostEvents",
        };

        this.RegisterCommand(prefix + "Upgrade", ReceiveUpgrade);
    }

    private String? _lastServer;
    /// <summary>初始化</summary>
    [MemberNotNull(nameof(_client))]
    protected void Init()
    {
        if (_client != null)
        {
            // 如果配置中的服务端地址与当前不一致，则需要同步修改客户端的服务地址
            var urls = Server ?? Setting?.Server;
            if (!urls.IsNullOrEmpty() && urls != _lastServer)
            {
                using var span = Tracer?.NewSpan("ChangeServer", new { urls, _lastServer });

                if (_client is ApiHttpClient http)
                    http.SetServer(urls);
                else if (_client is ApiClient rpc)
                    rpc.SetServer(urls);

                _lastServer = urls;
            }

            return;
        }

        OnInit();

        if (Actions == null || Actions.Count == 0) SetActions("Device/");
    }

    /// <summary>初始化对象容器以及客户端</summary>
    [MemberNotNull(nameof(_client))]
    protected virtual void OnInit()
    {
        var provider = ServiceProvider ??= ObjectContainer.Provider;

        // 找到容器，注册默认的模型实现，供后续InvokeAsync返回时自动创建正确的模型对象
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

        JsonHost ??= GetService<IJsonHost>() ?? JsonHelper.Default;
        //PasswordProvider ??= GetService<IPasswordProvider>() ?? new SaltPasswordProvider { Algorithm = "md5", SaltTime = 60 };
        PasswordProvider ??= GetService<IPasswordProvider>();

        if (_client == null)
        {
            var urls = Server ?? Setting?.Server;
            if (urls.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Setting), "未指定服务端地址");

            _client = urls.StartsWithIgnoreCase("http", "https") ? CreateHttp(urls) : CreateRpc(urls);
            _lastServer = urls;
        }
    }

    /// <summary>创建Http客户端</summary>
    /// <param name="urls"></param>
    /// <returns></returns>
    protected virtual ApiHttpClient CreateHttp(String urls) => new(urls)
    {
        JsonHost = JsonHost,
        DefaultUserAgent = $"{_name}/v{_version}",
        Timeout = Timeout,
        Log = Log,
    };

    /// <summary>创建RPC客户端</summary>
    /// <param name="urls"></param>
    /// <returns></returns>
    protected virtual ApiClient CreateRpc(String urls)
    {
        var client = new MyApiClient
        {
            Client = this,
            Servers = urls.Split(","),
            JsonHost = JsonHost,
            ServiceProvider = ServiceProvider,
            Timeout = Timeout,
            Log = Log
        };
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

        protected override Task<Object?> OnLoginAsync(ISocketClient client, Boolean force, CancellationToken cancellationToken) => InvokeWithClientAsync<Object>(client, Client.Actions[Features.Login], Client.BuildLoginRequest(), 0, cancellationToken);
    }

    /// <summary>异步调用。HTTP默认POST，自动识别GET</summary>
    /// <param name="action">动作</param>
    /// <param name="args">参数</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected virtual async Task<TResult> OnInvokeAsync<TResult>(String action, Object? args, CancellationToken cancellationToken)
    {
        if (Log != null && Log.Level <= LogLevel.Debug) WriteLog("[{0}]=>{1}", action, args is IPacket or Byte[]? "" : args?.ToJson());

        Init();

        // HTTP请求需要区分GET/POST
        TResult? rs = default;
        if (_client is ApiHttpClient http)
        {
            var method = HttpMethod.Post;
            if (args == null || args.GetType().IsBaseType() || action.StartsWithIgnoreCase("Get") || action.ToLower().Contains("/get"))
                method = HttpMethod.Get;

            rs = await http.InvokeAsync<TResult>(method, action, args, null, cancellationToken).ConfigureAwait(false);
        }
        else
            rs = await _client.InvokeAsync<TResult>(action, args, cancellationToken).ConfigureAwait(false);

        if (Log != null && Log.Level <= LogLevel.Debug) WriteLog("[{0}]<={1}", action, rs is IPacket or Byte[]? "" : rs?.ToJson());

        return rs!;
    }

    /// <summary>异步Get调用（仅用于HTTP）</summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="action"></param>
    /// <param name="args"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    protected virtual async Task<TResult> GetAsync<TResult>(String action, Object? args, CancellationToken cancellationToken = default)
    {
        if (Log != null && Log.Level <= LogLevel.Debug) WriteLog("[{0}]=>{1}", action, args is IPacket or Byte[]? "" : args?.ToJson());

        Init();

        if (_client is not ApiHttpClient http) throw new NotSupportedException();

        // 验证登录
        var needLogin = !Actions[Features.Login].EqualIgnoreCase(action);
        if (!Logined && needLogin && Features.HasFlag(Features.Login)) await Login(action, cancellationToken).ConfigureAwait(false);

        // GET请求
        var rs = await http.InvokeAsync<TResult>(HttpMethod.Get, action, args, null, cancellationToken).ConfigureAwait(false);

        if (Log != null && Log.Level <= LogLevel.Debug) WriteLog("[{0}]<={1}", action, rs is IPacket or Byte[]? "" : rs?.ToJson());

        return rs!;
    }

    /// <summary>[核心接口]远程调用服务端接口，支持重新登录</summary>
    /// <remarks>
    /// 所有对服务端接口的调用，都应该走这个方法，以便统一处理登录、心跳、令牌过期等问题。
    /// </remarks>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="action"></param>
    /// <param name="args"></param>
    /// <param name="cancellationToken">取消通知</param>
    /// <returns></returns>
    public virtual async Task<TResult?> InvokeAsync<TResult>(String action, Object? args = null, CancellationToken cancellationToken = default)
    {
        // 验证登录。如果该接口需要登录，且未登录，则先登录
        var needLogin = !Actions[Features.Login].EqualIgnoreCase(action);
        if (needLogin && !Logined && Features.HasFlag(Features.Login)) await Login(action, cancellationToken).ConfigureAwait(false);

        try
        {
            return await OnInvokeAsync<TResult>(action, args, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var ex2 = ex.GetTrue();
            if (ex2 is ApiException aex)
            {
                // 在客户端已登录状态下，服务端返回未授权，可能是令牌过期，尝试重新登录
                if (Logined && aex.Code == ApiCode.Unauthorized)
                {
                    Status = LoginStatus.Ready;
                    if (needLogin && Features.HasFlag(Features.Login))
                    {
                        Log?.Debug("{0}", ex);
                        WriteLog("重新登录，因调用[{0}]失败：{1}", action, ex.Message);
                        await Login(action, cancellationToken).ConfigureAwait(false);

                        // 再次执行当前请求
                        return await OnInvokeAsync<TResult>(action, args, cancellationToken).ConfigureAwait(false);
                    }
                }

                throw new ApiException(aex.Code, $"[{action}]{aex.Message}");
            }

            throw new XException($"[{action}]{ex.Message}", ex);
        }
    }

    /// <summary>同步调用</summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="action"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    [return: MaybeNull]
    public virtual TResult Invoke<TResult>(String action, Object? args = null)
    {
        using var source = new CancellationTokenSource(Timeout);
        return InvokeAsync<TResult>(action, args, source.Token).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    /// <summary>设置令牌。派生类可重定义逻辑</summary>
    /// <param name="token"></param>
    protected virtual void SetToken(String? token)
    {
        if (_client != null) _client.Token = token;
    }

    /// <summary>获取相对于服务器的当前时间，本地时区，避免两端时间差</summary>
    /// <returns></returns>
    public DateTime GetNow() => DateTime.Now.Add(_span);
    #endregion

    #region 登录注销
    private TimerX? _timerLogin;
    private Int32 _times;
    /// <summary>打开连接，尝试登录服务端。在网络未就绪之前反复尝试</summary>
    public virtual void Open()
    {
        _timerLogin = new TimerX(TryConnectServer, null, 0, 5_000) { Async = true };
    }

    private async Task TryConnectServer(Object state)
    {
        if (!NetworkInterface.GetIsNetworkAvailable())
        {
            WriteLog("网络不可达，延迟连接服务器");
            return;
        }

        var timer = _timerLogin;
        try
        {
            var source = new CancellationTokenSource(Timeout);
            if (!Logined) await Login(nameof(TryConnectServer), source.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // 登录报错后，加大定时间隔，输出简单日志
            if (timer != null && timer.Period < 30_000) timer.Period += 5_000;

            Log?.Error(ex.Message);

            if (!Logined) return;
        }

        timer.TryDispose();
        _timerLogin = null;
    }

    /// <summary>登录。使用编码和密钥登录服务端，获取令牌用于后续接口调用</summary>
    /// <remarks>
    /// 支持编码和密钥下发（自动注册）、时间校准。
    /// 用户可重载Login实现自定义登录逻辑，通过Logined判断是否登录成功。
    /// 也可以在OnLogined事件中处理登录成功后的逻辑。
    /// </remarks>
    /// <param name="source">来源。标记从哪里发起登录请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public virtual async Task<ILoginResponse?> Login(String? source = null, CancellationToken cancellationToken = default)
    {
        // 如果已登录，直接返回
        if (Status == LoginStatus.LoggedIn) return null;

        //!!! 这里的多线程登录设计不采取共用Task的架构，因为首次登录可能会失败，后续其它线程需要重新登录，而不是共用失败结果。

        // 如果正在登录，则稍等一会，避免重复登录。
        var times = Interlocked.Increment(ref _times);
        if (Status == LoginStatus.LoggingIn)
        {
            WriteLog("正在登录，请稍等{0}ms！序号：{1}，来源：{2}", 50 * 100, times, source);
            for (var i = 0; Status == LoginStatus.LoggingIn && i < 50; i++)
            {
                await TaskEx.Delay(100, cancellationToken).ConfigureAwait(false);
                if (Status == LoginStatus.LoggedIn) return null;
            }
        }

        if (Status != LoginStatus.LoggedIn) Status = LoginStatus.LoggingIn;

        Init();

        ILoginRequest? request = null;
        ILoginResponse? response = null;
        using var span = Tracer?.NewSpan(nameof(Login), new { Code, source, Server });
        WriteLog("登录：{0}，序号：{1}，来源：{2}", Code, times, source);
        try
        {
            // 创建登录请求，用户可重载BuildLoginRequest实现自定义登录请求，填充更多参数
            request = BuildLoginRequest();

            // 登录前清空令牌，避免服务端使用上一次信息
            SetToken(null);

            // 滚动的登录超时时间，实际上只对StarServer有效
            var timeout = times * 1000;
            if (timeout > Timeout) timeout = Timeout;
            var ts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, new CancellationTokenSource(timeout).Token);

            response = await LoginAsync(request, ts.Token).ConfigureAwait(false);
            if (response == null) return null;

            WriteLog("登录成功：{0}，序号：{1}，来源：{2}", response, times, source);

            // 登录后设置用于用户认证的token
            SetToken(response.Token);

            Status = LoginStatus.LoggedIn;
        }
        catch (Exception ex)
        {
            WriteLog("登录失败：{0}，序号：{1}，来源：{2}", ex.Message, times, source);

            Status = LoginStatus.Ready;
            span?.SetError(ex, null);
            throw;
        }

        // 登录成果。服务端执行自动注册时，可能有下发编码和密钥
        if (!response.Code.IsNullOrEmpty() && !response.Secret.IsNullOrEmpty())
        {
            WriteLog("下发密钥：{0}/{1}", response.Code, response.Secret);
            Code = response.Code;
            Secret = response.Secret;

            var set = Setting;
            if (set != null)
            {
                set.Code = response.Code;
                set.Secret = response.Secret;
                set.Save();
            }
        }

        FixTime(response.Time, response.ServerTime);

        OnLogined?.Invoke(this, new(request, response));

        StartTimer();

        return response;
    }

    /// <summary>计算客户端到服务端的网络延迟，以及相对时间差。支持GetNow()返回基于服务器的当前时间</summary>
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

    /// <summary>创建登录请求。支持重载后使用自定义的登录请求对象</summary>
    /// <remarks>
    /// 用户可以重载此方法，返回自定义的登录请求对象，用于支持更多的登录参数。
    /// 也可以调用基类BuildLoginRequest后得到ClientId和Secret等基本参数，然后填充自己的登录请求对象。
    /// 还可以直接使用自己的登录请求对象，调用FillLoginRequest填充基本参数。
    /// </remarks>
    /// <returns></returns>
    public virtual ILoginRequest BuildLoginRequest()
    {
        Init();

        var request = GetService<ILoginRequest>() ?? new LoginRequest();
        FillLoginRequest(request);

        return request;
    }

    /// <summary>填充登录请求。用户自定义登录时可选调用</summary>
    /// <param name="request"></param>
    protected virtual void FillLoginRequest(ILoginRequest request)
    {
        request.Code = Code;
        request.ClientId = $"{NetHelper.MyIP()}@{Process.GetCurrentProcess().Id}";

        if (!Secret.IsNullOrEmpty())
            request.Secret = PasswordProvider?.Hash(Secret) ?? Secret;

        if (request is LoginRequest info)
        {
            var asm = AssemblyX.Entry ?? AssemblyX.Create(Assembly.GetExecutingAssembly());
            if (asm != null)
            {
                info.Version = asm.FileVersion;
                info.Compile = asm.Compile.ToUniversalTime().ToLong();
            }

            info.IP = NetHelper.GetIPsWithCache().Where(e => e.IsIPv4() && e.GetAddressBytes()[0] != 169).Join();
            info.Macs = NetHelper.GetMacs().Select(e => e.ToHex("-")).Where(e => e != "00-00-00-00-00-00").OrderBy(e => e).Join(",");
            info.UUID = MachineInfo.GetCurrent().BuildCode();

            info.Time = DateTime.UtcNow.ToLong();
        }
    }

    /// <summary>注销。调用服务端注销接口，销毁令牌</summary>
    /// <param name="reason"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public virtual async Task<ILogoutResponse?> Logout(String? reason, CancellationToken cancellationToken = default)
    {
        if (!Logined) return null;

        using var span = Tracer?.NewSpan(nameof(Logout), reason);
        WriteLog("注销：{0} {1}", Code, reason);

        try
        {
            var rs = await LogoutAsync(reason, cancellationToken).ConfigureAwait(false);

            // 更新令牌
            SetToken(rs?.Token);

            StopTimer();

            Status = LoginStatus.Ready;

            return rs;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            Log?.Error(ex.ToString());

            return null;
        }
    }

    /// <summary>发起登录异步请求。由Login内部调用</summary>
    /// <param name="request">登录请求</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected virtual Task<ILoginResponse?> LoginAsync(ILoginRequest request, CancellationToken cancellationToken) => InvokeAsync<ILoginResponse>(Actions[Features.Login], request, cancellationToken);

    /// <summary>发起注销异步请求。由Logout内部调用</summary>
    /// <returns></returns>
    protected virtual async Task<ILogoutResponse?> LogoutAsync(String? reason, CancellationToken cancellationToken)
    {
        if (_client is ApiHttpClient)
            return await GetAsync<ILogoutResponse>(Actions[Features.Logout], new { reason }, cancellationToken).ConfigureAwait(false);

        return await InvokeAsync<ILogoutResponse>(Actions[Features.Logout], new { reason }, cancellationToken).ConfigureAwait(false);
    }
    #endregion

    #region 心跳保活
    /// <summary>心跳。请求服务端心跳接口，上报客户端性能数据的同时，更新其在服务端的最后活跃时间</summary>
    /// <remarks>
    /// 心跳逻辑内部带有失败重试机制，最大失败数MaxFails默认120，超过该数时，新的数据将被抛弃。
    /// 在网络不可用或者接口请求异常时，会将数据保存到队列，等待网络恢复或者下次心跳时重试。
    /// </remarks>
    /// <returns></returns>
    public virtual async Task<IPingResponse?> Ping(CancellationToken cancellationToken = default)
    {
        Init();

        //using var span = Tracer?.NewSpan(nameof(Ping));
        try
        {
            // 创建心跳请求。支持重载后使用自定义的心跳请求对象，填充更多参数
            var request = BuildPingRequest();

            // 如果网络不可用，直接保存到队列
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                // 如果心跳请求实现了ICloneable接口，可以克隆一份，避免后续修改
                if (_fails.Count < MaxFails) _fails.Enqueue((request as ICloneable)?.Clone() as IPingRequest ?? request);
                return null;
            }

            IPingResponse? response = null;
            try
            {
                response = await PingAsync(request, cancellationToken).ConfigureAwait(false);
                if (response != null)
                {
                    // 由服务器改变采样频率
                    if (response.Period > 0 && _timerPing != null) _timerPing.Period = response.Period * 1000;

                    FixTime(response.Time, response.ServerTime);

                    // 更新令牌。即将过期时，服务端会返回新令牌
                    if (!response.Token.IsNullOrEmpty()) SetToken(response.Token);

                    // 心跳响应携带的命令，推送到队列
                    if (response.Commands != null && response.Commands.Length > 0)
                    {
                        foreach (var model in response.Commands)
                        {
                            await ReceiveCommand(model, "Pong", cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch
            {
                // 失败时保存到队列
                //if (_fails.Count < MaxFails) _fails.Enqueue(request);
                if (_fails.Count < MaxFails) _fails.Enqueue((request as ICloneable)?.Clone() as IPingRequest ?? request);

                throw;
            }

            // 上报正常，处理历史，失败则丢弃
            while (_fails.TryDequeue(out var info))
            {
                await PingAsync(info, cancellationToken).ConfigureAwait(false);
            }

            return response;
        }
        catch (Exception ex)
        {
            //span?.SetError(ex, null);

            var ex2 = ex.GetTrue();
            if (ex2 is ApiException aex && (aex.Code == ApiCode.Unauthorized || aex.Code == ApiCode.Forbidden))
            {
                Status = LoginStatus.Ready;
                if (Features.HasFlag(Features.Login))
                {
                    Log?.Debug("重新登录，因心跳失败：{0}", ex.Message);
                    await Login(nameof(Ping), cancellationToken).ConfigureAwait(false);
                }

                return null;
            }

            Log?.Debug("心跳异常 {0}", ex.GetTrue().Message);

            throw;
        }
    }

    /// <summary>创建心跳请求。支持重载后使用自定义的心跳请求对象</summary>
    /// <remarks>
    /// 用户可以重载此方法，返回自定义的心跳请求对象，用于支持更多的心跳参数。
    /// 也可以调用基类BuildPingRequest后得到基本参数，然后填充自己的心跳请求对象。
    /// 还可以直接使用自己的心跳请求对象，调用FillPingRequest填充基本参数。
    /// </remarks>
    public virtual IPingRequest BuildPingRequest()
    {
        Init();

        var request = GetService<IPingRequest>() ?? new PingRequest();
        FillPingRequest(request);

        return request;
    }

    /// <summary>填充心跳请求</summary>
    /// <param name="request"></param>
    protected virtual void FillPingRequest(IPingRequest request)
    {
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
    }

    /// <summary>发起心跳异步请求。由Ping内部调用</summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected virtual Task<IPingResponse?> PingAsync(IPingRequest request, CancellationToken cancellationToken) => InvokeAsync<IPingResponse>(Actions[Features.Ping], request, cancellationToken);
    #endregion

    #region 升级更新
    private async Task CheckUpgrade(Object? data)
    {
        if (!NetworkInterface.GetIsNetworkAvailable()) return;

        await Upgrade(null).ConfigureAwait(false);
    }

    private async Task<String?> ReceiveUpgrade(String? arguments)
    {
        // 参数作为通道
        var channel = arguments;
        var rs = await Upgrade(channel).ConfigureAwait(false);
        if (rs == null) return "没有可用更新！";

        return $"成功更新到[{rs.Version}]";
    }

    private String? _lastVersion;
    /// <summary>获取更新信息。如有更新，则下载解压覆盖并重启应用</summary>
    /// <returns></returns>
    public virtual async Task<IUpgradeInfo?> Upgrade(String? channel, CancellationToken cancellationToken = default)
    {
        using var span = Tracer?.NewSpan(nameof(Upgrade));
        WriteLog("检查更新");

        // 清理旧版备份文件
        var ug = new Upgrade { Log = Log };
        ug.DeleteBackup(".");

        // 调用接口查询思否存在更新信息
        var info = await UpgradeAsync(channel, cancellationToken).ConfigureAwait(false);
        if (info == null || info.Version == _lastVersion) return info;

        // _lastVersion避免频繁更新同一个版本
        WriteLog("发现更新：{0}", info.ToJson(true));
        this.WriteInfoEvent("Upgrade", $"准备从[{_lastVersion}]更新到[{info.Version}]，开始下载 {info.Source}");

        try
        {
            // 下载文件包
            ug.Url = BuildUrl(info.Source!);
            await ug.Download(cancellationToken).ConfigureAwait(false);

            // 检查文件完整性
            if (!info.FileHash.IsNullOrEmpty() && !ug.CheckFileHash(info.FileHash))
            {
                this.WriteInfoEvent("Upgrade", "下载完成，哈希校验失败");
            }
            else
            {
                this.WriteInfoEvent("Upgrade", "下载完成，准备解压文件");
                if (!ug.Extract())
                {
                    this.WriteInfoEvent("Upgrade", "解压失败");
                }
                else
                {
                    if (info is UpgradeInfo info2 && !info2.Preinstall.IsNullOrEmpty())
                    {
                        this.WriteInfoEvent("Upgrade", "执行预安装脚本");

                        ug.Run(info2.Preinstall);
                    }

                    this.WriteInfoEvent("Upgrade", "解压完成，准备覆盖文件");

                    // 执行更新，解压缩覆盖文件
                    var rs = ug.Update();

                    // 执行更新后命令
                    if (rs && !info.Executor.IsNullOrEmpty()) ug.Run(info.Executor);
                    _lastVersion = info.Version;

                    // 强制更新时，马上重启
                    if (rs && info.Force) Restart(ug);
                }
            }
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            //Log?.Error(ex.ToString());
            this.WriteErrorEvent("Upgrade", $"更新失败！{ex.Message}");
            throw;
        }

        return info;
    }

    /// <summary>更新完成，重启自己</summary>
    /// <param name="upgrade"></param>
    protected virtual void Restart(Upgrade upgrade)
    {
        var asm = Assembly.GetEntryAssembly();
        if (asm == null) return;

        var name = asm.GetName().Name;
        if (name.IsNullOrEmpty()) return;

        // 重新拉起进程。对于大多数应用，都是拉起新进程，然后退出当前进程；对于星尘代理，通过新进程来重启服务。
        var args = Environment.GetCommandLineArgs();
        if (args == null || args.Length == 0) args = new String[1];
        args[0] = "-upgrade";
        var gs = args.Join(" ");

        // 执行重启，如果失败，延迟后再次尝试
        var rs = upgrade.Run(name, gs, 3_000);
        if (!rs)
        {
            var delay = 3_000;
            this.WriteInfoEvent("Upgrade", $"拉起新进程失败，延迟{delay}ms后重试");
            Thread.Sleep(delay);
            rs = upgrade.Run(name, gs, 1_000);
        }

        if (rs)
        {
            var pid = Process.GetCurrentProcess().Id;
            this.WriteInfoEvent("Upgrade", "强制更新完成，新进程已拉起，准备退出当前进程！PID=" + pid);

            upgrade.KillSelf();
        }
        else
        {
            this.WriteInfoEvent("Upgrade", "强制更新完成，但拉起新进程失败");
        }
    }

    /// <summary>放弃更新异步请求。由Upgrade内部调用</summary>
    /// <returns></returns>
    protected virtual async Task<IUpgradeInfo?> UpgradeAsync(String? channel, CancellationToken cancellationToken)
    {
        if (_client is ApiHttpClient)
            return await GetAsync<IUpgradeInfo>(Actions[Features.Upgrade], new { channel }, cancellationToken).ConfigureAwait(false);

        return await InvokeAsync<IUpgradeInfo>(Actions[Features.Upgrade], new { channel }, cancellationToken).ConfigureAwait(false);
    }
    #endregion

    #region 下行通知
    private TimerX? _timerPing;
    private TimerX? _timerUpgrade;
    /// <summary>开始心跳定时器</summary>
    protected virtual void StartTimer()
    {
        if (_timerPing == null && (Features.HasFlag(Features.Ping) || Features.HasFlag(Features.Notify)))
        {
            lock (this)
            {
                _timerPing ??= new TimerX(DoPing, null, 1000, 60_000) { Async = true };
            }
        }

        if (_timerUpgrade == null && Features.HasFlag(Features.Upgrade))
        {
            lock (this)
            {
                _timerUpgrade ??= new TimerX(CheckUpgrade, null, 5_000, 600_000) { Async = true };
            }
        }
    }

    /// <summary>停止心跳定时器</summary>
    protected virtual void StopTimer()
    {
        _timerPing.TryDispose();
        _timerPing = null;
        _timerUpgrade.TryDispose();
        _timerUpgrade = null;
        _eventTimer.TryDispose();
        _eventTimer = null;

        _ws.TryDispose();
        _ws = null;
    }

    private async Task DoPing(Object state)
    {
        using var span = Tracer?.NewSpan(Name + "Ping");
        try
        {
            await OnPing(state).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            Log?.Debug("{0}", ex);
        }
    }

    private WsChannel? _ws;
    /// <summary>定时心跳</summary>
    /// <param name="state"></param>
    /// <returns></returns>
    protected virtual async Task OnPing(Object state)
    {
        if (Features.HasFlag(Features.Ping)) await Ping().ConfigureAwait(false);

        if (_client is ApiHttpClient http && Features.HasFlag(Features.Notify))
        {
            // 非NetCore平台，使用自研轻量级WebSocket
#if NETCOREAPP
            _ws ??= new WsChannelCore(this);
#else
            _ws ??= new WsChannel(this);
#endif
            if (_ws != null) await _ws.ValidWebSocket(http).ConfigureAwait(false);
        }
    }

    /// <summary>接收命令，分发调用指定委托</summary>
    /// <remarks>
    /// 命令处理流程中，会对命令进行去重，避免重复执行。
    /// 其次判断命令是否已经过期，如果已经过期则取消执行。
    /// 还支持定时执行，延迟执行。
    /// </remarks>
    /// <param name="model"></param>
    /// <param name="source"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public virtual async Task<CommandReplyModel?> ReceiveCommand(CommandModel model, String source, CancellationToken cancellationToken = default)
    {
        if (model == null) return null;

        // 去重，避免命令被重复执行
        if (model.Id > 0 && !_cache.Add($"cmd:{model.Id}", model, 3600)) return null;

        // 埋点，建立调用链
        var json = model.ToJson();
        using var span = Tracer?.NewSpan("cmd:" + model.Command, json);
        if (!model.TraceId.IsNullOrEmpty()) span?.Detach(model.TraceId);
        try
        {
            // 有效期判断前把UTC转为本地时间
            var now = GetNow();
            var expire = model.Expire.ToLocalTime();
            WriteLog("[{0}] 收到命令: {1}", source, json);
            if (expire.Year < 2000 || expire > now)
            {
                // 延迟执行
                var startTime = model.StartTime.ToLocalTime();
                var ts = startTime - now;
                if (ts.TotalMilliseconds > 0)
                {
                    //TimerX.Delay(s =>
                    //{
                    //    _ = OnReceiveCommand(model, CancellationToken.None);
                    //}, (Int32)ts.TotalMilliseconds);
                    _ = Task.Run(async () =>
                    {
                        await TaskEx.Delay((Int32)ts.TotalMilliseconds).ConfigureAwait(false);
                        WriteLog("[{0}] 延迟执行: {1}", source, json);
                        await OnReceiveCommand(model, CancellationToken.None).ConfigureAwait(false);
                    }, cancellationToken);

                    var reply = new CommandReplyModel
                    {
                        Id = model.Id,
                        Status = CommandStatus.处理中,
                        Data = $"已安排计划执行 {startTime.ToFullString()}"
                    };

                    if (Features.HasFlag(Features.CommandReply))
                        await CommandReply(reply, cancellationToken).ConfigureAwait(false);

                    return reply;
                }
                else
                    return await OnReceiveCommand(model, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var reply = new CommandReplyModel { Id = model.Id, Status = CommandStatus.取消 };

                if (Features.HasFlag(Features.CommandReply))
                    await CommandReply(reply, cancellationToken).ConfigureAwait(false);

                return reply;
            }
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
        }

        return null;
    }

    /// <summary>触发收到命令的动作</summary>
    /// <param name="model"></param>
    /// <param name="cancellationToken"></param>
    protected virtual async Task<CommandReplyModel?> OnReceiveCommand(CommandModel model, CancellationToken cancellationToken)
    {
        var e = new CommandEventArgs { Model = model };
        Received?.Invoke(this, e);

        var rs = await this.ExecuteCommand(model, cancellationToken).ConfigureAwait(false);
        e.Reply ??= rs;

        if (e.Reply != null && e.Reply.Id > 0 && Features.HasFlag(Features.CommandReply))
            await CommandReply(e.Reply, cancellationToken).ConfigureAwait(false);

        return e.Reply;
    }

    /// <summary>向命令引擎发送命令，触发指定已注册动作</summary>
    /// <param name="command"></param>
    /// <param name="argument"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public virtual Task<CommandReplyModel?> SendCommand(String command, String argument, CancellationToken cancellationToken = default) => OnReceiveCommand(new CommandModel { Command = command, Argument = argument }, cancellationToken);

    /// <summary>上报命令调用结果</summary>
    /// <param name="model"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public virtual Task<Object?> CommandReply(CommandReplyModel model, CancellationToken cancellationToken = default) => InvokeAsync<Object>(Actions[Features.CommandReply], model, cancellationToken);
    #endregion

    #region 事件上报
    private readonly ConcurrentQueue<EventModel> _events = new();
    private readonly ConcurrentQueue<EventModel> _failEvents = new();
    private TimerX? _eventTimer;
    private String? _eventTraceId;

    void InitEvent() => _eventTimer ??= new TimerX(DoPostEvent, null, 3_000, 60_000) { Async = true };

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
                if (list.Count > 0) await PostEvents(list.ToArray()).ConfigureAwait(false);

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
        // 如果没有事件上报功能，直接返回
        if (!Features.HasFlag(Features.PostEvent)) return false;

        // 使用时才创建定时器
        InitEvent();

        // 记录追踪标识，上报的时候带上，尽可能让源头和下游串联起来
        _eventTraceId = DefaultSpan.Current?.TraceId;

        // 获取相对于服务器的当前时间，避免两端时间差。转为UTC毫秒，作为事件时间。
        var now = GetNow().ToUniversalTime();
        var ev = new EventModel { Time = now.ToLong(), Type = type, Name = name, Remark = remark };
        _events.Enqueue(ev);

        _eventTimer?.SetNext(1000);

        return true;
    }
    #endregion

    #region 辅助
    /// <summary>
    /// 把Url相对路径格式化为绝对路径。常用于文件下载
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    public virtual String BuildUrl(String url)
    {
        if (Client is ApiHttpClient client && !url.StartsWithIgnoreCase("http://", "https://"))
        {
            var svr = client.Services.FirstOrDefault(e => e.Name == client.Source) ?? client.Services.FirstOrDefault();
            if (svr != null && svr.Address != null)
                url = new Uri(svr.Address, url) + "";
        }

        return url;
    }

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
    public void WriteLog(String format, params Object?[] args) => Log?.Info($"[{Name}]{format}", args);
    #endregion
}