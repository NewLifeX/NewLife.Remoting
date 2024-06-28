using NewLife;
using NewLife.IoT.Models;
using NewLife.IoT.ThingModels;
using NewLife.Model;
using NewLife.Remoting;
using NewLife.Remoting.Clients;
using NewLife.Remoting.Models;
using NewLife.Security;

namespace ZeroClient;

/// <summary>Http协议设备</summary>
public class HttpDevice : ClientBase
{
    #region 属性
    private readonly ClientSetting _setting;
    #endregion

    #region 构造
    public HttpDevice(ClientSetting setting) : base(setting)
    {
        // 设置动作，开启下行通知
        Features = Features.Login | Features.Logout | Features.Ping | Features.Notify | Features.Upgrade;
        SetActions("Node/");

        _setting = setting;
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
            container.TryAddTransient<ILoginRequest, LoginInfo>();
            container.TryAddTransient<IPingRequest, PingInfo>();
        }

        base.OnInit();
    }
    #endregion

    #region 登录
    public override ILoginRequest BuildLoginRequest()
    {
        var request = base.BuildLoginRequest();
        if (request is LoginInfo info)
        {
            info.Name = Environment.MachineName;
            info.IP = NetHelper.MyIP() + "";
            info.UUID = MachineInfo.GetCurrent().BuildCode();
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
            info.Memory = 0;
            info.TotalSize = 0;
        }

        return request;
    }
    #endregion

    #region 数据
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