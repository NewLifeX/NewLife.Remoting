using System.Reflection;
using NewLife.Security;
using NewLife.Web;

namespace NewLife.Remoting.Extensions.Services;

/// <summary>应用服务</summary>
public class TokenService
{
    /// <summary>颁发令牌</summary>
    /// <param name="name"></param>
    /// <param name="secret"></param>
    /// <param name="expire"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    public TokenModel IssueToken(String name, String secret, Int32 expire, String? id = null)
    {
        if (id.IsNullOrEmpty()) id = Rand.NextString(8);

        // 颁发令牌
        var ss = secret.Split(':');
        var jwt = new JwtBuilder
        {
            Issuer = Assembly.GetEntryAssembly()?.GetName().Name,
            Subject = name,
            Id = id,
            Expire = DateTime.Now.AddSeconds(expire),

            Algorithm = ss[0],
            Secret = ss[1],
        };

        return new TokenModel
        {
            AccessToken = jwt.Encode(null!),
            TokenType = jwt.Type ?? "JWT",
            ExpireIn = expire,
            RefreshToken = jwt.Encode(null!),
        };
    }

    /// <summary>验证并续发新令牌，过期前10分钟才能续发</summary>
    /// <param name="name"></param>
    /// <param name="token"></param>
    /// <param name="secret"></param>
    /// <param name="expire"></param>
    /// <returns></returns>
    public TokenModel? ValidAndIssueToken(String name, String token, String secret, Int32 expire)
    {
        if (token.IsNullOrEmpty()) return null;

        // 令牌有效期检查，10分钟内过期者，重新颁发令牌
        var ss = secret.Split(':');
        var jwt = new JwtBuilder
        {
            Algorithm = ss[0],
            Secret = ss[1],
        };
        if (!jwt.TryDecode(token, out _)) return null;

        return DateTime.Now.AddMinutes(10) > jwt.Expire ? IssueToken(name, secret, expire) : null;
    }

    /// <summary>解码令牌</summary>
    /// <param name="token"></param>
    /// <param name="tokenSecret"></param>
    /// <returns></returns>
    public (JwtBuilder, Exception?) DecodeTokenWithError(String token, String tokenSecret)
    {
        if (token.IsNullOrEmpty()) throw new ArgumentNullException(nameof(token));

        // 解码令牌
        var ss = tokenSecret.Split(':');
        var jwt = new JwtBuilder
        {
            Algorithm = ss[0],
            Secret = ss[1],
        };

        Exception? ex = null;
        if (!jwt.TryDecode(token, out var message)) ex = new ApiException(ApiCode.Forbidden, $"[{jwt.Subject}]非法访问 {message}");

        return (jwt, ex);
    }

    /// <summary>解码令牌，得到App应用</summary>
    /// <param name="token"></param>
    /// <param name="tokenSecret"></param>
    /// <returns></returns>
    public JwtBuilder DecodeToken(String token, String tokenSecret)
    {
        if (token.IsNullOrEmpty()) throw new ArgumentNullException(nameof(token));

        // 解码令牌
        var ss = tokenSecret.Split(':');
        var jwt = new JwtBuilder
        {
            Algorithm = ss[0],
            Secret = ss[1],
        };
        if (!jwt.TryDecode(token, out var message) || jwt.Subject.IsNullOrEmpty())
            throw new ApiException(ApiCode.Forbidden, $"非法访问[{jwt.Subject}]，{message}");

        return jwt;
    }
}