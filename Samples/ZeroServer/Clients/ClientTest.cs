using NewLife.Log;

namespace ZeroClient;

/// <summary>客户端测试入口。主程序通过反射调用</summary>
public static class ClientTest
{
    private static ITracer _tracer;
    private static NodeClient _device;

    public static async Task Main(IServiceProvider serviceProvider)
    {
        await Task.Delay(3_000);

        XTrace.WriteLine("开始Node客户端测试");

        // 降低日志等级，输出通信详情。生产环境不建议这么做
        XTrace.Log.Level = NewLife.Log.LogLevel.Debug;

        _tracer = serviceProvider.GetService<ITracer>();

        var set = ClientSetting.Current;

        // 产品编码、产品密钥从IoT管理平台获取，设备编码支持自动注册
        var device = new NodeClient(set)
        {
            Tracer = _tracer,
            Log = XTrace.Log,
        };

        await device.Login();

        _device = device;
    }
}
