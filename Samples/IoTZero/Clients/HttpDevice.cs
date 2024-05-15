using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.WebSockets;
using NewLife;
using NewLife.Caching;
using NewLife.IoT.Models;
using NewLife.IoT.ThingModels;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Security;
using NewLife.Serialization;
using NewLife.Threading;

namespace IoTEdge;

/// <summary>Http协议设备</summary>
public class HttpDevice : DisposeBase
{
    #region 属性
    /// <summary>服务器地址</summary>
    public String Server { get; set; }

    /// <summary>设备编码。从IoT管理平台获取（需提前分配），或者本地提交后动态注册</summary>
    public String DeviceCode { get; set; }

    /// <summary>密钥。设备密钥或产品密钥，分别用于一机一密和一型一密，从IoT管理平台获取</summary>
    public String DeviceSecret { get; set; }

    /// <summary>产品编码。从IoT管理平台获取</summary>
    public String ProductKey { get; set; }

    /// <summary>密码散列提供者。避免密码明文提交</summary>
    public IPasswordProvider PasswordProvider { get; set; } = new SaltPasswordProvider { Algorithm = "md5", SaltTime = 60 };

    /// <summary>收到命令时触发</summary>
    public event EventHandler<ServiceEventArgs> Received;

    private readonly ClientSetting _setting;
    private ApiHttpClient _client;
    private Int32 _delay;
    #endregion

    #region 构造
    public HttpDevice() { }

    public HttpDevice(ClientSetting setting)
    {
        _setting = setting;

        Server = setting.Server;
        DeviceCode = setting.DeviceCode;
        DeviceSecret = setting.DeviceSecret;
        ProductKey = setting.ProductKey;
    }

    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        StopTimer();
    }
    #endregion

    #region 登录注销
    /// <summary>
    /// 登录
    /// </summary>
    /// <param name="inf"></param>
    /// <returns></returns>
    public async Task LoginAsync()
    {
        var client = new ApiHttpClient(Server)
        {
            Tracer = Tracer,
            Log = XTrace.Log,
        };

        var info = new LoginInfo
        {
            Code = DeviceCode,
            Secret = DeviceSecret.IsNullOrEmpty() ? null : PasswordProvider.Hash(DeviceSecret),
            ProductKey = ProductKey,
        };
        var rs = await client.PostAsync<LoginResponse>("Device/Login", info);
        client.Token = rs.Token;

        if (!rs.Code.IsNullOrEmpty() && !rs.Secret.IsNullOrEmpty())
        {
            WriteLog("下发证书：{0}/{1}", rs.Code, rs.Secret);
            DeviceCode = rs.Code;
            DeviceSecret = rs.Secret;

            _setting.DeviceCode = rs.Code;
            _setting.DeviceSecret = rs.Secret;
            _setting.Save();
        }

        _client = client;

        StartTimer();
    }

    /// <summary>注销</summary>
    /// <param name="reason"></param>
    /// <returns></returns>
    public async Task LogoutAsync(String reason) => await _client.PostAsync<LogoutResponse>("Device/Logout", new { reason });
    #endregion

    #region 心跳&长连接
    /// <summary>心跳</summary>
    /// <returns></returns>
    public virtual async Task PingAsync()
    {
        if (Tracer != null) DefaultSpan.Current = null;

        using var span = Tracer?.NewSpan("Ping");
        try
        {
            var info = GetHeartInfo();

            var rs = await _client.PostAsync<PingResponse>("Device/Ping", info);

            var dt = rs.Time.ToDateTime();
            if (dt.Year > 2000)
            {
                // 计算延迟
                var ts = DateTime.UtcNow - dt;
                var ms = (Int32)ts.TotalMilliseconds;
                _delay = (_delay + ms) / 2;
            }

            var svc = _client.Services.FirstOrDefault();
            if (svc != null && _client.Token != null && (_websocket == null || _websocket.State != WebSocketState.Open))
            {
                var url = svc.Address.ToString().Replace("http://", "ws://").Replace("https://", "wss://");
                var uri = new Uri(new Uri(url), "/Device/Notify");
                var client = new ClientWebSocket();
                client.Options.SetRequestHeader("Authorization", "Bearer " + _client.Token);
                await client.ConnectAsync(uri, default);

                _websocket = client;

                _source = new CancellationTokenSource();
                _ = Task.Run(() => DoPull(client, _source.Token));
            }

            // 令牌
            if (rs is PingResponse pr && !pr.Token.IsNullOrEmpty())
                _client.Token = pr.Token;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);

            throw;
        }
    }

    /// <summary>获取心跳信息</summary>
    public PingInfo GetHeartInfo()
    {
        var mi = MachineInfo.GetCurrent();
        mi.Refresh();

        var properties = IPGlobalProperties.GetIPGlobalProperties();
        var connections = properties.GetActiveTcpConnections();

        var mcs = NetHelper.GetMacs().Select(e => e.ToHex("-")).OrderBy(e => e).Join(",");
        var driveInfo = new DriveInfo(Path.GetPathRoot(".".GetFullPath()));
        var ip = NetHelper.GetIPs().Where(ip => ip.IsIPv4() && !IPAddress.IsLoopback(ip) && ip.GetAddressBytes()[0] != 169).Join();
        var ext = new PingInfo
        {
            Memory = mi.Memory,
            AvailableMemory = mi.AvailableMemory,
            TotalSize = (UInt64)driveInfo.TotalSize,
            AvailableFreeSpace = (UInt64)driveInfo.AvailableFreeSpace,
            CpuRate = (Single)Math.Round(mi.CpuRate, 4),
            Temperature = mi.Temperature,
            Battery = mi.Battery,
            Uptime = Environment.TickCount / 1000,

            IP = ip,

            Time = DateTime.UtcNow.ToLong(),
            Delay = _delay,
        };
        // 开始时间 Environment.TickCount 很容易溢出，导致开机24天后变成负数。
        // 后来在 netcore3.0 增加了Environment.TickCount64
        // 现在借助 Stopwatch 来解决
        if (Stopwatch.IsHighResolution) ext.Uptime = (Int32)(Stopwatch.GetTimestamp() / Stopwatch.Frequency);

        return ext;
    }

    private TimerX _timer;
    /// <summary>开始心跳定时器</summary>
    protected virtual void StartTimer()
    {
        if (_timer == null)
            lock (this)
                _timer ??= new TimerX(async s => await PingAsync(), null, 3_000, 60_000, "Device") { Async = true };
    }

    /// <summary>停止心跳定时器</summary>
    protected void StopTimer()
    {
        if (_websocket != null && _websocket.State == WebSocketState.Open)
            _websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "finish", default).Wait();
        _source?.Cancel();

        _websocket = null;

        _timer.TryDispose();
        _timer = null;
    }

    private WebSocket _websocket;
    private CancellationTokenSource _source;
    private ICache _cache = new MemoryCache();

    private async Task DoPull(WebSocket socket, CancellationToken cancellationToken)
    {
        DefaultSpan.Current = null;
        try
        {
            var buf = new Byte[4 * 1024];
            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var data = await socket.ReceiveAsync(new ArraySegment<Byte>(buf), cancellationToken);
                var model = buf.ToStr(null, 0, data.Count).ToJsonEntity<ServiceModel>();
                if (model != null && _cache.Add($"cmd:{model.Id}", model, 3600))
                {
                    // 建立追踪链路
                    using var span = Tracer?.NewSpan("service:" + model.Name, model);
                    span?.Detach(model.TraceId);
                    try
                    {
                        if (model.Expire.Year < 2000 || model.Expire > DateTime.Now)
                            await OnReceiveCommand(model);
                        else
                            await ServiceReply(new ServiceReplyModel { Id = model.Id, Status = ServiceStatus.取消 });
                    }
                    catch (Exception ex)
                    {
                        span?.SetError(ex, null);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }

        if (socket.State == WebSocketState.Open) await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "finish", default);
    }
    #endregion

    #region 服务
    /// <summary>
    /// 触发收到命令的动作
    /// </summary>
    /// <param name="model"></param>
    protected virtual async Task OnReceiveCommand(ServiceModel model)
    {
        var e = new ServiceEventArgs { Model = model };
        Received?.Invoke(this, e);

        if (e.Reply != null) await ServiceReply(e.Reply);
    }

    /// <summary>上报服务调用结果</summary>
    /// <param name="model"></param>
    /// <returns></returns>
    public virtual async Task<Object> ServiceReply(ServiceReplyModel model) => await _client.PostAsync<Object>("Thing/ServiceReply", model);
    #endregion

    public async Task PostDataAsync()
    {
        if (Tracer != null) DefaultSpan.Current = null;

        using var span = Tracer?.NewSpan("PostData");
        try
        {
            var items = new List<DataModel>
            {
                new DataModel
                {
                    Time = DateTime.UtcNow.ToLong(),
                    Name = "TestValue",
                    Value = Rand.Next(0, 100) + ""
                }
            };

            var data = new DataModels { DeviceCode = DeviceCode, Items = items.ToArray() };

            await _client.PostAsync<Int32>("Thing/PostData", data);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);

            throw;
        }
    }

    #region 日志
    /// <summary>链路追踪</summary>
    public ITracer Tracer { get; set; }

    /// <summary>日志</summary>
    public ILog Log { get; set; }

    /// <summary>写日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object[] args) => Log?.Info(format, args);
    #endregion
}