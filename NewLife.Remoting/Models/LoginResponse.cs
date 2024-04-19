namespace NewLife.Remoting.Models;

/// <summary>登录响应</summary>
public class LoginResponse
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

    /// <summary>服务器时间</summary>
    public Int64 Time { get; set; }
    #endregion
}
