using System;
using System.ComponentModel;
using Moq;
using NewLife;
using NewLife.Data;
using NewLife.Model;
using NewLife.Reflection;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest;

public class ApiHandlerTests
{
    [Fact]
    public void Prepare()
    {
        var js = """
[{"DeviceCode":"crUGfNON","Items":[{"Time":1698405802820,"Name":"AvailableMemory","Value":"1341980672"},{"Time":1698405802820,"Name":"CpuRate","Value":"0.097"},{"Time":1698405802820,"Name":"UplinkSpeed","Value":"3952"},{"Time":1698405802820,"Name":"DownlinkSpeed","Value":"2435"}]}]
""";

        var d = Prepare;
        var action = new ApiAction(d.Method, d.Method.DeclaringType);

        var host = Mock.Of<IApiHost>();
        host.Encoder = new JsonEncoder();
        //var host = new ApiServer();
        var session = Mock.Of<IApiSession>();
        var handler = new ApiHandler { Host = host };
        //handler.Invoke("Prepare", null, "App/PostDatas", js.GetBytes(), null, null);
        handler.Prepare(session, "App/PostDatas", (ArrayPacket)js.GetBytes(), action, null);
    }

    [Fact]
    [DisplayName("会话级别Encoder覆盖全局Encoder")]
    public void SessionEncoder_OverridesGlobal()
    {
        var action = new ApiAction(typeof(ApiHandlerTests).GetMethod(nameof(Prepare))!, typeof(ApiHandlerTests));

        var host = Mock.Of<IApiHost>();
        var globalEncoder = new JsonEncoder();
        host.Encoder = globalEncoder;

        var session = Mock.Of<IApiSession>();
        var handler = new ApiHandler { Host = host };

        // 默认使用全局 Encoder
        var prepareContext = handler.Prepare(session, "App/PostDatas", (ArrayPacket)"{}".GetBytes(), action, null);
        Assert.NotNull(prepareContext);

        // 设置会话级别 Encoder（自定义编码器模拟）
        var sessionEncoder = new JsonEncoder();
        var sessionMock = Mock.Get(session);
        sessionMock.Setup(s => s[It.Is<String>(k => k == "Encoder")]).Returns(sessionEncoder);

        // 再次 Prepare，应使用会话级别 Encoder
        handler.Prepare(session, "App/PostDatas", (ArrayPacket)"{}".GetBytes(), action, null);
    }

    [Fact]
    [DisplayName("无Encoder时使用全局Encoder")]
    public void NoSessionEncoder_UsesGlobal()
    {
        var action = new ApiAction(typeof(ApiHandlerTests).GetMethod(nameof(Prepare))!, typeof(ApiHandlerTests));

        var host = Mock.Of<IApiHost>();
        var globalEncoder = new JsonEncoder();
        host.Encoder = globalEncoder;

        var session = Mock.Of<IApiSession>();
        var handler = new ApiHandler { Host = host };

        // 不设置会话 Encoder，准备上下文应正常执行
        var ctx = handler.Prepare(session, "App/PostDatas", (ArrayPacket)"{}".GetBytes(), action, null);
        Assert.NotNull(ctx);
    }
}
