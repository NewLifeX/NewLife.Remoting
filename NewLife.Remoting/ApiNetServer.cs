using NewLife.Data;
using NewLife.Log;
using NewLife.Messaging;
using NewLife.Model;
using NewLife.Net;
using NewLife.Remoting.Http;
using NewLife.Serialization;
using HttpCodec = NewLife.Remoting.Http.HttpCodec;

namespace NewLife.Remoting;

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

        var json = ServiceProvider?.GetService<IJsonHost>() ?? JsonHelper.Default;

        Add(new WebSocketServerCodec { Server = "ApiServer", Protocol = "SRMP" });
        Add(new HttpCodec { AllowParseHeader = true, JsonHost = json });

        // 新生命标准网络封包协议
        Add(Host.GetMessageCodec());

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

        // 解码得到请求消息，忽略响应消息
        if (e.Message is not IMessage msg || msg.Reply) return;

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

            // 投递线程池前确保 Payload 独立持有内存，避免 SAEA buffer 或 ArrayPool 内存被提前回收
            // 如果消息使用了原来SEAE的数据包，需要拷贝，避免多线程冲突
            // 也可能在粘包处理时，已经拷贝了一次
            EnsureOwnedPayload(msg, e.Packet);

            // 不要捕获上下文，避免多次请求串到一起
            ThreadPool.UnsafeQueueUserWorkItem(m =>
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

                    // Process 返回的 IMessage 持有 IOwnerPacket 所有权（通过 Payload 链），
                    // using 确保发送完毕后释放，级联归还 ArrayPool 缓冲区
                    using var rs = _Host.Process(this, msg2, this);
                    if (rs != null && Session != null && !Session.Disposed) Session.SendMessage(rs);

                    // 归还消息对象到池
                    if (m is DefaultMessage dm) DefaultMessage.Return(dm);
                }
                catch (Exception ex)
                {
                    //XTrace.WriteException(ex);
                    OnError(this, new ExceptionEventArgs("", ex));
                }
            }, msg);
        }
        else
        {
            // 同步路径：using 释放响应消息，级联归还 Payload 链中的 IOwnerPacket 缓冲区
            using var rs = _Host.Process(this, msg, this);
            if (rs != null && Session != null && !Session.Disposed) Session.SendMessage(rs);

            // 归还消息对象到池
            if (msg is DefaultMessage dm) DefaultMessage.Return(dm);
        }
    }

    /// <summary>确保消息 Payload 独立持有内存，避免与 SAEA 缓冲区或 ArrayPool 租用内存共享导致多线程冲突</summary>
    /// <param name="msg">待处理的请求消息</param>
    /// <param name="saeaPacket">本次 SAEA 接收的原始数据包</param>
    private static void EnsureOwnedPayload(IMessage msg, IPacket? saeaPacket)
    {
        if (msg.Payload == null || saeaPacket is not ArrayPacket ap) return;

        if (msg.Payload is ArrayPacket ap2 && ap.Buffer == ap2.Buffer)
            // 小数据：Payload 直接切片自 SAEA buffer，异步路径下 buffer 可能被下一次接收复用
            msg.Payload = ap2.Clone();
        else if (msg.Payload is IOwnerPacket)
            // 大数据：OwnerPacket 持有 ArrayPool 租用内存，OnReceive 同步返回后管道会归还
            msg.Payload = msg.Payload.Clone();
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
}