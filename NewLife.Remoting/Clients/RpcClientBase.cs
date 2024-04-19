using NewLife;
using NewLife.Log;
using NewLife.Net;
using NewLife.Remoting;
using NewLife.Remoting.Models;

namespace NewLife.Remoting.Clients;

/// <summary>Rpc版设备客户端。每个设备节点有一个客户端连接服务端</summary>
public class RpcClientBase : ClientBase
{
    #region 属性
    /// <summary>命令前缀</summary>
    public String Prefix { get; set; } = "Device/";

    private ApiClient _client = null!;
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public RpcClientBase() : base()
    {
        _client = new MyApiClient
        {
            Client = this,
            Log = XTrace.Log
        };
    }

    /// <summary>实例化</summary>
    /// <param name="urls"></param>
    public RpcClientBase(String urls) : this()
    {
        if (!urls.IsNullOrEmpty())
            _client.Servers = urls.Split(",");
    }
    #endregion

    #region 方法
    /// <summary>异步调用</summary>
    /// <param name="action">动作</param>
    /// <param name="args">参数</param>
    /// <returns></returns>
    protected override async Task<TResult> OnPostAsync<TResult>(String action, Object args) => await _client.InvokeAsync<TResult>(action, args);

    /// <summary>异步获取</summary>
    /// <param name="action">动作</param>
    /// <param name="args">参数</param>
    /// <returns></returns>
    protected override async Task<TResult> OnGetAsync<TResult>(String action, Object args) => await _client.InvokeAsync<TResult>(action, args);

    class MyApiClient : ApiClient
    {
        public ClientBase Client { get; set; }

        protected override async Task<Object> OnLoginAsync(ISocketClient client, Boolean force) => await InvokeWithClientAsync<Object>(client, "Device/Login", Client.BuildLoginRequest());
    }
    #endregion

    #region 登录
    /// <summary>登录</summary>
    /// <returns></returns>
    public override async Task<LoginResponse?> Login()
    {
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

        var rs = await _client.InvokeAsync<LoginResponse>(Prefix + "Login", request);

        // 登录后设置用于用户认证的token
        _client.Token = rs?.Token;

        return rs;
    }

    /// <summary>注销</summary>
    /// <returns></returns>
    protected override async Task<LogoutResponse?> LogoutAsync(String reason)
    {
        var rs = await _client.InvokeAsync<LogoutResponse>(Prefix + "Logout", new { reason });

        // 更新令牌
        _client.Token = rs?.Token;

        return rs;
    }
    #endregion

    #region 心跳
    /// <summary>心跳</summary>
    /// <returns></returns>
    public override async Task<PingResponse?> Ping()
    {
        var rs = await base.Ping();

        // 令牌
        if (rs != null && rs.Token.IsNullOrEmpty())
            _client.Token = rs.Token;

        return rs;
    }
    /// <summary>心跳</summary>
    /// <param name="inf"></param>
    /// <returns></returns>
    protected override async Task<PingResponse?> PingAsync(PingRequest inf) => await _client.InvokeAsync<PingResponse>(Prefix + "Ping", inf);
    #endregion

    #region 长连接
    #endregion

    #region 更新
    /// <summary>更新</summary>
    /// <returns></returns>
    protected override async Task<UpgradeInfo> UpgradeAsync() => await _client.InvokeAsync<UpgradeInfo>(Prefix + "Upgrade");
    #endregion

    #region 辅助
    #endregion
}