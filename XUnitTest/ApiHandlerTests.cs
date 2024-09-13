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
}
