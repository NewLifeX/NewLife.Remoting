using NewLife;
using NewLife.IoT.Models;
using NewLife.IoT.ThingModels;
using NewLife.Log;
using NewLife.Model;
using NewLife.Remoting;
using NewLife.Remoting.Clients;
using NewLife.Remoting.Models;
using NewLife.Security;

namespace IoTEdge;

/// <summary>Http协议设备</summary>
public class HttpDevice : ClientBase
{
    #region 属性
    /// <summary>产品编码。从IoT管理平台获取</summary>
    public String ProductKey { get; set; }

    private readonly ClientSetting _setting;
    #endregion

    #region 构造
    public HttpDevice() => Prefix = "Device/";

    public HttpDevice(ClientSetting setting) : base(setting)
    {
        _setting = setting;

        ProductKey = setting.ProductKey;
    }
    #endregion

    #region 方法
    protected override void OnInit()
    {
        var provider = ServiceProvider ??= ObjectContainer.Provider;

        // 找到容器，注册默认的模型实现，供后续InvokeAsync时自动创建正确的模型对象
        var container = ModelExtension.GetService<IObjectContainer>(provider) ?? ObjectContainer.Current;
        if (container != null)
        {
            container.TryAddTransient<ILoginRequest, LoginInfo>();
            //container.TryAddTransient<ILoginResponse, LoginResponse>();
            //container.TryAddTransient<ILogoutResponse, LogoutResponse>();
            container.TryAddTransient<IPingRequest, PingInfo>();
            //container.TryAddTransient<IPingResponse, PingResponse>();
            //container.TryAddTransient<IUpgradeInfo, UpgradeInfo>();
        }

        base.OnInit();
    }
    #endregion

    #region 登录注销
    public override ILoginRequest BuildLoginRequest()
    {
        var request = base.BuildLoginRequest();
        if (request is LoginInfo info)
        {
            info.ProductKey = ProductKey;
            info.Name = Environment.MachineName;
            info.IP = NetHelper.MyIP() + "";

            var mi = MachineInfo.GetCurrent();
            info.UUID = mi.BuildCode();
        }

        return request;
    }
    #endregion

    #region 心跳
    public override IPingRequest BuildPingRequest()
    {
        var request = base.BuildPingRequest();
        if (request is PingInfo info)
        {

        }

        return request;
    }

    public override Task<Object> CommandReply(CommandReplyModel model) => InvokeAsync<Object>("Thing/ServiceReply", new ServiceReplyModel
    {
        Id = model.Id,
        Status = (ServiceStatus)model.Status,
        Data = model.Data,
    });
    #endregion

    #region 数据
    /// <summary>上传数据</summary>
    /// <returns></returns>
    public async Task PostDataAsync()
    {
        if (Tracer != null) DefaultSpan.Current = null;

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