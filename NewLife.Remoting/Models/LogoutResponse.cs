namespace NewLife.Remoting.Models;

/// <summary>注销响应</summary>
public interface ILogoutResponse
{
    /// <summary>令牌</summary>
    public String? Token { get; set; }
}

/// <summary>注销响应</summary>
public class LogoutResponse : ILogoutResponse
{
    #region 属性
    /// <summary>名称</summary>
    public String? Name { get; set; }

    /// <summary>令牌</summary>
    public String? Token { get; set; }
    #endregion
}