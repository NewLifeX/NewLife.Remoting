using System.Net;
using System.Net.WebSockets;
using System.Text;
using NewLife.Data;
using NewLife.Log;
using NewLife.Messaging;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Security;
using NewLife.Serialization;

namespace NewLife.Remoting.Extensions.Services;

/// <summary>WebSocket 命令会话。通过 WebSocket 长连接实现服务端与客户端的双向通信</summary>
/// <remarks>
/// 继承自 <see cref="CommandSession"/>，专门用于 WebSocket 协议的命令会话管理。
/// 
/// <para>核心功能：</para>
/// <list type="number">
/// <item>维护 WebSocket 连接，监听客户端消息</item>
/// <item>处理服务端下发的命令，通过 WebSocket 发送给客户端</item>
/// <item>支持 Ping/Pong 心跳机制，维持长连接活性</item>
/// <item>支持数据包分发器，用于 EventHub 场景</item>
/// <item>实现 IEventDispatcher 接口，可作为 EventHub 订阅者接收广播消息</item>
/// </list>
/// 
/// <para>生命周期：</para>
/// <list type="number">
/// <item>客户端发起 WebSocket 连接请求</item>
/// <item>服务端 Accept 后创建 WsCommandSession 实例</item>
/// <item>调用 <see cref="WaitAsync"/> 进入阻塞等待，监听客户端消息</item>
/// <item>服务端通过 <see cref="HandleAsync"/> 下发命令</item>
/// <item>客户端断开或异常时，会话结束并触发下线回调</item>
/// </list>
/// 
/// <para>心跳机制：</para>
/// 客户端发送 "Ping" 文本消息，服务端响应 "Pong"，同时刷新在线状态。
/// 
/// <para>事件广播机制：</para>
/// <list type="number">
/// <item>客户端通过 WebSocket 发送事件消息，格式为 "event#topic#clientid#message"</item>
/// <item>服务端解析后通过 Dispatcher 分发给 EventHub</item>
/// <item>EventHub 根据 topic 找到事件总线并广播给所有订阅者</item>
/// <item>作为订阅者的 WsCommandSession 收到广播后，通过 WebSocket 发送给各自的客户端</item>
/// </list>
/// </remarks>
/// <param name="socket">WebSocket 实例</param>
public class WsCommandSession(WebSocket socket) : CommandSession, IEventHandler<IPacket>
{
    #region 属性
    /// <summary>是否活动中。根据 WebSocket 连接状态判断</summary>
    public override Boolean Active => socket != null && socket.State == WebSocketState.Open;

    /// <summary>数据包分发器。用于 EventHub 场景，将收到的数据包分发给订阅者</summary>
    public IEventHandler<IPacket>? Dispatcher { get; set; }

    /// <summary>服务提供者。用于获取 JSON 序列化器等服务</summary>
    public IServiceProvider? ServiceProvider { get; set; }

    /// <summary>取消令牌源。用于取消 WebSocket 等待循环</summary>
    private CancellationTokenSource? _source;
    #endregion

    #region 销毁
    /// <summary>销毁会话，关闭 WebSocket 连接</summary>
    /// <param name="disposing">是否主动销毁</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        try
        {
            // 取消等待循环
            _source?.Cancel();

            // 正常关闭 WebSocket
            if (socket != null && socket.State == WebSocketState.Open)
                socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Dispose", default);
        }
        catch { }
        finally
        {
            socket?.Dispose();
        }
    }
    #endregion

    #region 命令处理
    /// <summary>处理服务端下发的命令，通过 WebSocket 发送给客户端</summary>
    /// <param name="command">命令模型</param>
    /// <param name="message">原始命令消息的 JSON 字符串</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public override Task HandleAsync(CommandModel command, String? message, CancellationToken cancellationToken)
    {
        // 优先使用原始消息，避免重复序列化
        if (message == null && command != null)
        {
            var jsonHost = ServiceProvider?.GetService<IJsonHost>();
            message = jsonHost != null ? jsonHost.Write(command) : command.ToJson();
        }

        return socket.SendAsync(message.GetBytes(), WebSocketMessageType.Text, true, cancellationToken);
    }
    #endregion

    #region 发送数据
    /// <summary>向客户端发送文本消息</summary>
    /// <param name="message">消息内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task SendAsync(String message, CancellationToken cancellationToken = default)
    {
        if (!Active) return Task.CompletedTask;

        return socket.SendAsync(message.GetBytes(), WebSocketMessageType.Text, true, cancellationToken);
    }

    /// <summary>向客户端发送数据包</summary>
    /// <param name="data">数据包</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task SendAsync(IPacket data, CancellationToken cancellationToken = default)
    {
        if (!Active) return Task.CompletedTask;

        return socket.SendAsync(data.ToSegment(), WebSocketMessageType.Binary, true, cancellationToken);
    }
    #endregion

    #region IEventHandler 实现
    private static readonly Byte[] _eventPrefix = Encoding.ASCII.GetBytes("event#");
    /// <summary>作为订阅者收到广播消息时，通过 WebSocket 发送给客户端</summary>
    /// <remarks>
    /// 当 EventHub 广播消息时，会调用此方法将消息发送给订阅的客户端。
    /// </remarks>
    /// <param name="data">广播的数据包</param>
    /// <param name="context">事件上下文。用于在发布者、订阅者及中间处理器之间传递协调数据，如 Handler、ClientId 等</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功发送返回 1，否则返回 0</returns>
    public async Task HandleAsync(IPacket data, IEventContext? context, CancellationToken cancellationToken)
    {
        if (!Active || data == null) return;

        if (data.GetSpan().StartsWith(_eventPrefix))
        {
            // 事件消息，直接发送
        }
        // Raw记录原始数据包，分发给其它ws处理器时，可以原封不动转发给客户端
        else if (context is IExtend ext && ext["Raw"] is IPacket raw && raw.GetSpan().StartsWith(_eventPrefix))
            data = raw;
        // 这里不要组包，可能上层就是要发送裸包数据
        //else if (context is IExtend ext2)
        //{
        //    // 普通消息，包装成事件格式发送，方便客户端区分
        //    var topic = (context as EventContext)?.Topic ?? ext2["Topic"]?.ToString();
        //    var clientId = (context as EventContext)?.ClientId ?? ext2["ClientId"]?.ToString();
        //    var msg = $"event#{topic}#{clientId}#" + data.ToStr();
        //    data = new ArrayPacket(msg.GetBytes());
        //}

        using var span = Tracer?.NewSpan("cmd:Ws.Dispatch", Code, data.Total);
        try
        {
            // 通过 WebSocket 发送给客户端
            await socket.SendAsync(data.ToSegment(), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            span?.SetError(ex, null);
            Log?.WriteLog("WebSocket发送异常", false, ex.Message);
        }
    }
    #endregion

    #region WebSocket 等待
    /// <summary>阻塞等待 WebSocket 连接结束。在连接期间持续监听客户端消息</summary>
    /// <remarks>
    /// 此方法会阻塞当前任务，直到 WebSocket 连接关闭或取消。
    /// 连接期间会处理客户端发送的消息，包括心跳和业务数据。
    /// </remarks>
    /// <param name="context">HTTP 上下文</param>
    /// <param name="span">链路追踪埋点，进入等待前会结束该埋点</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public virtual async Task WaitAsync(HttpContext context, ISpan? span, CancellationToken cancellationToken)
    {
        // 获取远程地址信息
        var connection = context.Connection;
        var address = connection.RemoteIpAddress ?? IPAddress.Loopback;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var remote = new IPEndPoint(address, connection.RemotePort);
        var sid = Rand.Next();

        Log?.WriteLog("WebSocket连接", true, $"State={socket.State} sid={sid} Remote={remote}");

        // 通知上线
        SetOnline?.Invoke(true);

        // 即将进入阻塞等待，结束埋点
        span?.TryDispose();

        // 链接取消令牌。当客户端断开时，触发取消，结束长连接
        using var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _source = source;

        try
        {
            await ReceiveLoopAsync(remote, source).ConfigureAwait(false);
        }
        finally
        {
            // 确保取消令牌源被重置
            _source = null;
        }

        // 尝试正常关闭 WebSocket，需检查状态避免 ObjectDisposedException
        try
        {
            if (socket.State == WebSocketState.Open)
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "finish", default).ConfigureAwait(false);
        }
        catch (ObjectDisposedException) { }
        catch (WebSocketException ex)
        {
            Log?.WriteLog("WebSocket异常", false, ex.Message);
        }

        Log?.WriteLog("WebSocket断开", true, $"State={socket.State} CloseStatus={socket.CloseStatus} sid={sid} Remote={remote}");

        // 通知下线
        SetOnline?.Invoke(false);
    }

    /// <summary>WebSocket 消息接收循环</summary>
    /// <param name="remote">远程端点地址</param>
    /// <param name="source">取消令牌源</param>
    private async Task ReceiveLoopAsync(IPEndPoint remote, CancellationTokenSource source)
    {
        var buf = new Byte[64 * 1024];
        while (!source.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            // try-catch 放在循环内，避免单次异常退出循环
            try
            {
                var data = await socket.ReceiveAsync(new ArraySegment<Byte>(buf), source.Token).ConfigureAwait(false);
                if (data.MessageType == WebSocketMessageType.Close) break;

                if (data.MessageType is WebSocketMessageType.Text or WebSocketMessageType.Binary)
                {
                    using var span = Tracer?.NewSpan("cmd:Ws.Receive", $"[{data.MessageType}]{remote}", data.Count);

                    var pk = new ArrayPacket(buf, 0, data.Count);
                    await OnReceive(pk, source.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (WebSocketException ex) when (!source.IsCancellationRequested)
            {
                Log?.WriteLog("WebSocket异常", false, ex.Message);
                break;
            }
            catch (Exception ex) when (!source.IsCancellationRequested)
            {
                XTrace.WriteException(ex);
            }
        }

        // 通知取消
        try
        {
            if (!source.IsCancellationRequested) source.Cancel();
        }
        catch (ObjectDisposedException) { }
    }
    #endregion

    #region 消息处理
    /// <summary>处理客户端上发的数据</summary>
    /// <remarks>
    /// 支持以下消息类型：
    /// <list type="bullet">
    /// <item>心跳消息：收到 "Ping" 时响应 "Pong"，并刷新在线状态</item>
    /// <item>事件消息：格式为 "event#topic#clientid#message"，将消息分发到 EventHub 对应的 topic</item>
    /// <item>其它业务数据：通过 Dispatcher 分发处理</item>
    /// </list>
    /// </remarks>
    /// <param name="data">数据包</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    protected virtual async Task OnReceive(IPacket data, CancellationToken cancellationToken)
    {
        // 处理心跳消息
        if (data.Total == 4)
        {
            var msg = data.ToStr();
            if (msg == "Pong") return;
            if (msg == "Ping")
            {
                // 刷新在线状态。可能客户端心跳已经停了，WS 还在，这里重新上线
                SetOnline?.Invoke(true);

                await socket.SendAsync("Pong".GetBytes(), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
                return;
            }
        }

        // 分发业务数据
        if (Dispatcher != null)
        {
            // 可能收到订阅动作指令，在EventHub中执行订阅动作时，需要把处理器传递过去。EventHub中会读取名为Handler的上下文参数
            var context = new EventContext();
            context["Handler"] = this;

            // Raw记录原始数据包，分发给其它ws处理器时，可以原封不动转发给客户端
            context["Raw"] = data;

            await Dispatcher.HandleAsync(data, context, cancellationToken).ConfigureAwait(false);
        }
    }
    #endregion
}
