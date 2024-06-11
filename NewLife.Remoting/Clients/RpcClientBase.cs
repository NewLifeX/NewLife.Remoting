using NewLife.Log;
using NewLife.Net;

namespace NewLife.Remoting.Clients;

/// <summary>Rpc版应用客户端基类</summary>
public class RpcClientBase : ClientBase
{
    #region 属性
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
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public override async Task<TResult> OnInvokeAsync<TResult>(String action, Object? args, CancellationToken cancellationToken)
    {
        return await _client.InvokeAsync<TResult>(action, args, cancellationToken);
    }

    class MyApiClient : ApiClient
    {
        public ClientBase Client { get; set; } = null!;

        protected override async Task<Object?> OnLoginAsync(ISocketClient client, Boolean force) => await InvokeWithClientAsync<Object>(client, Client.Prefix + "/Login", Client.BuildLoginRequest());
    }

    /// <summary>设置令牌。派生类可重定义逻辑</summary>
    /// <param name="token"></param>
    protected override void SetToken(String? token)
    {
        base.SetToken(token);

        if (_client != null) _client.Token = token;
    }
    #endregion
}