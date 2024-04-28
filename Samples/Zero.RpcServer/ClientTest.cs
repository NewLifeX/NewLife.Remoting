using System.Net.Sockets;
using System.Text;
using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Security;
using NewLife.Serialization;

namespace Zero.RpcServer;

static class ClientTest
{
    /// <summary>Tcp连接ApiServer</summary>
    public static async void TcpTest(Int32 port)
    {
        await Task.Delay(1_000);
        XTrace.WriteLine("");
        XTrace.WriteLine("Tcp开始连接！");

        // 连接服务端
        var client = new ApiClient("tcp://127.0.0.2:12346");
        client.Open();

        await Process(client);

        // 关闭连接
        client.Close("测试完成");
    }

    /// <summary>Udp连接ApiServer</summary>
    public static async void UdpTest(Int32 port)
    {
        await Task.Delay(2_000);
        XTrace.WriteLine("");
        XTrace.WriteLine("Udp开始连接！");

        // 连接服务端
        var client = new ApiClient("udp://127.0.0.2:12346");
        client.Open();

        await Process(client);

        // 关闭连接
        client.Close("测试完成");
    }

    /// <summary>Tcp连接ApiServer</summary>
    public static async void WebSocketTest(Int32 port)
    {
        await Task.Delay(3_000);
        XTrace.WriteLine("");
        XTrace.WriteLine("WebSocket开始连接！");

        // 连接服务端
        var client = new ApiClient("ws://127.0.0.2:12346");
        client.Open();

        await Process(client);

        // 关闭连接
        client.Close("测试完成");
    }

    static async Task Process(ApiClient client)
    {
        // 获取所有接口
        var apis = await client.InvokeAsync<String[]>("api/all");
        client.WriteLog("共有接口数：{0}", apis.Length);

        // 获取服务端信息
        var state = Rand.NextString(8);
        var state2 = Rand.NextString(8);
        var infs = await client.InvokeAsync<IDictionary<String, Object>>("api/info", new { state, state2 });
        client.WriteLog("服务端信息：{0}", infs.ToJson(true));
    }
}
