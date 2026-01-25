using System;
using NewLife.Log;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest.Remoting;

/// <summary>ApiServer单元测试</summary>
public class ApiServerTests
{
    [Fact(DisplayName = "动态端口测试")]
    public void DynamicPortTest()
    {
        // Port=0 让系统自动分配可用端口
        using var server = new ApiServer(0)
        {
            Log = XTrace.Log,
        };

        // 启动前端口为0
        Assert.Equal(0, server.Port);

        server.Start();

        // 启动后端口应大于0
        Assert.True(server.Port > 0, $"启动后端口应大于0，实际值：{server.Port}");
        Assert.True(server.Active);

        XTrace.WriteLine($"系统分配的端口：{server.Port}");

        server.Stop("测试完成");
        Assert.False(server.Active);
    }

    [Fact(DisplayName = "指定端口测试")]
    public void SpecifiedPortTest()
    {
        var port = 23456;
        using var server = new ApiServer(port)
        {
            Log = XTrace.Log,
        };

        Assert.Equal(port, server.Port);

        server.Start();

        // 指定端口时，端口号应保持不变
        Assert.Equal(port, server.Port);
        Assert.True(server.Active);

        server.Stop("测试完成");
    }

    [Fact(DisplayName = "多次动态端口测试")]
    public void MultipleDynamicPortTest()
    {
        // 创建两个动态端口服务器，验证端口不冲突
        using var server1 = new ApiServer(0) { Log = XTrace.Log };
        using var server2 = new ApiServer(0) { Log = XTrace.Log };

        server1.Start();
        server2.Start();

        Assert.True(server1.Port > 0);
        Assert.True(server2.Port > 0);
        Assert.NotEqual(server1.Port, server2.Port);

        XTrace.WriteLine($"服务器1端口：{server1.Port}，服务器2端口：{server2.Port}");

        server1.Stop("测试完成");
        server2.Stop("测试完成");
    }
}
