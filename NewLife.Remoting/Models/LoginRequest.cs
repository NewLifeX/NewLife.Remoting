namespace NewLife.Remoting.Models;

/// <summary>登录请求</summary>
public interface ILoginRequest
{
    /// <summary>编码</summary>
    String? Code { get; set; }

    /// <summary>密钥</summary>
    String? Secret { get; set; }

    /// <summary>实例。应用可能多实例部署，ip@proccessid</summary>
    String? ClientId { get; set; }

    /// <summary>版本</summary>
    String? Version { get; set; }
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
    #endregion
}
