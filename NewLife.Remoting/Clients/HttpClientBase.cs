using NewLife.Caching;
using NewLife.Log;
using NewLife.Remoting.Models;
using NewLife.Serialization;

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

    private readonly ApiHttpClient _client;
    private readonly ICache _cache = new MemoryCache();
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public HttpClientBase() : base()
    {
        _client = new ApiHttpClient
        {
            Log = XTrace.Log
        };
    }

    /// <summary>实例化</summary>
    /// <param name="urls"></param>
    public HttpClientBase(String urls) : this() => AddServices(urls);

    /// <summary>新增服务点</summary>
    /// <param name="name"></param>
    /// <param name="url"></param>
    public void AddService(String name, String url) => _client.Add(name, new Uri(url));

    /// <summary>根据服务端地址列表新增服务点集合</summary>
    /// <param name="urls"></param>
    public void AddServices(String urls)
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
    public override Task<TResult> OnInvokeAsync<TResult>(String action, Object? args, CancellationToken cancellationToken)
    {
        _client.Tracer = Tracer;
        _client.Log = Log;

        if (args == null || action.StartsWithIgnoreCase("Get") || action.Contains("/Get"))
            return _client.GetAsync<TResult>(action, args);
        else
            return _client.PostAsync<TResult>(action, args);
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
                await OnReceive(txt);
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }

        if (socket.State == WebSocketState.Open) await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "finish", default);
    }
#endif

    /// <summary>收到服务端主动下发消息。默认转为CommandModel命令处理</summary>
    /// <param name="message"></param>
    /// <returns></returns>
    protected virtual async Task OnReceive(String message)
    {
        if (message.StartsWithIgnoreCase("Pong"))
        {
        }
        else
        {
            var model = message.ToJsonEntity<CommandModel>();
            if (model != null) await ReceiveCommand(model, "WebSocket");
        }
    }
    #endregion
}