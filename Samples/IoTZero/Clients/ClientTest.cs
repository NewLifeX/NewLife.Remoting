using IoTEdge;
using NewLife.Log;

namespace IoTZero.Clients;

/// <summary>客户端测试入口。主程序通过反射调用</summary>
public static class ClientTest
{
    private static ITracer _tracer;
    private static HttpDevice _device;

    public static async Task Main(IServiceProvider serviceProvider)
    {
        await Task.Delay(3_000);

        XTrace.WriteLine("开始IoT客户端测试");

        _tracer = serviceProvider.GetService<ITracer>();

        var set = ClientSetting.Current;

        // 产品编码、产品密钥从IoT管理平台获取，设备编码支持自动注册
        var device = new HttpDevice(set)
        {
            Tracer = _tracer,
            Log = XTrace.Log,
        };

        await device.LoginAsync();

        _device = device;
    }
}
