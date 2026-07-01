using System.Text;
using NewLife.Log;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Serialization;

namespace NewLife.Remoting.Extensions.Services;

/// <summary>SSE 命令会话。通过 Server-Sent Events 实现服务端向客户端的单向命令推送</summary>
/// <remarks>
/// SSE 是 WebSocket 的轻量降级通道，适用于：
/// <list type="bullet">
/// <item>客户端环境不支持 WebSocket（某些代理/防火墙）</item>
/// <item>仅需服务端→客户端单向推送的场景</item>
/// <item>作为 WebSocket 的备用通道，提高整体可靠性</item>
/// </list>
/// 
/// <para>SSE 协议格式：</para>
/// <code>
/// event: command
/// data: {"Id":1,"Command":"Restart",...}
///
/// </code>
/// 每条消息以空行（\n\n）分隔，支持多行 data。
/// 
/// <para>客户端通过 HTTP GET /Device/NotifySSE 建立连接，需携带设备令牌。</para>
/// </remarks>
public class SseCommandSession : CommandSession
{
    #region 属性
    private readonly HttpResponse _response;
    private readonly Stream _body;

    /// <summary>服务提供者。用于获取 JSON 序列化器</summary>
    public IServiceProvider? ServiceProvider { get; set; }

    /// <summary>取消令牌源</summary>
    private CancellationTokenSource? _source;

    /// <summary>是否活动中。根据响应流是否可写判断</summary>
    public override Boolean Active => !_response.HttpContext.RequestAborted.IsCancellationRequested;

    /// <summary>心跳间隔（秒）。默认 30 秒，发送 SSE 注释防止代理超时</summary>
    public Int32 HeartbeatInterval { get; set; } = 30;
    #endregion

    #region 构造
    /// <summary>实例化 SSE 命令会话</summary>
    /// <param name="response">HTTP 响应</param>
    /// <param name="deviceCode">设备编码</param>
    /// <param name="serviceProvider">服务提供者</param>
    public SseCommandSession(HttpResponse response, String deviceCode, IServiceProvider serviceProvider)
    {
        _response = response ?? throw new ArgumentNullException(nameof(response));
        _body = response.Body;
        Code = deviceCode;
        ServiceProvider = serviceProvider;
    }

    /// <summary>销毁资源</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        try
        {
            _source?.Cancel();
            _source.TryDispose();
        }
        catch { }
    }
    #endregion

    #region 命令处理
    /// <summary>通过 SSE 向客户端发送命令</summary>
    /// <param name="command">命令模型</param>
    /// <param name="message">原始命令消息的 JSON 字符串</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public override async Task HandleAsync(CommandModel command, String? message, CancellationToken cancellationToken)
    {
        if (!Active) return;

        // 优先使用原始消息，避免重复序列化
        if (message == null && command != null)
        {
            var jsonHost = ServiceProvider?.GetService<IJsonHost>();
            message = jsonHost != null ? jsonHost.Write(command) : command.ToJson();
        }

        if (message.IsNullOrEmpty()) return;

        // SSE 格式: "event: command\ndata: {json}\n\n"
        var sseData = $"event: command\ndata: {message}\n\n";
        var bytes = Encoding.UTF8.GetBytes(sseData);

        try
        {
            await _body.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await _body.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log?.WriteLog("SSE发送命令失败", false, ex.Message);
        }
    }
    #endregion

    #region SSE 等待
    /// <summary>初始化 SSE 响应头并保持连接</summary>
    /// <param name="span">链路追踪埋点</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public virtual async Task WaitAsync(ISpan? span, CancellationToken cancellationToken)
    {
        // 设置 SSE 响应头
        _response.StatusCode = 200;
        _response.ContentType = "text/event-stream";
        _response.Headers.Append("Cache-Control", "no-cache");
        _response.Headers.Append("Connection", "keep-alive");
        _response.Headers.Append("X-Accel-Buffering", "no");  // 禁用 nginx 缓冲

        // 通知上线
        SetOnline?.Invoke(true);

        // 结束创建埋点
        span?.TryDispose();

        // 创建取消令牌
        using var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _response.HttpContext.RequestAborted);
        _source = source;

        try
        {
            // 发送初始连接事件
            var connected = $"event: connected\ndata: {{\"code\":\"{Code}\"}}\n\n";
            await _body.WriteAsync(Encoding.UTF8.GetBytes(connected), source.Token).ConfigureAwait(false);
            await _body.FlushAsync(source.Token).ConfigureAwait(false);

            // 保持连接，定期发送心跳
            Log?.WriteLog("SSE连接", true, $"Code={Code}");

            while (!source.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(HeartbeatInterval * 1000, source.Token).ConfigureAwait(false);

                    // 发送 SSE 注释作为心跳，防止代理超时
                    var heartbeat = ": heartbeat\n\n";
                    await _body.WriteAsync(Encoding.UTF8.GetBytes(heartbeat), source.Token).ConfigureAwait(false);
                    await _body.FlushAsync(source.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Log?.WriteLog("SSE心跳异常", false, ex.Message);
                    break;
                }
            }
        }
        finally
        {
            _source = null;
        }

        Log?.WriteLog("SSE断开", true, $"Code={Code}");

        // 通知下线
        SetOnline?.Invoke(false);
    }
    #endregion
}
