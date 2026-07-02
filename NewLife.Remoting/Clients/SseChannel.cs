using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using NewLife.Data;
using NewLife.Http;
using NewLife.Log;

namespace NewLife.Remoting.Clients;

/// <summary>SSE 通道。基于 Server-Sent Events 的轻量级服务端推送通道</summary>
/// <remarks>
/// SSE 是 WebSocket 的降级替代方案，通过标准 HTTP GET 长连接接收服务端推送。
/// 适用于 WebSocket 不可用时的场景（某些代理/防火墙不支持 WS 升级）。
/// 协议格式: text/event-stream，每条消息以空行分隔。
///
/// <para>连接生命周期：</para>
/// <list type="bullet">
/// <item>通过 ValidSse 建立 SSE 长连接，由 OnPing 定时器周期性调用以维持连接</item>
/// <item>DoPull 在后台读取流，解析 SSE 事件，通过 ClientBase.HandleAsync 分派</item>
/// <item>连接断开时自动取消，下次 OnPing 重新建立</item>
/// </list>
/// </remarks>
/// <param name="client">客户端基类实例</param>
class SseChannel(ClientBase client) : DisposeBase
{
    #region 属性
    private readonly ClientBase _client = client;
    private CancellationTokenSource? _source;
    private HttpClient? _httpClient;

    /// <summary>是否已连接</summary>
    public Boolean Active => _source != null && !_source.IsCancellationRequested;
    #endregion

    #region 构造与销毁
    /// <summary>销毁资源</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        StopSse();
    }
    #endregion

    #region 连接管理
    /// <summary>验证并维护 SSE 连接。由 ClientBase.OnPing 周期性调用</summary>
    /// <remarks>
    /// 若已连接则跳过；若未连接或已断开则建立新连接。
    /// 连接成功后启动后台 DoPull 循环读取 SSE 流。
    /// </remarks>
    /// <param name="http">Http客户端</param>
    /// <param name="sseAction">SSE 端点路径。例如 Device/NotifySSE</param>
    /// <returns></returns>
    public virtual async Task ValidSse(ApiHttpClient http, String sseAction)
    {
        var svc = http.Current;
        if (svc == null) return;

        var token = http.Token;
        if (http.Filter is TokenHttpFilter thf) token = thf.Token?.AccessToken;
        if (token.IsNullOrEmpty()) return;

        // 已连接则跳过
        if (Active) return;

        // 停止旧连接
        StopSse();

        var uri = new Uri(new Uri(svc.Address.ToString()), sseAction);

        using var span = _client.Tracer?.NewSpan("SseConnect", uri + "");
        try
        {
            _source = new CancellationTokenSource();
            _httpClient = new HttpClient();
            // SSE 为长连接，设置足够长的超时（约 24.8 天）
            _httpClient.Timeout = TimeSpan.FromMilliseconds(Int32.MaxValue);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, _source.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            // ReadAsStreamAsync() 不带 CancellationToken 以确保 netstandard2.0 兼容
            var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            _ = Task.Factory.StartNew(() => DoPull(stream, _source.Token), TaskCreationOptions.LongRunning);

            _client.WriteLog("SSE连接成功: {0}", uri);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            _client.WriteLog("SSE连接失败: {0}", ex.Message);
            StopSse();
        }
    }

    /// <summary>停止 SSE 连接</summary>
    public void StopSse()
    {
        _source?.Cancel();
        _source.TryDispose();
        _source = null;
        _httpClient.TryDispose();
        _httpClient = null;
    }
    #endregion

    #region SSE 接收循环
    /// <summary>SSE 事件接收循环。后台线程读取流，解析 SSE 格式并分派</summary>
    /// <param name="stream">HTTP 响应流</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    private async Task DoPull(Stream stream, CancellationToken cancellationToken)
    {
        DefaultSpan.Current = null;
        using var reader = new StreamReader(stream);
        var eventType = "";
        var sb = new StringBuilder();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // ReadLineAsync 不带 CancellationToken 以确保 net45/netstandard2.0 兼容
                String? line;
                try
                {
                    line = await reader.ReadLineAsync().ConfigureAwait(false);
                }
                catch (IOException) { break; }

                if (line == null) break;

                if (line.Length == 0)
                {
                    // 空行 = 事件结束
                    if (sb.Length > 0)
                    {
                        var data = sb.ToString();
                        sb.Clear();

                        if (eventType == "command" || eventType.IsNullOrEmpty())
                            await DispatchCommand(data, cancellationToken).ConfigureAwait(false);

                        eventType = "";
                    }
                }
                else if (line[0] == ':')
                {
                    // SSE 注释（心跳 ": heartbeat"），忽略
                }
                else if (line.StartsWith("event:"))
                {
                    eventType = line[6..].Trim();
                }
                else if (line.StartsWith("data:"))
                {
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(line[5..].Trim());
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!cancellationToken.IsCancellationRequested)
                _client.Log?.Error("[{0}]SSE异常[{1}]: {2}", _client.Name, ex.GetType().Name, ex.Message);
        }
        finally
        {
            _source?.Cancel();
        }
    }

    /// <summary>分派命令消息到 ClientBase</summary>
    /// <param name="data">JSON 数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    private async Task DispatchCommand(String data, CancellationToken cancellationToken)
    {
        try
        {
            var pk = new ArrayPacket(data.GetBytes());
            await _client.HandleAsync(pk, null, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _client.Log?.Error("[{0}]SSE命令处理异常: {1}", _client.Name, ex.Message);
        }
    }
    #endregion

    #region 日志
    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;
    #endregion
}
