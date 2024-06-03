using NewLife;
using NewLife.IoT.Models;
using NewLife.IoT.ThingModels;
using NewLife.Log;
using NewLife.Remoting.Clients;
using NewLife.Remoting.Models;
using NewLife.Security;
using LoginResponse = NewLife.Remoting.Models.LoginResponse;

namespace IoTEdge;

/// <summary>Http协议设备</summary>
public class HttpDevice : HttpClientBase
{
    #region 属性
    /// <summary>产品编码。从IoT管理平台获取</summary>
    public String ProductKey { get; set; }

    private readonly ClientSetting _setting;
    #endregion

    #region 构造
    public HttpDevice() => Prefix = "Device/";

    public HttpDevice(ClientSetting setting) : this()
    {
        _setting = setting;

        ProductKey = setting.ProductKey;

        AddServices(setting.Server);
    }
    #endregion

    #region 登录注销
    public override LoginRequest BuildLoginRequest()
    {
        var request = base.BuildLoginRequest();

        return new LoginInfo
        {
            Code = request.Code,
            Secret = request.Secret,
            Version = request.Version,
            ClientId = request.ClientId,

            ProductKey = ProductKey,
            //ProductSecret = _setting.DeviceSecret,
        };
    }

    public override async Task<LoginResponse> Login()
    {
        var rs = await base.Login();

        if (Logined && !rs.Secret.IsNullOrEmpty())
        {
            _setting.DeviceCode = rs.Code;
            _setting.DeviceSecret = rs.Secret;
            _setting.Save();
        }

        return rs;
    }
    #endregion

    #region 心跳
    public override PingRequest BuildPingRequest()
    {
        var request = base.BuildPingRequest();

        return new PingInfo
        {
        };
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