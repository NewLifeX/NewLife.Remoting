using System;
using System.ComponentModel;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest;

/// <summary>ApiReceivedEventArgs单元测试</summary>
public class ApiReceivedEventArgsTests
{
    [Fact]
    [DisplayName("默认属性")]
    public void DefaultProperties()
    {
        var args = new ApiReceivedEventArgs();

        Assert.Null(args.Session);
        Assert.Null(args.Remote);
        Assert.Null(args.ApiMessage);
        Assert.Null(args.UserState);
    }

    [Fact]
    [DisplayName("属性读写")]
    public void Properties_SetAndGet()
    {
        var apiMsg = new ApiMessage { Action = "Test", Code = 200 };
        var args = new ApiReceivedEventArgs
        {
            ApiMessage = apiMsg,
            UserState = "customState"
        };

        Assert.Equal("Test", args.ApiMessage.Action);
        Assert.Equal(200, args.ApiMessage.Code);
        Assert.Equal("customState", args.UserState);
    }
}
