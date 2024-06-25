using NewLife.Http;
using NewLife.Model;
using NewLife.Net;
using NewLife.Serialization;
using HttpCodec = NewLife.Remoting.Http.HttpCodec;

namespace NewLife.Remoting;

class ApiHttpServer : ApiNetServer
{
    #region 属性
    //private String RawUrl;
    #endregion

    public ApiHttpServer()
    {
        Name = "Http";

        ProtocolType = NetType.Http;
    }

    /// <summary>初始化</summary>
    /// <param name="config"></param>
    /// <param name="host"></param>
    /// <returns></returns>
    public override Boolean Init(Object config, IApiHost host)
    {
        Host = host;

        if (config is NetUri uri) Port = uri.Port;

        //RawUrl = uri + "";
        var json = ServiceProvider?.GetService<IJsonHost>() ?? JsonHelper.Default;

        // Http封包协议
        //Add<HttpCodec>();
        Add(new HttpCodec { AllowParseHeader = true, JsonHost = json });

        //host.Handler = new ApiHttpHandler { Host = host };

        host.Encoder = new HttpEncoder { JsonHost = json };

        return true;
    }
}