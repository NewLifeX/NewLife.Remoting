using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using NewLife;
using NewLife.Caching;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Security;
using NewLife.Serialization;
using NewLife.Threading;

namespace NewLife.Remoting.Clients;

/// <summary>设备客户端。每个设备节点有一个客户端连接服务端</summary>
public abstract class ClientBase : DisposeBase, ICommandClient, IEventProvider, ITracerFeature, ILogFeature
{
    #region 属性
    /// <summary>应用标识</summary>
    public String? Code { get; set; }

    /// <summary>应用密钥</summary>
    public String? Secret { get; set; }

    /// <summary>密码提供者</summary>
    public IPasswordProvider PasswordProvider { get; set; } = new SaltPasswordProvider { Algorithm = "md5" };

    /// <summary>是否已登录</summary>
    public Boolean Logined { get; set; }

    /// <summary>登录完成后触发</summary>
    public event EventHandler<LoginEventArgs>? OnLogined;

    /// <summary>请求到服务端并返回的延迟时间。单位ms</summary>
    public Int32 Delay { get; set; }

    /// <summary>最大失败数。超过该数时，新的数据将被抛弃，默认120</summary>
    public Int32 MaxFails { get; set; } = 120;

    private readonly ConcurrentDictionary<String, Delegate> _commands = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>命令集合</summary>
    public IDictionary<String, Delegate> Commands => _commands;

    /// <summary>收到命令时触发</summary>
    public event EventHandler<CommandEventArgs>? Received;

    /// <summary>协议版本</summary>
    private String _version;
    private TimeSpan _span;
    private readonly ConcurrentQueue<PingRequest> _fails = new();
    private readonly ICache _cache = new MemoryCache();
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public ClientBase()
    {
        _version = Assembly.GetExecutingAssembly().GetName().Version + "";
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
    /// <summary>异步调用</summary>
    /// <param name="action">动作</param>
    /// <param name="args">参数</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public abstract Task<TResult> OnInvokeAsync<TResult>(String action, Object? args, CancellationToken cancellationToken);

    /// <summary>远程调用拦截，支持重新登录</summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="action"></param>
    /// <param name="args"></param>
    /// <param name="cancellationToken">取消通知</param>
    /// <returns></returns>
    [return: MaybeNull]
    public virtual async Task<TResult> InvokeAsync<TResult>(String action, Object? args = null, CancellationToken cancellationToken = default)
    {
        var needLogin = !Logined && !action.EqualIgnoreCase("Node/Login", "Node/Logout");
        if (needLogin)
            await Login();

        try
        {
            return await OnInvokeAsync<TResult>(action, args, cancellationToken);
        }
        catch (Exception ex)
        {
            var ex2 = ex.GetTrue();
            if (Logined && ex2 is ApiException aex && (aex.Code == 401 || aex.Code == 403) && !action.EqualIgnoreCase("Node/Login", "Node/Logout"))
            {
                Log?.Debug("{0}", ex);
                WriteLog("重新登录！");
                await Login();

                return await OnInvokeAsync<TResult>(action, args, cancellationToken);
            }

            throw;
        }
    }

    /// <summary>获取相对于服务器的当前时间，避免两端时间差</summary>
    /// <returns></returns>
    public DateTime GetNow() => DateTime.Now.Add(_span);
    #endregion

    #region 登录
    /// <summary>登录</summary>
    /// <returns></returns>
    public virtual async Task<LoginResponse?> Login()
    {
        using var span = Tracer?.NewSpan(nameof(Login), Code);
        WriteLog("登录：{0}", Code);
        try
        {
            var request = BuildLoginRequest();

            Logined = false;

            var rs = await LoginAsync(request);
            if (rs == null) return null;

            if (!rs.Code.IsNullOrEmpty() && !rs.Secret.IsNullOrEmpty())
            {
                WriteLog("下发密钥：{0}/{1}", rs.Code, rs.Secret);
                Code = rs.Code;
                Secret = rs.Secret;
            }

            FixTime(rs.Time, rs.Time);

            Logined = true;

            OnLogined?.Invoke(this, new LoginEventArgs { Request = request, Response = rs });

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
    public virtual LoginRequest BuildLoginRequest()
    {
        var info = new LoginRequest
        {
            Code = Code,
            Secret = Secret.IsNullOrEmpty() ? null : PasswordProvider.Hash(Secret),
        };

        return info;
    }

    /// <summary>注销</summary>
    /// <param name="reason"></param>
    /// <returns></returns>
    public virtual async Task<LogoutResponse?> Logout(String reason)
    {
        if (!Logined) return null;

        using var span = Tracer?.NewSpan(nameof(Logout), reason);
        WriteLog("注销：{0} {1}", Code, reason);

        try
        {
            var rs = await LogoutAsync(reason);

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
    protected abstract Task<LoginResponse?> LoginAsync(LoginRequest request);

    /// <summary>注销</summary>
    /// <returns></returns>
    protected abstract Task<LogoutResponse?> LogoutAsync(String reason);
    #endregion

    #region 心跳
    /// <summary>心跳</summary>
    /// <returns></returns>
    public virtual async Task<PingResponse?> Ping()
    {
        if (Tracer != null) DefaultSpan.Current = null;
        using var span = Tracer?.NewSpan(nameof(Ping));
        try
        {
            var request = BuildPingRequest();

            var rs = await PingAsync(request);
            if (rs != null)
            {
                // 由服务器改变采样频率
                if (rs.Period > 0 && _timer != null) _timer.Period = rs.Period * 1000;

                FixTime(rs.Time, rs.ServerTime);
            }

            return rs;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);

            var ex2 = ex.GetTrue();
            if (ex2 is ApiException aex && (aex.Code == 402 || aex.Code == 403))
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
    public PingRequest BuildPingRequest()
    {
        var request = new PingRequest
        {
            Uptime = Environment.TickCount / 1000,
            Time = DateTime.UtcNow.ToLong(),
            Delay = Delay,
        };
        // 开始时间 Environment.TickCount 很容易溢出，导致开机24天后变成负数。
        // 后来在 netcore3.0 增加了Environment.TickCount64
        // 现在借助 Stopwatch 来解决
        if (Stopwatch.IsHighResolution) request.Uptime = (Int32)(Stopwatch.GetTimestamp() / Stopwatch.Frequency);

        return request;
    }

    /// <summary>心跳</summary>
    /// <param name="inf"></param>
    /// <returns></returns>
    protected abstract Task<PingResponse?> PingAsync(PingRequest inf);

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
                    _timer = new TimerX(OnPing, null, 3_000, 60_000, "Device") { Async = true };
                    _timerUpgrade = new TimerX(s => Upgrade(), null, 5_000, 600_000, "Device") { Async = true };
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
    }

    /// <summary>定时心跳</summary>
    /// <param name="state"></param>
    /// <returns></returns>
    protected virtual Task OnPing(Object state) => Ping();

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
    #endregion

    #region 上报
    private readonly ConcurrentQueue<EventModel> _events = new();
    private readonly ConcurrentQueue<EventModel> _failEvents = new();
    private TimerX? _eventTimer;
    private String? _eventTraceId;

    /// <summary>批量上报事件</summary>
    /// <param name="events"></param>
    /// <returns></returns>
    public abstract Task<Int32> PostEvents(params EventModel[] events);

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
        // 记录追踪标识，上报的时候带上，尽可能让源头和下游串联起来
        _eventTraceId = DefaultSpan.Current?.ToString();

        var now = GetNow().ToUniversalTime();
        var ev = new EventModel { Time = now.ToLong(), Type = type, Name = name, Remark = remark };
        _events.Enqueue(ev);

        _eventTimer?.SetNext(1000);

        return true;
    }

    /// <summary>上报命令调用结果</summary>
    /// <param name="model"></param>
    /// <returns></returns>
    public abstract Task<Object?> CommandReply(CommandReplyModel model);
    #endregion

    #region 更新
    private String _lastVersion;
    /// <summary>获取更新信息</summary>
    /// <returns></returns>
    public async Task<UpgradeInfo> Upgrade()
    {
        using var span = Tracer?.NewSpan(nameof(Upgrade));
        WriteLog("检查更新");

        // 清理
        var ug = new Upgrade { Log = XTrace.Log };
        ug.DeleteBackup(".");

        var ur = await UpgradeAsync();
        if (ur != null && ur.Version != _lastVersion)
        {
            WriteLog("发现更新：{0}", ur.ToJson(true));

            ug.Url = ur.Source;
            await ug.Download();

            // 检查文件完整性
            if (ur.FileHash.IsNullOrEmpty() || ug.CheckFileHash(ur.FileHash))
            {
                // 执行更新，解压缩覆盖文件
                var rs = ug.Update();
                if (rs && !ur.Executor.IsNullOrEmpty()) ug.Run(ur.Executor);
                _lastVersion = ur.Version;

                // 强制更新时，马上重启
                if (rs && ur.Force)
                {
                    // 重新拉起进程
                    rs = ug.Run("dotnet", "IoTClient.dll -upgrade");

                    if (rs) ug.KillSelf();
                }
            }
        }

        return ur;
    }

    /// <summary>更新</summary>
    /// <returns></returns>
    protected abstract Task<UpgradeInfo> UpgradeAsync();
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