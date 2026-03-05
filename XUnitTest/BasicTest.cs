using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using NewLife;
using NewLife.Caching;
using NewLife.Log;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest;

/// <summary>基础功能测试</summary>
public class BasicTest
{
    [Fact]
    [DisplayName("ApiCode常量")]
    public void ApiCode_Constants()
    {
        Assert.Equal(0, ApiCode.Ok);
        Assert.Equal(401, ApiCode.Unauthorized);
        Assert.Equal(403, ApiCode.Forbidden);
        Assert.Equal(404, ApiCode.NotFound);
        Assert.Equal(500, ApiCode.InternalServerError);
    }

    [Fact]
    [DisplayName("ApiException构造")]
    public void ApiException_Constructor()
    {
        var ex = new ApiException(500, "Internal Error");

        Assert.Equal(500, ex.Code);
        Assert.Equal("Internal Error", ex.Message);
    }

    [Fact]
    [DisplayName("ApiException不同Code")]
    public void ApiException_DifferentCodes()
    {
        var ex1 = new ApiException(401, "Unauthorized");
        Assert.Equal(401, ex1.Code);
        Assert.Equal("Unauthorized", ex1.Message);

        var ex2 = new ApiException(404, "Not Found");
        Assert.Equal(404, ex2.Code);
        Assert.Equal("Not Found", ex2.Message);

        var ex3 = new ApiException(500, "Server Error");
        Assert.Equal(500, ex3.Code);
    }

    [Fact]
    [DisplayName("ApiException是Exception")]
    public void ApiException_IsException()
    {
        var ex = new ApiException(500, "Error");
        Assert.IsAssignableFrom<Exception>(ex);
    }
}