using System;
using System.ComponentModel;
using NewLife.Remoting;
using NewLife.Remoting.Extensions.Services;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Web;
using Xunit;

namespace XUnitTest;

public class TokenServiceTests
{
    private static ITokenSetting CreateSetting(Int32 expire = 3600) => new TestTokenSetting
    {
        TokenSecret = "HS256:test_secret_key_for_jwt_tokens_123456",
        TokenExpire = expire,
        AutoRegister = true,
        SessionTimeout = 600,
    };

    private class TestTokenSetting : ITokenSetting
    {
        public String TokenSecret { get; set; } = null!;
        public Int32 TokenExpire { get; set; }
        public Boolean AutoRegister { get; set; }
        public Int32 SessionTimeout { get; set; }
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
}
