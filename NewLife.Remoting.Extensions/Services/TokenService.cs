using System.Reflection;
using NewLife;
using NewLife.Caching;
using NewLife.Log;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Security;
using NewLife.Web;

namespace NewLife.Remoting.Extensions.Services;

/// <summary>令牌服务。颁发与验证令牌</summary>
/// <remarks>可重载覆盖功能逻辑</remarks>
/// <param name="tokenSetting">令牌设置</param>
/// <param name="tracer">链路追踪</param>
/// <param name="cacheProvider">缓存提供者。用于保存吊销令牌黑名单，未配置时吊销功能停用</param>
public class TokenService(ITokenSetting tokenSetting, ITracer tracer, ICacheProvider? cacheProvider = null) : ITokenService
{
    /// <summary>吊销黑名单缓冲秒数。吊销缓存存活到令牌过期后的一小段缓冲，防止时钟偏差导致令牌复活</summary>
    private const Int32 RevokeExpire = 60;

    /// <summary>吊销缓存。使用全局缓存以便多实例共享，未配置时吊销功能停用</summary>
    private ICache? Cache => cacheProvider?.Cache;

    /// <summary>令牌配置</summary>
    protected virtual JwtBuilder GetJwt()
    {
        var ss = tokenSetting.TokenSecret.Split(':');
        if (ss.Length < 2 || ss[0].IsNullOrEmpty() || ss[1].IsNullOrEmpty())
            throw new InvalidOperationException("令牌密钥格式错误，应为 算法:密钥，例如 HS256:xxxx");

        return new JwtBuilder
        {
            Algorithm = ss[0],
            Secret = ss[1],
        };
    }

    /// <summary>颁发令牌（使用ITokenSetting中的密钥）。签发分离的访问令牌与刷新令牌</summary>
    /// <remarks>
    /// 访问令牌（AccessToken）有效期为 <see cref="ITokenSetting.TokenExpire"/>，用于调用业务接口；
    /// 刷新令牌（RefreshToken）有效期更长（默认 TokenExpire 的12倍，可用 <see cref="ITokenSetting2.RefreshExpire"/> 覆盖），
    /// 只能用于刷新，不能作为访问令牌调用业务接口。两者通过 kind 声明区分。
    /// </remarks>
    /// <param name="name">主体名称，通常为应用或设备编码</param>
    /// <param name="id">令牌标识。为空时自动生成，用于识别会话</param>
    /// <returns>令牌模型，包含访问令牌与刷新令牌</returns>
    public virtual IToken IssueToken(String name, String? id = null)
    {
        if (id.IsNullOrEmpty()) id = Rand.NextString(8);

        using var span = tracer?.NewSpan(nameof(IssueToken), new { name, id });

        var now = DateTime.Now;

        // 访问令牌：短有效期，只能调用业务接口
        var jwt = GetJwt();
        jwt.Issuer = Assembly.GetEntryAssembly()?.GetName().Name;
        jwt.Subject = name;
        jwt.Id = id;
        jwt.Expire = now.AddSeconds(tokenSetting.TokenExpire);
        var access = jwt.Encode(new { kind = "access" });

        // 刷新令牌：长有效期，只能用于刷新，不能作为访问令牌
        var refreshExpire = tokenSetting is ITokenSetting2 ts2 && ts2.RefreshExpire > 0
            ? ts2.RefreshExpire
            : tokenSetting.TokenExpire * 12;
        jwt.Expire = now.AddSeconds(refreshExpire);
        var refresh = jwt.Encode(new { kind = "refresh" });

        return new TokenModel
        {
            AccessToken = access,
            TokenType = jwt.Type ?? "JWT",
            ExpireIn = tokenSetting.TokenExpire,
            RefreshToken = refresh,
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

    /// <summary>解码令牌。校验签名、有效期与吊销黑名单</summary>
    /// <remarks>
    /// 本方法只做通用解码与吊销校验，不区分访问/刷新令牌类型；
    /// 类型互斥由消费方负责：访问接口拒绝刷新令牌，刷新接口拒绝访问令牌。
    /// </remarks>
    /// <param name="token">令牌字符串</param>
    /// <returns>解码后的Jwt对象与异常。解码成功且未吊销时异常为空</returns>
    public virtual (JwtBuilder, Exception?) DecodeToken(String token)
    {
        if (token.IsNullOrEmpty()) throw new ArgumentNullException(nameof(token));

        // 解码令牌
        var jwt = GetJwt();

        Exception? ex = null;
        if (!jwt.TryDecode(token, out var message))
            ex = new ApiException(ApiCode.Unauthorized, $"非法访问[{jwt.Subject}] {message}");
        else if (IsRevoked(token))
            ex = new ApiException(ApiCode.Unauthorized, "令牌已吊销！");

        return (jwt, ex);
    }

    /// <summary>吊销令牌。把令牌写入黑名单缓存，缓存存活到令牌过期后一小段缓冲，令牌到期后自动失效</summary>
    /// <remarks>
    /// 缓存键采用令牌 MD5，缓存值保存令牌原文；校验时取出原文比对，
    /// 避免两个不同令牌 MD5 碰撞时误伤未吊销令牌。
    /// </remarks>
    /// <param name="token">要吊销的令牌。访问令牌与刷新令牌各自独立吊销</param>
    /// <returns>吊销缓存生效秒数（含缓冲），0 表示未吊销（令牌无效、已过期或未配置缓存）</returns>
    public virtual Int32 RevokeToken(String token)
    {
        if (token.IsNullOrEmpty()) return 0;

        var cache = Cache;
        if (cache == null) return 0;

        // 解码获取过期时间，已过期的令牌无需吊销
        var jwt = GetJwt();
        if (!jwt.TryDecode(token, out _)) return 0;
        if (jwt.Expire.Year <= 2000 || jwt.Expire <= DateTime.Now) return 0;

        // 缓存存活 = 令牌剩余有效秒数 + 缓冲，确保覆盖到令牌过期之后
        var remain = (Int32)(jwt.Expire - DateTime.Now).TotalSeconds + RevokeExpire;
        cache.Set(RevokeKey(token), token, remain);

        return remain;
    }

    /// <summary>是否已吊销。取出缓存原文与当前令牌比对，防止MD5碰撞误伤</summary>
    /// <param name="token">令牌字符串</param>
    /// <returns>是否已吊销</returns>
    private Boolean IsRevoked(String token)
    {
        var cache = Cache;
        if (cache == null) return false;

        return cache.TryGetValue<String>(RevokeKey(token), out var old) && old == token;
    }

    /// <summary>吊销缓存键。采用令牌MD5缩短键长度</summary>
    /// <param name="token">令牌字符串</param>
    /// <returns>缓存键</returns>
    private static String RevokeKey(String token) => "TokenRevoke:" + token.MD5();
}
