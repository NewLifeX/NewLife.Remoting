using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.NetworkInformation;
using NewLife;
using NewLife.Caching;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Models;
using NewLife.Threading;

#if NETCOREAPP
using System.Net.WebSockets;
using WebSocket = System.Net.WebSockets.WebSocket;
#endif

namespace NewLife.Remoting.Clients;

/// <summary>Http版设备客户端。每个设备节点有一个客户端连接服务端</summary>
public class HttpClientBase : ClientBase
{
    #region 属性
    /// <summary>命令前缀</summary>
    public String Prefix { get; set; } = "Device/";

    /// <summary>
    /// Http客户端，可用于给其它服务提供通信链路，自带令牌
    /// </summary>
    public ApiHttpClient Client => _client;

    private MyHttpClient _client = null!;
    private ICache _cache = new MemoryCache();
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public HttpClientBase() : base()
    {
        _client = new MyHttpClient
        {
            Client = this,
            Log = XTrace.Log
        };
    }

    /// <summary>实例化</summary>
    /// <param name="urls"></param>
    public HttpClientBase(String urls) : this()
    {
        if (!urls.IsNullOrEmpty())
        {
            var ss = urls.Split(",");
            for (var i = 0; i < ss.Length; i++)
                _client.Add("service" + (i + 1), new Uri(ss[i]));
        }
    }
    #endregion

    #region 方法
    public override async Task<TResult> OnInvokeAsync<TResult>(String action, Object? args, CancellationToken cancellationToken)
    {
        var method = HttpMethod.Post;
        if (action.StartsWithIgnoreCase("Get") || action.Contains("Get"))
            method = HttpMethod.Get;

        return await _client.InvokeAsync<TResult>(method, action, args);
    }

    class MyHttpClient : ApiHttpClient
    {
        public ClientBase Client { get; set; }

        public Service Current { get; private set; }

        /// <summary>远程调用拦截，支持重新登录</summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="method"></param>
        /// <param name="action"></param>
        /// <param name="args"></param>
        /// <param name="onRequest"></param>
        /// <param name="cancellationToken">取消通知</param>
        /// <returns></returns>
        public override async Task<TResult> InvokeAsync<TResult>(HttpMethod method, String action, Object args = null, Action<HttpRequestMessage> onRequest = null, CancellationToken cancellationToken = default)
        {
            var needLogin = !Client.Logined && !action.EqualIgnoreCase(Prefix + "Login", "Node/Logout");
            if (needLogin)
                await Client.Login();

            try
            {
                return await base.InvokeAsync<TResult>(method, action, args, onRequest, cancellationToken);
            }
            catch (Exception ex)
            {
                var ex2 = ex.GetTrue();
                if (ex2 is ApiException aex && (aex.Code == 402 || aex.Code == 403) && !action.EqualIgnoreCase(Prefix + "Login", "Device/Logout"))
                {
                    XTrace.WriteException(ex);
                    XTrace.WriteLine("重新登录！");
                    await Client.Login();

                    return await base.InvokeAsync<TResult>(method, action, args, onRequest, cancellationToken);
                }

                throw;
            }
        }

        protected override Service GetService() => Current = base.GetService();
    }
    #endregion

    #region 登录
    /// <summary>登录</summary>
    /// <returns></returns>
    public override async Task<LoginResponse?> Login()
    {
        _client.Tracer = Tracer;
        _client.Token = null;

        var rs = await base.Login();

        _client.Token = rs?.Token;

        return rs;
    }

    /// <summary>登录</summary>
    /// <param name="request">登录信息</param>
    /// <returns></returns>
    protected override async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        // 登录前清空令牌，避免服务端使用上一次信息
        _client.Token = null;

        var rs = await _client.PostAsync<LoginResponse>(Prefix + "Login", request);

        // 登录后设置用于用户认证的token
        _client.Token = rs?.Token;

        return rs;
    }

    /// <summary>注销</summary>
    /// <returns></returns>
    protected override async Task<LogoutResponse?> LogoutAsync(String reason)
    {
        var rs = await _client.GetAsync<LogoutResponse>(Prefix + "Logout", new { reason });

        // 更新令牌
        _client.Token = rs?.Token;

        return rs;
    }
    #endregion

    #region 心跳
    /// <summary>心跳后建立WebSocket长连接</summary>
    /// <param name="state"></param>
    /// <returns></returns>
    protected override async Task OnPing(Object state)
    {
        DefaultSpan.Current = null;
        using var span = Tracer?.NewSpan("DevicePing");
        try
        {
            var rs = await Ping();

            // 令牌
            if (rs is PingResponse pr && !pr.Token.IsNullOrEmpty())
                _client.Token = pr.Token;

#if NETCOREAPP
            var svc = _client.Current;
            if (svc == null) return;

            // 使用过滤器内部token，因为它有过期刷新机制
            var token = _client.Token;
            if (_client.Filter is NewLife.Http.TokenHttpFilter thf) token = thf.Token?.AccessToken;
            span?.AppendTag($"svc={svc.Address} Token=[{token?.Length}]");

            if (token.IsNullOrEmpty()) return;

            if (_websocket != null && _websocket.State == WebSocketState.Open)
            {
                try
                {
                    // 在websocket链路上定时发送心跳，避免长连接被断开
                    var str = "Ping";
                    await _websocket.SendAsync(new ArraySegment<Byte>(str.GetBytes()), WebSocketMessageType.Text, true, default);
                }
                catch (Exception ex)
                {
                    span?.SetError(ex, null);
                    WriteLog("{0}", ex);
                }
            }

            if (_websocket == null || _websocket.State != WebSocketState.Open)
            {
                var url = svc.Address.ToString().Replace("http://", "ws://").Replace("https://", "wss://");
                var uri = new Uri(new Uri(url), "/Device/Notify");

                using var span2 = Tracer?.NewSpan("WebSocketConnect", uri + "");

                var client = new ClientWebSocket();
                client.Options.SetRequestHeader("Authorization", "Bearer " + token);

                span?.AppendTag($"WebSocket.Connect {uri}");
                await client.ConnectAsync(uri, default);

                _websocket = client;

                _source = new CancellationTokenSource();
                _ = Task.Run(() => DoPull(client, _source.Token));
            }
#endif
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            Log?.Debug("{0}", ex);
        }
    }

    /// <summary>心跳</summary>
    /// <param name="inf"></param>
    /// <returns></returns>
    protected override async Task<PingResponse?> PingAsync(PingRequest inf) => await _client.PostAsync<PingResponse>(Prefix + "Ping", inf);
    #endregion

    #region 长连接
    /// <summary>停止心跳定时器</summary>
    protected override void StopTimer()
    {
        base.StopTimer();

#if NETCOREAPP
        _source?.Cancel();
        try
        {
            if (_websocket != null && _websocket.State == WebSocketState.Open)
                _websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "finish", default);
        }
        catch { }

        _websocket = null;
#endif
    }

#if NETCOREAPP
    private WebSocket _websocket;
    private CancellationTokenSource _source;
    private async Task DoPull(WebSocket socket, CancellationToken cancellationToken)
    {
        DefaultSpan.Current = null;
        try
        {
            var buf = new Byte[4 * 1024];
            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var data = await socket.ReceiveAsync(new ArraySegment<Byte>(buf), cancellationToken);
                var txt = buf.ToStr(null, 0, data.Count);
                if (txt.StartsWithIgnoreCase("Pong"))
                {
                }
                else
                {
                    var model = txt.ToJsonEntity<ServiceModel>();
                    if (model != null) await ReceiveCommand(model);
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }

        if (socket.State == WebSocketState.Open) await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "finish", default);
    }
#endif

    async Task ReceiveCommand(ServiceModel model)
    {
        if (model == null) return;

        // 去重，避免命令被重复执行
        if (!_cache.Add($"cmd:{model.Id}", model, 3600)) return;

        // 建立追踪链路
        using var span = Tracer?.NewSpan("service:" + model.Name, model);
        span?.Detach(model.TraceId);
        try
        {
            //todo 有效期判断可能有隐患，现在只是假设服务器和客户端在同一个时区，如果不同，可能会出现问题
            //WriteLog("Got Service: {0}", model.ToJson());
            var now = GetNow();
            if (model.Expire.Year < 2000 || model.Expire > now)
            {
                // 延迟执行
                var ts = model.StartTime - now;
                if (ts.TotalMilliseconds > 0)
                {
                    TimerX.Delay(s =>
                    {
                        _ = OnReceiveCommand(model);
                    }, (Int32)ts.TotalMilliseconds);

                    var reply = new ServiceReplyModel
                    {
                        Id = model.Id,
                        Status = ServiceStatus.处理中,
                        Data = $"已安排计划执行 {model.StartTime.ToFullString()}"
                    };
                    await ServiceReply(reply);
                }
                else
                    await OnReceiveCommand(model);
            }
            else
            {
                var rs = new ServiceReplyModel { Id = model.Id, Status = ServiceStatus.取消 };
                await ServiceReply(rs);
            }
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
        }
    }
    #endregion

    #region 上报
    /// <summary>批量上报事件</summary>
    /// <param name="events"></param>
    /// <returns></returns>
    public override async Task<Int32> PostEvents(params EventModel[] events) => await _client.PostAsync<Int32>(Prefix + "PostEvents", events);

    /// <summary>上报服务调用结果</summary>
    /// <param name="model"></param>
    /// <returns></returns>
    public override async Task<Object?> CommandReply(CommandReplyModel model) => await _client.PostAsync<Object>(Prefix + "CommandReply", model);
    #endregion

    #region 更新
    /// <summary>更新</summary>
    /// <returns></returns>
    protected override async Task<UpgradeInfo> UpgradeAsync() => await _client.GetAsync<UpgradeInfo>(Prefix + "Upgrade");
    #endregion

    #region 辅助
    #endregion
}