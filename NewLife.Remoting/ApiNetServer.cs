using NewLife.Data;
using NewLife.Log;
using NewLife.Messaging;
using NewLife.Net;

namespace NewLife.Remoting;

/// <summary>SRMP协议服务端。承载 <see cref="ApiHost.GetMessageCodec"/> 默认封包协议的长连接 RPC</summary>
class ApiNetServer : NetServer<ApiNetSession>, IApiServer
{
    /// <summary>主机</summary>
    public IApiHost Host { get; set; } = null!;

    /// <summary>当前服务器所有会话</summary>
    public IApiSession[] AllSessions => Sessions.ToValueArray().Where(e => e is IApiSession).Cast<IApiSession>().ToArray();

    public ApiNetServer()
    {
        Name = "Api";
        UseSession = true;
    }

    /// <summary>初始化</summary>
    /// <param name="config"></param>
    /// <param name="host"></param>
    /// <returns></returns>
    public virtual Boolean Init(Object config, IApiHost host)
    {
        Host = host;

        if (config is NetUri uri) Local = uri;

        // 新生命标准网络封包协议。拆端口后 ApiServer 只跑 SRMP，HTTP 由 ApiHttpServer 承担
        Protocol = Host.GetMessageCodec();

        return true;
    }
}

class ApiNetSession : NetSession<ApiNetServer>, IApiSession
{
    private ApiServer _Host = null!;
    /// <summary>主机</summary>
    IApiHost IApiSession.Host => _Host;

    /// <summary>最后活跃时间</summary>
    public DateTime LastActive { get; set; }

    /// <summary>所有服务器所有会话，包含自己</summary>
    public virtual IApiSession[] AllSessions => _Host.Server.AllSessions;

    /// <summary>令牌</summary>
    public String? Token { get; set; }

    /// <summary>开始会话处理</summary>
    public override void Start()
    {
        _Host = (Host!.Host as ApiServer)!;

        base.Start();
    }

    protected override void OnReceive(ReceivedEventArgs e)
    {
        LastActive = DateTime.Now;

        // 大帧未到齐时消息泵在头部到齐即交付，体为流式（数据随管道到达）。后续克隆负载、解码都要求完整内存包，
        // 按 Core 的官方指引在同步事件处理器内先物化（ReadAllAsync 等待数据到齐；串行语义下一次只有一帧在途，等待不会与下一帧冲突）。
        // 服务端会话未发过等待请求（无匹配队列），Core 不会替我们物化流式体
        if (e.Message is IMessage m && m.Body is { IsStreaming: true } body)
            m.SetBody(body.ReadAllAsync().AsTask().GetAwaiter().GetResult());

        // 解码得到请求消息，忽略响应消息
        if (e.Message is not IMessage msg || msg.Reply) return;

        Process(msg);
    }

    /// <summary>处理请求消息。连接复用（Multiplex）时投递线程池，否则同步处理</summary>
    /// <param name="msg">请求消息</param>
    private void Process(IMessage msg)
    {
        // 连接复用
        if (_Host is ApiServer svr && svr.Multiplex)
        {
            // 防御性检查：正常情况下 Payload 不会为 null（即使无参调用，SRMP 协议头也已填充），
            // 若此处触发，说明下层编解码实现存在设计缺陷
            if (msg.Payload == null)
            {
                WriteLog("Payload 不应为 null，请检查下层编解码实现");
                return;
            }

            // 帧泵缓冲随本轮处理结束即回收，投递线程池前克隆独立负载（连同 HTTP 头部等元数据）
            var clone = CloneMessage(msg);

            // 不要捕获上下文，避免多次请求串到一起
            ThreadPool.UnsafeQueueUserWorkItem(m =>
            {
                _ = ProcessMessageAsync(m);
            }, clone);

            // 本地异步函数：异步处理请求并发送响应，不阻塞线程池线程
            async Task ProcessMessageAsync(Object? m)
            {
                try
                {
                    // 防御性检查：m 必然是 IMessage 且 Payload 非 null，理论上不会执行到此处，
                    // 若触发则说明下层消息投递逻辑存在设计缺陷
                    if (m is not IMessage msg2 || msg2.Payload == null)
                    {
                        WriteLog("线程池回调收到非法消息，不应发生，请检查下层消息投递实现");
                        return;
                    }

                    // ProcessAsync 返回的 IMessage 持有 IOwnerPacket 所有权（通过 Payload 链），
                    // using 确保发送完毕后释放，级联归还 ArrayPool 缓冲区
                    using var rs = await _Host.ProcessAsync(this, msg2, this).ConfigureAwait(false);
                    if (rs != null && Session != null && !Session.Disposed) Session.SendMessage(rs);
                }
                catch (Exception ex)
                {
                    //XTrace.WriteException(ex);
                    OnError(this, new ExceptionEventArgs("", ex));
                }
                finally
                {
                    // 归还克隆消息持有的负载
                    (m as IMessage).TryDispose();
                }
            }
        }
        else
        {
            // 同步路径：using 释放响应消息，级联归还 Payload 链中的 IOwnerPacket 缓冲区
            using var rs = _Host.Process(this, msg, this);
            if (rs != null && Session != null && !Session.Disposed) Session.SendMessage(rs);
        }
    }

    /// <summary>克隆消息。复制负载使消息独立持有内存，可安全跨线程处理</summary>
    /// <param name="msg">源消息</param>
    /// <returns>独立的消息副本，调用方负责释放</returns>
    private static IMessage CloneMessage(IMessage msg)
    {
        IMessage clone = msg switch
        {
            DefaultMessage dm => new DefaultMessage { Flag = dm.Flag, Sequence = dm.Sequence, Kind = dm.Kind },
            _ => new Message { Kind = msg.Kind },
        };
        clone.SetBody(msg.Payload!.Clone());

        return clone;
    }

    /// <summary>单向远程调用，无需等待返回</summary>
    /// <param name="action">服务操作</param>
    /// <param name="args">参数</param>
    /// <param name="flag">标识</param>
    /// <returns></returns>
    public Int32 InvokeOneWay(String action, Object? args = null, Byte flag = 0)
    {
        using var span = Host!.Tracer?.NewSpan("rpc:" + action, args);
        if (args != null && span != null) args = span.Attach(args);

        // 编码请求
        using var msg = Host.Host.Encoder.CreateRequest(action, args);

        if (msg is DefaultMessage dm)
        {
            dm.OneWay = true;
            if (flag > 0) dm.Flag = flag;
        }

        try
        {
            return Session.SendMessage(msg);
        }
        catch (Exception ex)
        {
            // 跟踪异常
            span?.SetError(ex, args);

            throw;
        }
        finally
        {
            //msg.Payload.TryDispose();
        }
    }

    /// <summary>发送消息</summary>
    /// <param name="msg">消息</param>
    /// <returns></returns>
    public Int32 SendMessage(IMessage msg) => Session.SendMessage(msg);
}