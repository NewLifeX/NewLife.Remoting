﻿namespace NewLife.Remoting.Models;

/// <summary>登录请求</summary>
public interface ILoginRequest
{
    /// <summary>编码</summary>
    String? Code { get; set; }

    /// <summary>密钥</summary>
    String? Secret { get; set; }

    /// <summary>实例。应用可能多实例部署，ip@proccessid</summary>
    String? ClientId { get; set; }

    ///// <summary>版本</summary>
    //String? Version { get; set; }

    ///// <summary>本地UTC时间</summary>
    //Int64 Time { get; set; }
}

/// <summary>登录请求</summary>
public class LoginRequest : ILoginRequest
{
    #region 属性
    /// <summary>编码</summary>
    public String? Code { get; set; }

    /// <summary>密钥</summary>
    public String? Secret { get; set; }

    /// <summary>实例。应用可能多实例部署，ip@proccessid</summary>
    public String? ClientId { get; set; }

    /// <summary>版本</summary>
    public String? Version { get; set; }

    /// <summary>编译时间。UTC毫秒</summary>
    public Int64 Compile { get; set; }

    /// <summary>本地IP地址</summary>
    public String? IP { get; set; }

    /// <summary>MAC地址</summary>
    public String? Macs { get; set; }

    /// <summary>唯一标识</summary>
    public String? UUID { get; set; }

    /// <summary>本地时间。UTC毫秒</summary>
    public Int64 Time { get; set; }
    #endregion
}
