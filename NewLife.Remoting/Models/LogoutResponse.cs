namespace NewLife.Remoting.Models;

/// <summary>设备注销响应</summary>
public class LogoutResponse
{
    #region 属性
    /// <summary>名称</summary>
    public String? Name { get; set; }

    /// <summary>令牌</summary>
    public String? Token { get; set; }
    #endregion
}