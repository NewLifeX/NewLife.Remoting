﻿using System;
using System.Net.WebSockets;
using NewLife.Collections;
using NewLife.Data;
using NewLife.Log;
using NewLife.Messaging;
using NewLife.Net;
using NewLife.Threading;

namespace NewLife.Remoting;

/// <summary>Websocket版应用接口客户端</summary>
/// <remarks>
/// 保持到服务端直接的长连接RPC通信。
/// 常用于向服务端发送请求并接收应答，也可以接收服务端主动发送的单向消息。
/// 
/// 文档 https://newlifex.com/core/srmp
/// </remarks>
public class WsClient : ApiHost, IApiClient
{
    #region 属性
    /// <summary>是否已打开</summary>
    public Boolean Active { get; protected set; }

    /// <summary>服务端地址集合。负载均衡</summary>
    public String[]? Servers { get; set; }

    /// <summary>客户端连接集群</summary>
    public ICluster<String, WebSocket>? Cluster { get; set; }

    /// <summary>是否使用连接池。true时建立多个到服务端的连接（高吞吐），默认false使用单一连接（低延迟）</summary>
    public Boolean UsePool { get; set; }

    /// <summary>令牌。每次请求携带</summary>
    public String? Token { get; set; }

    /// <summary>最后活跃时间</summary>
    public DateTime LastActive { get; set; }

    /// <summary>收到请求或响应时</summary>
    public event EventHandler<ApiReceivedEventArgs>? Received;

    /// <summary>调用统计</summary>
    public ICounter? StatInvoke { get; set; }
    #endregion

    #region 构造
    /// <summary>实例化应用接口客户端</summary>
    public WsClient()
    {
        var type = GetType();
        Name = type.GetDisplayName() ?? type.Name.TrimEnd("Client");
    }

    /// <summary>实例化应用接口客户端</summary>
    /// <param name="uris">服务端地址集合，逗号分隔</param>
    public WsClient(String uris) : this()
    {
        if (!uris.IsNullOrEmpty()) Servers = uris.Split(",", ";");
    }

    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        _Timer.TryDispose();

        Close(Name + (disposing ? "Dispose" : "GC"));
    }
    #endregion

    #region 打开关闭
    /// <summary>打开客户端</summary>
    public virtual Boolean Open()
    {
        if (Active) return true;
        lock (this)
        {
            if (Active) return true;

            var ss = Servers;
            if (ss == null || ss.Length == 0) throw new ArgumentNullException(nameof(Servers), "未指定服务端地址");

            Encoder ??= new JsonEncoder();

            // 集群
            Cluster = InitCluster();
            WriteLog("集群：{0}", Cluster);

            Encoder.Log = EncoderLog;

            // 控制性能统计信息
            var ms = StatPeriod * 1000;
            if (ms > 0)
            {
                _Timer = new TimerX(DoWork, null, ms, ms) { Async = true };
            }

            return Active = true;
        }
    }

    /// <summary>关闭</summary>
    /// <param name="reason">关闭原因。便于日志分析</param>
    /// <returns>是否成功</returns>
    public virtual void Close(String reason)
    {
        if (!Active) return;

        Cluster?.Close(reason ?? (GetType().Name + "Close"));

        Active = false;
    }

    /// <summary>初始化集群</summary>
    protected virtual ICluster<String, WebSocket> InitCluster()
    {
        var cluster = Cluster;
        cluster ??= UsePool ?
                new ClientPoolCluster<WebSocket> { Log = Log } :
                new ClientSingleCluster<WebSocket> { Log = Log };

        if (cluster is ClientSingleCluster<WebSocket> sc && sc.OnCreate == null) sc.OnCreate = OnCreate;
        if (cluster is ClientPoolCluster<WebSocket> pc && pc.OnCreate == null) pc.OnCreate = OnCreate;

        cluster.GetItems ??= () => Servers ?? [];
        cluster.Open();

        return cluster;
    }
    #endregion

    #region 远程调用
    /// <summary>异步调用，等待返回结果</summary>
    /// <typeparam name="TResult">返回类型</typeparam>
    /// <param name="action">服务操作</param>
    /// <param name="args">参数</param>
    /// <param name="cancellationToken">取消通知</param>
    /// <returns></returns>
    public virtual async Task<TResult?> InvokeAsync<TResult>(String action, Object? args = null, CancellationToken cancellationToken = default)
    {
        // 让上层异步到这直接返回，后续代码在另一个线程执行
        //!!! Task.Yield会导致强制捕获上下文，虽然会在另一个线程执行，但在UI线程中可能无法抢占上下文导致死锁
        //await Task.Yield();

        Open();

        if (Cluster == null) throw new ArgumentNullException(nameof(Cluster));

        var act = action;
        var client = Cluster.Get();
        try
        {
            return await InvokeWithClientAsync<TResult>(client, act, args, 0, cancellationToken).ConfigureAwait(false);
        }
        catch (ApiException ex)
        {
            // 这个连接没有鉴权，重新登录后再次调用
            if (ex.Code == ApiCode.Unauthorized)
            {
                //await Cluster.InvokeAsync(client => OnLoginAsync(client, true)).ConfigureAwait(false);
                await OnLoginAsync(client, true).ConfigureAwait(false);

                return await InvokeWithClientAsync<TResult>(client, act, args, 0, cancellationToken).ConfigureAwait(false);
            }

            throw;
        }
        // 截断任务取消异常，避免过长
        catch (TaskCanceledException)
        {
            throw new TaskCanceledException($"[{act}]超时[{Timeout:n0}ms]取消");
        }
        finally
        {
            Cluster.Put(client);
        }
    }

    /// <summary>同步调用，阻塞等待</summary>
    /// <param name="action">服务操作</param>
    /// <param name="args">参数</param>
    /// <returns></returns>
    public virtual TResult? Invoke<TResult>(String action, Object? args = null) => Task.Run(() => InvokeAsync<TResult>(action, args)).Result;

    /// <summary>单向发送。同步调用，不等待返回</summary>
    /// <param name="action">服务操作</param>
    /// <param name="args">参数</param>
    /// <param name="flag">标识</param>
    /// <returns></returns>
    public virtual Int32 InvokeOneWay(String action, Object? args = null, Byte flag = 0)
    {
        if (!Open()) return -1;
        if (Cluster == null) throw new ArgumentNullException(nameof(Cluster));

        return Cluster.Invoke(client => InvokeWithClient(client, action, args, flag));
    }

    /// <summary>指定客户端的异步调用，等待返回结果</summary>
    /// <remarks>常用于在OnLoginAsync中实现连接后登录功能</remarks>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="client">客户端</param>
    /// <param name="action">服务操作</param>
    /// <param name="args">参数</param>
    /// <param name="flag">标识</param>
    /// <param name="cancellationToken">取消通知</param>
    /// <returns></returns>
    public virtual async Task<TResult?> InvokeWithClientAsync<TResult>(WebSocket client, String action, Object? args = null, Byte flag = 0, CancellationToken cancellationToken = default)
    {
        // 性能计数器，次数、TPS、平均耗时
        var st = StatInvoke;
        var sw = st.StartCount();

        LastActive = DateTime.Now;

        // 令牌
        if (!Token.IsNullOrEmpty() && args != null)
        {
            var dic = args.ToDictionary();
            if (!dic.ContainsKey("Token")) dic["Token"] = Token;
            args = dic;
        }

        // 埋点，注入traceParent到参数集合
        var span = Tracer?.NewSpan("rpc:" + action, args);
        if (args != null && span != null) args = span.Attach(args);

        // 编码请求，构造消息
        var enc = Encoder;
        var msg = enc.CreateRequest(action, args);
        if (flag > 0 && msg is DefaultMessage dm) dm.Flag = flag;

        var invoker = client.State + "";
        IMessage? rs = null;
        try
        {
            // 发起异步请求，等待返回
            //rs = (await client.SendMessageAsync(msg, cancellationToken).ConfigureAwait(false)) as IMessage;

            //todo 通过websocket收发数据
            var codec = GetMessageCodec();
            var pk = codec.Write(null, msg) as Packet;
            await client.SendAsync(pk.ToSegment(), WebSocketMessageType.Binary, true, default);

            var buf = new Byte[4 * 1024];
            var data = await client.ReceiveAsync(new ArraySegment<Byte>(buf), default);
            rs = codec.Read(null, data) as IMessage;

            if (rs == null) return default;
        }
        catch (AggregateException aggex)
        {
            var ex = aggex.GetTrue();

            // 跟踪异常
            span?.SetError(ex, args);

            if (ex is TaskCanceledException)
            {
                throw new TimeoutException($"请求[{action}]超时({msg})！", ex);
            }
            throw;
        }
        catch (TaskCanceledException ex)
        {
            span?.SetError(ex, args);
            throw new TimeoutException($"请求[{action}]超时({msg})！", ex);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, args);
            throw;
        }
        finally
        {
            var msCost = st.StopCount(sw) / 1000;
            if (SlowTrace > 0 && msCost >= SlowTrace) WriteLog($"慢调用[{action}]({msg})，耗时{msCost:n0}ms");

            span?.Dispose();
        }

        // 特殊返回类型
        var resultType = typeof(TResult);
        if (resultType == typeof(IMessage)) return (TResult)rs;
        //if (resultType == typeof(Packet)) return rs.Payload;
        if (rs.Payload == null) return default;

        // 解码响应得到SRMP报文
        var message = enc.Decode(rs) ?? throw new InvalidOperationException();

        // 是否成功
        if (message.Code is not ApiCode.Ok and not ApiCode.Ok200)
            throw new ApiException(message.Code, message.Data?.ToStr().Trim('\"') ?? "") { Source = invoker + "/" + action };

        if (message.Data == null) return default;
        if (resultType == typeof(Packet)) return (TResult)(Object)message.Data;

        // 解码结果
        var result = enc.DecodeResult(action, message.Data, rs, resultType);
        if (result == null) return default;
        if (resultType == typeof(Object)) return (TResult)result;

        // 返回
        //return (TResult?)enc.Convert(result, resultType);
        return (TResult?)result;
    }

    /// <summary>单向调用，不等待返回</summary>
    /// <param name="client"></param>
    /// <param name="action">服务操作</param>
    /// <param name="args">参数</param>
    /// <param name="flag">标识</param>
    /// <returns></returns>
    public Int32 InvokeWithClient(WebSocket client, String action, Object? args, Byte flag = 0)
    {
        if (client == null) return -1;

        // 性能计数器，次数、TPS、平均耗时
        var st = StatInvoke;

        using var span = Tracer?.NewSpan("rpc:" + action, args);
        if (args != null && span != null) args = span.Attach(args);

        // 编码请求
        var msg = Encoder.CreateRequest(action, args);

        if (msg is DefaultMessage dm)
        {
            dm.OneWay = true;
            if (flag > 0) dm.Flag = flag;
        }

        var sw = st.StartCount();
        try
        {
            //return client.SendMessage(msg);
            var codec = GetMessageCodec();
            var pk = codec.Write(null, msg) as Packet;
            client.SendAsync(pk.ToSegment(), WebSocketMessageType.Binary, true, default).Wait();

            return pk.Total;
        }
        catch (Exception ex)
        {
            // 跟踪异常
            span?.SetError(ex, args);

            throw;
        }
        finally
        {
            var msCost = st.StopCount(sw) / 1000;
            if (SlowTrace > 0 && msCost >= SlowTrace) WriteLog($"慢调用[{action}]，耗时{msCost:n0}ms");
        }
    }
    #endregion

    #region 异步接收
    /// <summary>客户端收到服务端主动下发消息</summary>
    /// <param name="message"></param>
    /// <param name="e"></param>
    protected virtual void OnReceive(IMessage message, ApiReceivedEventArgs e) => Received?.Invoke(this, e);

    private void Client_Received(Object? sender, ReceivedEventArgs e)
    {
        LastActive = DateTime.Now;

        // Api解码消息得到Action和参数
        if (e.Message is not IMessage msg) return;

        var apiMessage = Encoder.Decode(msg);
        var e2 = new ApiReceivedEventArgs
        {
            Remote = sender as ISocketRemote,
            Message = msg,
            ApiMessage = apiMessage,
            UserState = e,
        };

        OnReceive(msg, e2);
    }
    #endregion

    #region 登录
    /// <summary>新会话。客户端每次连接或断线重连后，可用InvokeWithClientAsync做登录</summary>
    /// <param name="client">会话</param>
    public virtual void OnNewSession(WebSocket client) => OnLoginAsync(client, true)?.Wait();

    /// <summary>连接后自动登录</summary>
    /// <param name="client">客户端</param>
    /// <param name="force">强制登录</param>
    protected virtual Task<Object?> OnLoginAsync(WebSocket client, Boolean force) => Task.FromResult<Object?>(null);

    /// <summary>登录</summary>
    /// <returns></returns>
    public virtual async Task<Object?> LoginAsync()
    {
        if (Cluster == null) throw new ArgumentNullException(nameof(Cluster));

        return await Cluster.InvokeAsync(client => OnLoginAsync(client, false)).ConfigureAwait(false);
    }
    #endregion

    #region 连接池
    /// <summary>创建客户端之后，打开连接之前</summary>
    /// <param name="svr"></param>
    protected virtual WebSocket OnCreate(String svr)
    {
        var url = svr.Replace("http://", "ws://").Replace("https://", "wss://");
        var uri = new Uri(new Uri(url), "/Device/Notify");

        using var span2 = Tracer?.NewSpan("WebSocketConnect", uri + "");

        var client = new ClientWebSocket();
        client.Options.SetRequestHeader("Authorization", "Bearer " + Token);

        DefaultSpan.Current?.AppendTag($"WebSocket.Connect {uri}");
        client.ConnectAsync(uri, default);

        return client;
    }
    #endregion

    #region 统计
    private TimerX? _Timer;
    private String? _Last;

    /// <summary>显示统计信息的周期。默认600秒，0表示不显示统计信息</summary>
    public Int32 StatPeriod { get; set; } = 600;

    private void DoWork(Object? state)
    {
        var sb = Pool.StringBuilder.Get();
        var pf1 = StatInvoke;
        if (pf1 != null && pf1.Value > 0) sb.AppendFormat("请求：{0} ", pf1);

        var msg = sb.Put(true);
        if (msg.IsNullOrEmpty() || msg == _Last) return;
        _Last = msg;

        WriteLog(msg);
    }
    #endregion
}