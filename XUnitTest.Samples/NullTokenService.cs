using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Web;

namespace XUnitTest.Samples;

/// <summary>测试用 noop 令牌服务。替换 ITokenService，使 SendCommand 等接口在集成测试中无需真实应用令牌</summary>
internal sealed class NullTokenService : ITokenService
{
    /// <summary>颁发令牌（测试用，返回固定 token）</summary>
    /// <param name="name">名称</param>
    /// <param name="id">标识</param>
    /// <returns>令牌</returns>
    public IToken IssueToken(String name, String? id = null) => new TokenModel { AccessToken = "test", ExpireIn = 3600 };

    /// <summary>验证令牌（测试用，始终放行）</summary>
    /// <param name="token">令牌字符串</param>
    /// <returns>解析结果，始终无错误</returns>
    public (JwtBuilder, Exception?) DecodeToken(String token) => (new JwtBuilder(), null);
}
