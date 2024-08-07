﻿namespace NewLife.Remoting.Models;

/// <summary>登录响应</summary>
public interface ILoginResponse
{
    /// <summary>编码。平台下发新编码</summary>
    String? Code { get; set; }

    /// <summary>密钥。平台下发新密钥</summary>
    String? Secret { get; set; }

    /// <summary>令牌</summary>
    String? Token { get; set; }

    /// <summary>令牌过期时间。单位秒</summary>
    Int32 Expire { get; set; }

    /// <summary>本地时间。客户端用于计算延迟，Unix毫秒（UTC）</summary>
    Int64 Time { get; set; }

    /// <summary>服务器时间。客户端用于计算时间差，Unix毫秒（UTC）</summary>
    Int64 ServerTime { get; set; }
}

/// <summary>登录响应</summary>
public class LoginResponse : ILoginResponse
{
    #region 属性
    /// <summary>编码。平台下发新编码</summary>
    public String? Code { get; set; }

    /// <summary>密钥。平台下发新密钥</summary>
    public String? Secret { get; set; }

    /// <summary>名称</summary>
    public String? Name { get; set; }

    /// <summary>令牌</summary>
    public String? Token { get; set; }

    /// <summary>令牌过期时间。单位秒</summary>
    public Int32 Expire { get; set; }

    /// <summary>本地时间。客户端用于计算延迟，Unix毫秒（UTC）</summary>
    public Int64 Time { get; set; }

    /// <summary>服务器时间。客户端用于计算时间差，Unix毫秒（UTC）</summary>
    public Int64 ServerTime { get; set; }
    #endregion

    /// <summary>已重载。</summary>
    /// <returns></returns>
    public override String? ToString() => !Name.IsNullOrEmpty() ? Name : Code;
}
