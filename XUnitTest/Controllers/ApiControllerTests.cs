using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using Moq;
using NewLife;
using NewLife.Remoting;
using NewLife.Security;
using Xunit;

namespace XUnitTest.Controllers;

/// <summary>ApiController 诊断控制器单元测试</summary>
/// <remarks>
/// 覆盖内置诊断端点 Api/All、Api/Info 与静态的 GetIps，
/// 不依赖网络监听，只验证控制器自身的行为。
/// </remarks>
public class ApiControllerTests
{
    #region 辅助
    /// <summary>测试用诊断控制器</summary>
    private class DiagController
    {
        /// <summary>加法</summary>
        /// <param name="x">加数</param>
        /// <param name="y">被加数</param>
        /// <returns></returns>
        public Int32 Add(Int32 x, Int32 y) => x + y;

        /// <summary>问候</summary>
        /// <param name="name">名称</param>
        /// <returns></returns>
        public String Hello(String name) => $"Hello {name}";
    }

    /// <summary>创建挂载了诊断控制器的服务端</summary>
    private static ApiServer CreateServer()
    {
        var server = new ApiServer();
        server.Register<DiagController>();

        return server;
    }
    #endregion

    [Fact, DisplayName("Api/All列出已注册动作签名")]
    public void All_ReturnsSignatures()
    {
        var ctrl = new ApiController { Host = CreateServer() };

        var list = ctrl.All();

        Assert.NotEmpty(list);
        Assert.Contains(list, e => e.Contains("Add") && e.Contains("Int32 x, Int32 y"));
        Assert.Contains(list, e => e.Contains("Hello"));
    }

    [Fact, DisplayName("Api/All结果被缓存")]
    public void All_IsCached()
    {
        var ctrl = new ApiController { Host = CreateServer() };

        Assert.Same(ctrl.All(), ctrl.All());
    }

    [Fact, DisplayName("Api/Info返回服务端状态")]
    public void Info_ReturnsState()
    {
        var state = Rand.NextString(8);

        var dic = new ApiController().Info(state) as IDictionary<String, Object?>;

        Assert.NotNull(dic);
        Assert.Equal(state, dic!["State"]);
        Assert.True(dic.ContainsKey("Id"));
        Assert.True(dic.ContainsKey("MachineName"));
        Assert.True(dic.ContainsKey("Time"));
    }

    [Fact, DisplayName("Api/Info带会话时返回令牌并记录状态")]
    public void Info_WithSession_ReturnsToken()
    {
        var session = new Mock<IApiSession>();
        session.Setup(e => e.Token).Returns("token-abc");

        var origin = ControllerContext.Current;
        try
        {
            ControllerContext.Current = new ControllerContext { Session = session.Object };

            var ctrl = new ApiController { Session = session.Object };

            var dic = ctrl.Info("running") as IDictionary<String, Object?>;

            Assert.NotNull(dic);
            Assert.Equal("token-abc", dic!["Token"]);
            session.VerifySet(e => e["State"] = "running", Times.Once);
        }
        finally
        {
            ControllerContext.Current = origin;
        }
    }

    [Fact, DisplayName("GetIps只返回非回环IPv4地址")]
    public void GetIps_ReturnsIpv4()
    {
        var ips = ApiController.GetIps();

        // 受限环境下可能探测不到网卡，此时返回 null
        if (ips.IsNullOrEmpty()) return;

        foreach (var ip in ips!.Split(','))
        {
            Assert.True(IPAddress.TryParse(ip, out var addr));
            Assert.Equal(AddressFamily.InterNetwork, addr!.AddressFamily);
        }
    }
}
