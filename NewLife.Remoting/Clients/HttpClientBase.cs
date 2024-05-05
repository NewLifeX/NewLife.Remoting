using NewLife.Caching;
using NewLife.Log;
using NewLife.Remoting.Models;
using NewLife.Threading;
using NewLife.Serialization;
using System.Net.Http;
using System.Diagnostics.CodeAnalysis;

#if NETCOREAPP
using System.Net.WebSockets;
using WebSocket = System.Net.WebSockets.WebSocket;
#endif

namespace NewLife.Remoting.Clients;

/// <summary>Http版应用客户端基类</summary>
public class HttpClientBase : ClientBase
{
    #region 属性
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
            {
                _client.Add("service" + (i + 1), new Uri(ss[i]));
            }
        }
    }
    #endregion

    #region 方法
    /// <summary>异步调用</summary>
    /// <param name="action">动作</param>
    /// <param name="args">参数</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [return: MaybeNull]
    public override Task<TResult> OnInvokeAsync<TResult>(String action, Object? args, CancellationToken cancellationToken)
    {
        if (action.StartsWithIgnoreCase("Get") || action.Contains("/Get"))
            return _client.GetAsync<TResult>(action, args);
        else
            return _client.PostAsync<TResult>(action, args);
    }

    class MyHttpClient : ApiHttpClient
    {
        public HttpClientBase Client { get; set; } = null!;

        public Service? Current { get; private set; }

        protected override Service GetService() => Current = base.GetService();
    }

    /// <summary>设置令牌。派生类可重定义逻辑</summary>
    /// <param name="token"></param>
    protected override void SetToken(String? token)
    {
        base.SetToken(token);

        if (_client != null) _client.Token = token;
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

        var rs = await base.LoginAsync(request);

        // 登录后设置用于用户认证的token
        _client.Token = rs?.Token;

        return rs;
    }

    /// <summary>注销</summary>
    /// <returns></returns>
    protected override async Task<LogoutResponse?> LogoutAsync(String reason)
    {
        var rs = await base.LogoutAsync(reason);

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
    private WebSocket? _websocket;
    private CancellationTokenSource? _source;
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
                    var model = txt.ToJsonEntity<CommandModel>();
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

    async Task ReceiveCommand(CommandModel model)
    {
        if (model == null) return;

        // 去重，避免命令被重复执行
        if (!_cache.Add($"cmd:{model.Id}", model, 3600)) return;

        // 建立追踪链路
        using var span = Tracer?.NewSpan("cmd:" + model.Command, model);
        if (model.TraceId != null) span?.Detach(model.TraceId);
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

                    var reply = new CommandReplyModel
                    {
                        Id = model.Id,
                        Status = CommandStatus.处理中,
                        Data = $"已安排计划执行 {model.StartTime.ToFullString()}"
                    };
                    await CommandReply(reply);
                }
                else
                    await OnReceiveCommand(model);
            }
            else
            {
                var rs = new CommandReplyModel { Id = model.Id, Status = CommandStatus.取消 };
                await CommandReply(rs);
            }
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
        }
    }
    #endregion
}