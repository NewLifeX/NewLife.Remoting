namespace NewLife.Remoting.Models;

/// <summary>登录请求</summary>
public class LoginRequest
{
    #region 属性
    /// <summary>编码</summary>
    public String? Code { get; set; }

    /// <summary>密钥</summary>
    public String? Secret { get; set; }
    #endregion
}
