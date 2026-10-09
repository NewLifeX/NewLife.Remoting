using System;
using System.ComponentModel;
using NewLife;
using NewLife.Caching;
using NewLife.Remoting;
using NewLife.Remoting.Extensions.Services;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Web;
using Xunit;

namespace XUnitTest.Services;

public class TokenServiceTests
{
    private static ITokenSetting CreateSetting(Int32 expire = 3600) => new TestTokenSetting
    {
        TokenSecret = "HS256:test_secret_key_for_jwt_tokens_123456",
        TokenExpire = expire,
        AutoRegister = true,
        SessionTimeout = 600,
    };

    /// <summary>创建带独立内存缓存的令牌服务，避免测试间共享全局缓存</summary>
    private static (TokenService service, MemoryCache cache) CreateServiceWithCache(Int32 expire = 3600)
    {
        var cache = new MemoryCache();
        var provider = new CacheProvider { Cache = cache };
        var service = new TokenService(CreateSetting(expire), null!, provider);

        return (service, cache);
    }

    private class TestTokenSetting : ITokenSetting
    {
        public String TokenSecret { get; set; } = null!;
        public Int32 TokenExpire { get; set; }
        public Boolean AutoRegister { get; set; }
        public Int32 SessionTimeout { get; set; }
    }

    private class TestTokenSetting2 : TestTokenSetting, ITokenSetting2
    {
        public Int32 RefreshExpire { get; set; }
    }

    [Fact]
    [DisplayName("颁发令牌")]
    public void IssueToken_Basic()
    {
        var setting = CreateSetting();
        var service = new TokenService(setting, null!);

        var token = service.IssueToken("testApp");

        Assert.NotNull(token);
        Assert.NotNull(token.AccessToken);
        Assert.NotEmpty(token.AccessToken!);
        Assert.NotNull(token.RefreshToken);
        Assert.NotEmpty(token.RefreshToken!);
        Assert.Equal(3600, token.ExpireIn);
    }

    [Fact]
    [DisplayName("颁发令牌带Id")]
    public void IssueToken_WithId()
    {
        var setting = CreateSetting();
        var service = new TokenService(setting, null!);

        var token = service.IssueToken("testApp", "myClientId");

        Assert.NotNull(token);
        Assert.NotNull(token.AccessToken);
    }

    [Fact]
    [DisplayName("解码令牌成功")]
    public void DecodeToken_Success()
    {
        var setting = CreateSetting();
        var service = new TokenService(setting, null!);

        var token = service.IssueToken("testApp", "clientId123");
        var (jwt, ex) = service.DecodeToken(token.AccessToken!);

        Assert.NotNull(jwt);
        Assert.Null(ex);
        Assert.Equal("testApp", jwt.Subject);
        Assert.Equal("clientId123", jwt.Id);
    }

    [Fact]
    [DisplayName("解码令牌_空令牌抛异常")]
    public void DecodeToken_EmptyToken_Throws()
    {
        var setting = CreateSetting();
        var service = new TokenService(setting, null!);

        Assert.Throws<ArgumentNullException>(() => service.DecodeToken(""));
    }

    [Fact]
    [DisplayName("解码令牌_无效令牌抛异常")]
    public void DecodeToken_InvalidToken()
    {
        var setting = CreateSetting();
        var service = new TokenService(setting, null!);

        // 无效令牌在解析JWT时抛出异常
        Assert.ThrowsAny<Exception>(() => service.DecodeToken("invalid.jwt.token"));
    }

    [Fact]
    [DisplayName("过期时间生效")]
    public void IssueToken_ExpireTime()
    {
        var setting = CreateSetting(7200);
        var service = new TokenService(setting, null!);

        var token = service.IssueToken("testApp");

        Assert.Equal(7200, token.ExpireIn);
    }

    [Fact]
    [DisplayName("颁发并解码往返")]
    public void IssueAndDecode_RoundTrip()
    {
        var setting = CreateSetting();
        var service = new TokenService(setting, null!);

        var token = service.IssueToken("myApp", "sessionABC");
        var (jwt, ex) = service.DecodeToken(token.AccessToken!);

        Assert.Null(ex);
        Assert.Equal("myApp", jwt!.Subject);
        Assert.Equal("sessionABC", jwt.Id);
    }

    [Fact]
    [DisplayName("不同密钥解码失败")]
    public void DecodeToken_DifferentSecret_Fails()
    {
        var setting1 = CreateSetting();
        var service1 = new TokenService(setting1, null!);
        var token = service1.IssueToken("app1");

        var setting2 = new TestTokenSetting
        {
            TokenSecret = "HS256:different_secret_key_that_is_wrong_12345",
            TokenExpire = 3600,
            AutoRegister = false
        };
        var service2 = new TokenService(setting2, null!);

        var (jwt, ex) = service2.DecodeToken(token.AccessToken!);

        Assert.NotNull(ex);
    }

    [Fact]
    [DisplayName("令牌密钥缺少冒号抛异常")]
    public void IssueToken_MalformedSecret_Throws()
    {
        var setting = new TestTokenSetting
        {
            TokenSecret = "HS256secret_no_colon",
            TokenExpire = 3600,
            AutoRegister = false
        };
        var service = new TokenService(setting, null!);

        Assert.Throws<InvalidOperationException>(() => service.IssueToken("testApp"));
    }

    [Fact]
    [DisplayName("令牌密钥算法为空抛异常")]
    public void IssueToken_EmptyAlgorithm_Throws()
    {
        var setting = new TestTokenSetting
        {
            TokenSecret = ":secret_only",
            TokenExpire = 3600,
            AutoRegister = false
        };
        var service = new TokenService(setting, null!);

        Assert.Throws<InvalidOperationException>(() => service.IssueToken("testApp"));
    }

    [Fact]
    [DisplayName("吊销令牌后解码返回已吊销")]
    public void RevokeToken_Decode_Revoked()
    {
        var (service, _) = CreateServiceWithCache();

        var token = service.IssueToken("testApp", "clientId123");
        var remain = service.RevokeToken(token.AccessToken!);

        Assert.True(remain > 0);

        var (jwt, ex) = service.DecodeToken(token.AccessToken!);
        Assert.NotNull(jwt);
        Assert.NotNull(ex);
        var aex = Assert.IsType<ApiException>(ex);
        Assert.Equal(ApiCode.Unauthorized, aex.Code);
    }

    [Fact]
    [DisplayName("吊销令牌不影响其他令牌")]
    public void RevokeToken_OtherToken_Unchanged()
    {
        var (service, _) = CreateServiceWithCache();

        var t1 = service.IssueToken("testApp", "client1");
        var t2 = service.IssueToken("testApp", "client2");

        service.RevokeToken(t1.AccessToken!);

        var (jwt, ex) = service.DecodeToken(t2.AccessToken!);
        Assert.NotNull(jwt);
        Assert.Null(ex);
    }

    [Fact]
    [DisplayName("黑名单缓存保存令牌原文且存活期≈剩余+缓冲")]
    public void RevokeToken_CacheStoresTokenAndTtl()
    {
        var (service, cache) = CreateServiceWithCache(7200);

        var token = service.IssueToken("testApp", "clientId123");
        var remain = service.RevokeToken(token.AccessToken!);

        var key = "TokenRevoke:" + token.AccessToken!.MD5();
        // 缓存值保存令牌原文，用于校验时碰撞比对
        var value = cache.Get<String>(key);
        Assert.Equal(token.AccessToken, value);

        // 存活期约为剩余有效秒 + 60s 缓冲
        var exp = cache.GetExpire(key);
        Assert.True(exp > TimeSpan.Zero);
        Assert.InRange(exp.TotalSeconds, remain - 10, remain);
    }

    [Fact]
    [DisplayName("空或非法令牌吊销返回0")]
    public void RevokeToken_InvalidToken_ReturnsZero()
    {
        var (service, _) = CreateServiceWithCache();

        Assert.Equal(0, service.RevokeToken(""));
        Assert.Equal(0, service.RevokeToken("not-a-token"));
    }

    [Fact]
    [DisplayName("已过期令牌吊销返回0")]
    public void RevokeToken_ExpiredToken_ReturnsZero()
    {
        var (service, cache) = CreateServiceWithCache();

        var jwt = new JwtBuilder
        {
            Algorithm = "HS256",
            Secret = "test_secret_key_for_jwt_tokens_123456",
            Subject = "testApp",
            Expire = DateTime.Now.AddSeconds(-10),
        };
        var token = jwt.Encode(null!);

        Assert.Equal(0, service.RevokeToken(token));
        Assert.False(cache.ContainsKey("TokenRevoke:" + token.MD5()));
    }

    [Fact]
    [DisplayName("未配置缓存时吊销停用解码正常")]
    public void RevokeToken_NoCache_Noop()
    {
        var setting = CreateSetting();
        var service = new TokenService(setting, null!);

        var token = service.IssueToken("testApp");

        Assert.Equal(0, service.RevokeToken(token.AccessToken!));

        var (_, ex) = service.DecodeToken(token.AccessToken!);
        Assert.Null(ex);
    }

    [Fact]
    [DisplayName("访问令牌与刷新令牌分离且类型正确")]
    public void IssueToken_AccessAndRefreshSeparated()
    {
        var setting = CreateSetting(3600);
        var service = new TokenService(setting, null!);

        var token = service.IssueToken("testApp", "clientId123");

        Assert.NotNull(token.AccessToken);
        Assert.NotNull(token.RefreshToken);
        Assert.NotEqual(token.AccessToken, token.RefreshToken);

        var (jwtA, exA) = service.DecodeToken(token.AccessToken!);
        Assert.Null(exA);
        Assert.Equal("access", jwtA!["kind"] + "");

        var (jwtR, exR) = service.DecodeToken(token.RefreshToken!);
        Assert.Null(exR);
        Assert.Equal("refresh", jwtR!["kind"] + "");

        // 刷新令牌有效期更长，默认访问令牌的12倍
        Assert.True(jwtR.Expire > jwtA.Expire);
        var span = jwtR.Expire - DateTime.Now;
        Assert.InRange(span.TotalSeconds, 3600 * 12 - 120, 3600 * 12 + 120);
    }

    [Fact]
    [DisplayName("ITokenSetting2覆盖刷新令牌有效期")]
    public void IssueToken_RefreshExpire_FromSetting2()
    {
        var setting = new TestTokenSetting2
        {
            TokenSecret = "HS256:test_secret_key_for_jwt_tokens_123456",
            TokenExpire = 3600,
            AutoRegister = true,
            SessionTimeout = 600,
            RefreshExpire = 7200,
        };
        var service = new TokenService(setting, null!);

        var token = service.IssueToken("testApp");

        var (jwtR, exR) = service.DecodeToken(token.RefreshToken!);
        Assert.Null(exR);
        var span = jwtR!.Expire - DateTime.Now;
        Assert.InRange(span.TotalSeconds, 7200 - 120, 7200 + 120);
    }
}
