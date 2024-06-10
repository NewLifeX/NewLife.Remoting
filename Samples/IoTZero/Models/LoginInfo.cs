using NewLife.Remoting.Models;

namespace NewLife.IoT.Models;

/// <summary>节点登录信息</summary>
public class LoginInfo : ILoginRequest
{
    #region 属性
    /// <summary>设备编码</summary>
    public String Code { get; set; }

    /// <summary>设备密钥</summary>
    public String Secret { get; set; }

    /// <summary>产品证书</summary>
    public String ProductKey { get; set; }

    /// <summary>产品密钥</summary>
    public String ProductSecret { get; set; }

    /// <summary>实例。应用可能多实例部署，ip@proccessid</summary>
    public String ClientId { get; set; }

    /// <summary>名称。可用于标识设备的名称</summary>
    public String Name { get; set; }

    /// <summary>版本</summary>
    public String Version { get; set; }

    /// <summary>本地IP地址</summary>
    public String IP { get; set; }

    /// <summary>唯一标识</summary>
    public String UUID { get; set; }

    /// <summary>本地UTC时间</summary>
    public Int64 Time { get; set; }
    #endregion
}