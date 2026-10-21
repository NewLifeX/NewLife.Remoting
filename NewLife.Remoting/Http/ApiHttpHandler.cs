using System.Net;
using NewLife.Collections;
using NewLife.Data;
using NewLife.Http;
using NewLife.Messaging;
using NewLife.Remoting.Http;

namespace NewLife.Remoting;

/// <summary>ApiServer 的 HTTP 请求处理器。把 Core 的请求桥接为 <see cref="HttpMessage"/>，交主机分发后回填响应</summary>
/// <remarks>
/// <para>请求方向：合成 HTTP 头部文本（请求行 + 头部行）装入 HttpMessage，使既有 <see cref="HttpEncoder"/> 无需改动即可解析；
/// 请求体以共享句柄（引用计数）挂载，生命周期独立于 Core 的请求对象。</para>
/// <para>响应方向：从编码器输出的状态行解析状态码，负载所有权转移给 Core 的响应体，由 Core 在发送完成后释放。</para>
/// <para>本处理器不接受 WebSocket：Core 在路由前就会完成 WS 握手（<see cref="IHttpContext.WebSocket"/> 非空），此时直接回 501。</para>
/// </remarks>
/// <param name="server">Http服务端</param>
class ApiHttpHandler(ApiHttpServer server) : IHttpHandler
{
    /// <summary>桥接处理。合成请求消息，交主机异步分发，回填响应</summary>
    /// <param name="context">Http上下文</param>
    /// <returns>处理任务</returns>
    public virtual async Task ProcessRequestAsync(IHttpContext context)
    {
        var request = context.Request;

        // SRMP-over-WS 已取消：Core 会在路由前完成 WS 握手，这里明确拒绝，避免客户端拿到 101 之后空等
        if (context.WebSocket != null)
        {
            server.Log.Warn("Rejected WebSocket handshake {0} {1}", request.Method, request.RequestUri);

            context.Response.StatusCode = HttpStatusCode.NotImplemented;
            context.Response.ContentType = "text/plain";
            context.Response.Body = (ArrayPacket)L.T("The WebSocket channel is not supported; use tcp/udp for SRMP and this port for HTTP").GetBytes();

            return;
        }

        if (server.Host is not ApiServer host) throw new InvalidOperationException(L.T("Host of HTTP server [{0}] is not an ApiServer", server.Name));

        var session = new ApiHttpSession(server) { ["Encoder"] = server.Encoder };

        using var msg = CreateMessage(context.Request);

        var rs = await host.ProcessAsync(session, msg, host).ConfigureAwait(false) as HttpMessage;

        // 单向请求不产生响应消息，回空响应
        if (rs == null) return;

        var response = context.Response;
        response.StatusCode = ParseStatusCode(rs) ?? HttpStatusCode.OK;
        response.ContentType = "application/json";

        // 负载所有权转移给 Core 的响应对象，发送完成后由 Core 释放
        var payload = rs.Payload;
        rs.SetBody(null);
        response.Body = payload;

        rs.TryDispose();

        // 请求消息持有的负载：被响应占用（同一句柄）时放弃持有，否则随消息释放，归还内存池
        if (ReferenceEquals(msg.Payload, payload)) msg.SetBody(null);
    }

    /// <summary>把 Core 的请求合成为 HttpMessage。头部文本供 <see cref="HttpEncoder"/> 解析，请求体以共享句柄挂载</summary>
    /// <param name="request">Core的Http请求</param>
    /// <returns>独立的消息对象，调用方负责释放</returns>
    private static HttpMessage CreateMessage(HttpRequest request)
    {
        var uri = request.RequestUri?.OriginalString ?? "/";

        var sb = Pool.StringBuilder.Get();
        sb.Append(request.Method).Append(' ').Append(uri).Append(" HTTP/1.1\r\n");

        var headers = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in request.Headers)
        {
            if (item.Value == null) continue;

            headers[item.Key] = item.Value;
            sb.Append(item.Key).Append(": ").Append(item.Value).Append("\r\n");
        }

        var msg = new HttpMessage
        {
            Method = request.Method,
            Uri = uri,
            Headers = headers,
            ContentLength = request.ContentLength,
            Header = (ArrayPacket)sb.Return(true).GetBytes(),
        };

        // 共享切片（引用计数独立持有）：请求体生命周期不再依赖 Core 的请求对象
        var body = request.Body;
        if (body != null && body.Total > 0) msg.SetBody(body.Slice(0, -1));

        return msg;
    }

    /// <summary>从响应头部文本解析状态码。编码器输出形如 <c>HTTP/1.1 500 Error</c></summary>
    /// <param name="rs">响应消息</param>
    /// <returns>状态码；解析失败返回 null</returns>
    private static HttpStatusCode? ParseStatusCode(HttpMessage rs)
    {
        var header = rs.Header;
        if (header == null || header.Total == 0) return null;

        var span = header.GetSpan();
        var p = span.IndexOf((Byte)'\n');
        if (p <= 0) return null;

        var line = span[..p].ToStr().Trim();
        var ss = line.Split(' ');
        if (ss.Length < 2) return null;

        var code = ss[1].ToInt();

        return code > 0 ? (HttpStatusCode)code : null;
    }
}

/// <summary>HTTP 会话壳。HTTP 为请求-响应模型，无连接状态，只承载单次请求的上下文</summary>
/// <param name="server">Http服务端</param>
class ApiHttpSession(ApiHttpServer server) : IApiSession
{
    private readonly IDictionary<String, Object?> _items = new Dictionary<String, Object?>();

    /// <summary>主机</summary>
    public IApiHost Host => server.Host;

    /// <summary>最后活跃时间</summary>
    public DateTime LastActive { get; set; } = DateTime.Now;

    /// <summary>所有服务器所有会话，包含自己</summary>
    /// <remarks>HTTP 无长连接，恒为空集合，业务不要依赖它做广播</remarks>
    public IApiSession[] AllSessions => [];

    /// <summary>令牌</summary>
    public String? Token { get; set; }

    /// <summary>扩展数据</summary>
    public IDictionary<String, Object?> Items => _items;

    /// <summary>扩展数据索引器</summary>
    /// <param name="key">键</param>
    /// <returns></returns>
    public Object? this[String key] { get => _items.TryGetValue(key, out var value) ? value : null; set => _items[key] = value; }

    /// <summary>单向远程调用，无需等待返回</summary>
    /// <param name="action">服务操作</param>
    /// <param name="args">参数</param>
    /// <param name="flag">标识</param>
    /// <returns>写入的字节数</returns>
    public Int32 InvokeOneWay(String action, Object? args = null, Byte flag = 0) => throw new NotSupportedException(L.T("The HTTP channel is a request-response model and does not support server-initiated calls"));

    /// <summary>发送消息。用于流式推送等场景</summary>
    /// <param name="msg">消息</param>
    /// <returns>写入的字节数</returns>
    public Int32 SendMessage(IMessage msg) => throw new NotSupportedException(L.T("The HTTP channel does not support server-side streaming push; use the tcp/udp channel instead"));

    /// <summary>返回会话描述</summary>
    /// <returns></returns>
    public override String ToString() => $"{server.Name}[Http]";
}
