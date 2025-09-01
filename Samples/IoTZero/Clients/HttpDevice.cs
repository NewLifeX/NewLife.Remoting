using NewLife.IoT.Models;
using NewLife.IoT.ThingModels;
using NewLife.Model;
using NewLife.Remoting.Clients;
using NewLife.Remoting.Models;
using NewLife.Security;
using NewLife.Threading;

namespace IoTEdge;

/// <summary>Http协议设备</summary>
public class HttpDevice : ClientBase
{
    #region 属性
    /// <summary>产品编码。从IoT管理平台获取</summary>
    public String ProductKey { get; set; }

    /// <summary>产品密钥</summary>
    public String ProductSecret { get; set; }

    private readonly ClientSetting _setting;
    private TimerX _timer;
    #endregion

    #region 构造
    public HttpDevice(ClientSetting setting) : base(setting)
    {
        // 设置动作，开启下行通知
        Features = Features.Login | Features.Logout | Features.Ping | Features.Notify | Features.Upgrade | Features.PostEvent;
        SetActions("Device/");
        Actions[Features.CommandReply] = "Thing/ServiceReply";

        _setting = setting;

        ProductKey = setting.ProductKey;
        ProductSecret = setting.DeviceSecret;
    }

    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        _timer.TryDispose();
        _timer = null;
    }
    #endregion

    #region 方法
    protected override void OnInit()
    {
        var provider = ServiceProvider ??= ObjectContainer.Provider;

        PasswordProvider = new SaltPasswordProvider { Algorithm = "md5", SaltTime = 60 };

        // 找到容器，注册默认的模型实现，供后续InvokeAsync时自动创建正确的模型对象
        var container = ModelExtension.GetService<IObjectContainer>(provider) ?? ObjectContainer.Current;
        if (container != null)
        {
            container.AddTransient<ILoginRequest, LoginInfo>();
            //container.AddTransient<ILoginResponse, LoginResponse>();
            //container.AddTransient<ILogoutResponse, LogoutResponse>();
            container.AddTransient<IPingRequest, PingInfo>();
            container.AddTransient<IPingResponse, MyPingResponse>();
            //container.AddTransient<IUpgradeInfo, UpgradeInfo>();
        }

        base.OnInit();
    }
    #endregion

    #region 登录
    public override async Task<ILoginResponse> Login(String source = null, CancellationToken cancellationToken = default)
    {
        var rs = await base.Login(source, cancellationToken);

        if (rs != null && Logined)
        {
            var period = _setting.PollingTime;
            if (period <= 0) period = 60_000;
            _timer = new TimerX(DoWork, null, 5_000, period * 1000) { Async = true };
        }

        return rs;
    }
    public override ILoginRequest BuildLoginRequest()
    {
        var request = new LoginInfo();
        FillLoginRequest(request);

        request.ProductKey = ProductKey;
        request.ProductSecret = ProductSecret;
        request.Name = Environment.MachineName;

        return request;
    }
    #endregion

    #region 心跳
    public override async Task<IPingResponse> Ping(CancellationToken cancellationToken = default)
    {
        var rs = await base.Ping(cancellationToken);
        if (rs is MyPingResponse mrs)
        {
            if (mrs.PollingTime > 0)
            {
                _setting.PollingTime = mrs.PollingTime;
                _setting.Save();
            }
        }

        return rs;
    }

    public override IPingRequest BuildPingRequest()
    {
        var request = new PingInfo();
        FillPingRequest(request);

        return request;
    }
    #endregion

    #region 数据
    private async Task DoWork(Object state)
    {
        await PostDataAsync();

        var period = _setting.PollingTime;
        if (period <= 0) period = 60_000;

        var timer = _timer;
        if (timer != null && timer.Period != period)
        {
            WriteLog("采集间隔由[{0}]修改为[{1}]", timer.Period, period);
            timer.Period = period;
        }
    }

    /// <summary>上传数据</summary>
    /// <returns></returns>
    public async Task PostDataAsync()
    {
        //if (Tracer != null) DefaultSpan.Current = null;

        using var span = Tracer?.NewSpan("PostData");
        try
        {
            var items = new List<DataModel>
            {
                new() {
                    Time = DateTime.UtcNow.ToLong(),
                    Name = "TestValue",
                    Value = Rand.Next(0, 100) + ""
                }
            };

            var data = new DataModels { DeviceCode = Code, Items = items.ToArray() };

            await InvokeAsync<Int32>("Thing/PostData", data);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);

            throw;
        }
    }
    #endregion
}