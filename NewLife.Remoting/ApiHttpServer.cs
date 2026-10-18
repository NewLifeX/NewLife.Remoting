using NewLife.Http;
using NewLife.Log;
using NewLife.Model;
using NewLife.Net;
using NewLife.Remoting.Http;
using NewLife.Serialization;

namespace NewLife.Remoting;

/// <summary>Http协议服务端。基于 NewLife.Core 的 <see cref="HttpServer"/> 承载 HTTP 通道</summary>
/// <remarks>
/// <para>拆端口设计：SRMP 由 <see cref="ApiNetServer"/> 独占主端口，HTTP 使用独立端口（见 <see cref="ApiServer.HttpPort"/>）。</para>
/// <para>帧层、连接与路由完全复用 Core 的 HttpServer，本类只把请求桥接给 <see cref="ApiServer"/> 的动作分发。</para>
/// <para>本类不承载 WebSocket：SRMP-over-WS 已取消，WS 握手请求会被 <see cref="ApiHttpHandler"/> 明确拒绝。</para>
/// </remarks>
class ApiHttpServer : DisposeBase, IApiServer
{
    #region 属性
    /// <summary>名称</summary>
    public String Name { get; set; } = "Http";

    /// <summary>主机</summary>
    public IApiHost Host { get; set; } = null!;

    /// <summary>服务提供者</summary>
    public IServiceProvider? ServiceProvider { get; set; }

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>APM追踪</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>地址重用，主要应用于网络服务器重启交替</summary>
    public Boolean ReuseAddress { get; set; }

    /// <summary>端口。启动前设置，0 表示系统分配可用端口；启动后读取实际端口</summary>
    public Int32 Port { get => Server.Port; set => Server.Port = value; }

    /// <summary>是否正在工作</summary>
    public Boolean Active => Server.Active;

    /// <summary>Http服务器</summary>
    public HttpServer Server { get; } = new();

    /// <summary>当前服务器所有会话</summary>
    /// <remarks>HTTP 为请求-响应模型，不维护长连接会话集合，返回空数组</remarks>
    public IApiSession[] AllSessions => [];

    /// <summary>Http编码器。会话级使用，按 URI/表单解析参数，与 SRMP 的主编码器各自独立</summary>
    public HttpEncoder? Encoder { get; private set; }
    #endregion

    #region 构造
    /// <summary>销毁</summary>
    /// <param name="disposing">是否由托管代码显示释放</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        if (disposing) Server.TryDispose();
    }
    #endregion

    #region 方法
    /// <summary>初始化</summary>
    /// <param name="config">配置</param>
    /// <param name="host">主机</param>
    /// <returns></returns>
    public virtual Boolean Init(Object config, IApiHost host)
    {
        Host = host;

        if (config is NetUri uri) Port = uri.Port;

        Server.Log = Log;
        Server.Tracer = Tracer;
        Server.ReuseAddress = ReuseAddress;

        var json = ServiceProvider?.GetService<IJsonHost>() ?? JsonHelper.Default;
        Encoder = new HttpEncoder { JsonHost = json, UseHttpStatus = (host as ApiServer)?.UseHttpStatus ?? false };

        // 兜底路由：ApiServer 的全部动作由 ApiHttpHandler 按 URI 分发
        Server.Map("/{*path}", new ApiHttpHandler(this));

        return true;
    }

    /// <summary>开始</summary>
    public virtual void Start()
    {
        Log.Info("Starting HTTP server [{0}], port {1}", Name, Port);

        Server.Start();

        Log.Info("HTTP server [{0}] ready, port {1}", Name, Port);
    }

    /// <summary>停止</summary>
    /// <param name="reason">关闭原因。便于日志分析</param>
    public virtual void Stop(String reason)
    {
        Log.Info("Stopping HTTP server [{0}] {1}", Name, reason);

        Server.Stop(reason);
    }
    #endregion
}