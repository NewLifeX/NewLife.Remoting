using System;
using NewLife.Remoting.Models;

namespace NewLife.IoT.Models;

/// <summary>设备注销响应</summary>
public class LogoutResponse : ILogoutResponse
{
    #region 属性
    /// <summary>节点编码</summary>
    public String Code { get; set; }

    /// <summary>节点密钥</summary>
    public String Secret { get; set; }

    /// <summary>名称</summary>
    public String Name { get; set; }

    /// <summary>令牌</summary>
    public String Token { get; set; }
    #endregion
}