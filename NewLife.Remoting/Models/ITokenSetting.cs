namespace NewLife.Remoting.Models;

/// <summary>令牌设置</summary>
public interface ITokenSetting
{
    /// <summary>令牌密钥。用于生成JWT令牌的算法和密钥，如HS256:ABCD1234</summary>
    String TokenSecret { get; set; }

    /// <summary>令牌有效期。默认2*3600秒</summary>
    Int32 TokenExpire { get; set; }

    /// <summary>会话超时。默认600秒</summary>
    Int32 SessionTimeout { get; set; }

    /// <summary>自动注册。允许客户端自动注册，默认true</summary>
    Boolean AutoRegister { get; set; }
}

/// <summary>令牌设置（扩展）。增加刷新令牌有效期</summary>
public interface ITokenSetting2 : ITokenSetting
{
    /// <summary>刷新令牌有效期（秒）。0 表示采用访问令牌有效期的12倍</summary>
    Int32 RefreshExpire { get; set; }
}
