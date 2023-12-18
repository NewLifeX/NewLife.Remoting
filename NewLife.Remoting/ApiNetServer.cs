﻿using NewLife.Log;
using NewLife.Messaging;
using NewLife.Net;
using NewLife.Remoting.Http;

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
        //// 如果主机为空，监听所有端口
        //if (Local.Host.IsNullOrEmpty() || Local.Host == "*") AddressFamily = System.Net.Sockets.AddressFamily.Unspecified;

        // Http封包协议
        //Add<HttpCodec>();
        Add(new HttpCodec { AllowParseHeader = true });

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

    ///// <summary>查找Api动作</summary>
    ///// <param name="action"></param>
    ///// <returns></returns>
    //public virtual ApiAction? FindAction(String action) => _Host.Manager.Find(action);

    ///// <summary>创建控制器实例</summary>
    ///// <param name="api"></param>
    ///// <returns></returns>
    //public virtual Object CreateController(ApiAction api)
    //{
    //    var controller = api.Controller;
    //    if (controller != null) return controller;

    //    controller = _Host.ServiceProvider?.GetService(api.Type);

    //    controller ??= api.Type.CreateInstance();
    //    if (controller == null) throw new InvalidDataException($"无法创建[{api.Type.FullName}]的实例");

    //    return controller;
    //}

    protected override void OnReceive(ReceivedEventArgs e)
    {
        LastActive = DateTime.Now;

        // Api解码消息得到Action和参数
        if (e.Message is not IMessage msg || msg.Reply) return;

        // 连接复用
        if (_Host is ApiServer svr && svr.Multiplex)
        {
            // 如果消息使用了原来SEAE的数据包，需要拷贝，避免多线程冲突
            // 也可能在粘包处理时，已经拷贝了一次
            if (e.Packet != null)
            {
                if (msg.Payload != null && e.Packet.Data == msg.Payload.Data)
                {
                    msg.Payload = msg.Payload.Clone();
                }
            }

            // 不要捕获上下文，避免多次请求串到一起
            ThreadPool.UnsafeQueueUserWorkItem(m =>
            {
                try
                {
                    var rs = _Host.Process(this, (m as IMessage)!);
                    if (rs != null && Session != null && !Session.Disposed) Session.SendMessage(rs);
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
            var rs = _Host.Process(this, msg);
            if (rs != null && Session != null && !Session.Disposed) Session.SendMessage(rs);
        }
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
        var msg = Host.Host.Encoder.CreateRequest(action, args);

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
    }
}