using System.Reflection;
using NewLife.Log;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Security;
using NewLife.Web;

namespace NewLife.Remoting.Extensions.Services;

/// <summary>令牌服务。颁发与验证令牌</summary>
/// <remarks>可重载覆盖功能逻辑</remarks>
/// <param name="tokenSetting"></param>
/// <param name="tracer"></param>
public class TokenService(ITokenSetting tokenSetting, ITracer tracer) : ITokenService
{
    /// <summary>令牌配置</summary>
    protected virtual JwtBuilder GetJwt()
    {
        var ss = tokenSetting.TokenSecret.Split(':');
        return new JwtBuilder
        {
            Algorithm = ss[0],
            Secret = ss[1],
        };
    }

    /// <summary>颁发令牌（使用ITokenSetting中的密钥）</summary>
    /// <param name="name"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    public virtual TokenModel IssueToken(String name, String? id = null)
    {
        if (id.IsNullOrEmpty()) id = Rand.NextString(8);

        using var span = tracer?.NewSpan(nameof(IssueToken), new { name, id });

        // 颁发令牌
        var jwt = GetJwt();
        jwt.Issuer = Assembly.GetEntryAssembly()?.GetName().Name;
        jwt.Subject = name;
        jwt.Id = id;
        jwt.Expire = DateTime.Now.AddSeconds(tokenSetting.TokenExpire);

        return new TokenModel
        {
            AccessToken = jwt.Encode(null!),
            TokenType = jwt.Type ?? "JWT",
            ExpireIn = tokenSetting.TokenExpire,
            RefreshToken = jwt.Encode(null!),
        };
    }

    ///// <summary>验证并续发新令牌，过期前10分钟才能续发</summary>
    ///// <param name="name"></param>
    ///// <param name="token"></param>
    ///// <returns></returns>
    //public virtual TokenModel? ValidAndIssueToken(String name, String token)
    //{
    //    if (token.IsNullOrEmpty()) return null;

    //    // 令牌有效期检查，10分钟内过期者，重新颁发令牌
    //    var jwt = GetJwt();
    //    if (!jwt.TryDecode(token, out _)) return null;

    //    return DateTime.Now.AddMinutes(10) > jwt.Expire ? IssueToken(name, jwt.Id) : null;
    //}

    /// <summary>解码令牌</summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public virtual (JwtBuilder, Exception?) DecodeToken(String token)
    {
        if (token.IsNullOrEmpty()) throw new ArgumentNullException(nameof(token));

        // 解码令牌
        var jwt = GetJwt();

        Exception? ex = null;
        if (!jwt.TryDecode(token, out var message))
            ex = new ApiException(ApiCode.Forbidden, $"非法访问[{jwt.Subject}] {message}");

        return (jwt, ex);
    }
}