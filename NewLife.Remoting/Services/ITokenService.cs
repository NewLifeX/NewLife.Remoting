using NewLife.Web;

namespace NewLife.Remoting.Services;

/// <summary>令牌服务。颁发与验证令牌</summary>
public interface ITokenService
{
    /// <summary>颁发令牌（使用ITokenSetting中的密钥）</summary>
    IToken IssueToken(String name, String? id = null);

    /// <summary>验证令牌</summary>
    (JwtBuilder, Exception?) DecodeToken(String token);
}