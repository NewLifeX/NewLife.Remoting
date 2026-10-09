using NewLife.Web;

namespace NewLife.Remoting.Services;

/// <summary>令牌服务。颁发与验证令牌</summary>
public interface ITokenService
{
    /// <summary>颁发令牌（使用ITokenSetting中的密钥）。签发分离的访问令牌与刷新令牌</summary>
    /// <param name="name">主体名称，通常为应用或设备编码</param>
    /// <param name="id">令牌标识。为空时自动生成，用于识别会话</param>
    /// <returns>令牌模型，包含访问令牌与刷新令牌</returns>
    IToken IssueToken(String name, String? id = null);

    /// <summary>验证令牌。解码并校验签名、有效期与吊销黑名单，不区分访问/刷新令牌类型</summary>
    /// <param name="token">令牌字符串</param>
    /// <returns>解码后的Jwt对象与异常，解码成功且未吊销时异常为空</returns>
    (JwtBuilder, Exception?) DecodeToken(String token);

    /// <summary>吊销令牌。将令牌写入黑名单缓存，缓存存活到令牌过期后的一小段缓冲</summary>
    /// <param name="token">要吊销的令牌。访问令牌与刷新令牌各自独立吊销</param>
    /// <returns>吊销缓存生效秒数（含缓冲），0 表示未吊销（令牌无效、已过期或未配置缓存）</returns>
    Int32 RevokeToken(String token);
}
