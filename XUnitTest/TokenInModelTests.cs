using System;
using System.ComponentModel;
using NewLife.Remoting.Extensions.Models;
using Xunit;

namespace XUnitTest;

public class TokenInModelTests
{
    [Fact]
    [DisplayName("属性读写")]
    public void Properties_SetAndGet()
    {
        var model = new TokenInModel
        {
            grant_type = "password",
            UserName = "admin",
            Password = "secret",
            ClientId = "192.168.1.1@1234",
            refresh_token = "rt_abc123"
        };

        Assert.Equal("password", model.grant_type);
        Assert.Equal("admin", model.UserName);
        Assert.Equal("secret", model.Password);
        Assert.Equal("192.168.1.1@1234", model.ClientId);
        Assert.Equal("rt_abc123", model.refresh_token);
    }

    [Fact]
    [DisplayName("默认值全为null")]
    public void DefaultValues()
    {
        var model = new TokenInModel();

        Assert.Null(model.grant_type);
        Assert.Null(model.UserName);
        Assert.Null(model.Password);
        Assert.Null(model.ClientId);
        Assert.Null(model.refresh_token);
    }
}
