using IoTEdge;
using NewLife.Log;

namespace IoTZero.Clients;

/// <summary>客户端测试入口。主程序通过反射调用</summary>
public static class ClientTest
{
    private static ITracer _tracer;
    private static HttpDevice _device;

    public static async Task Process(IServiceProvider serviceProvider)
    {
        await Task.Delay(3_000);

        XTrace.WriteLine("开始IoT客户端测试");

        _tracer = serviceProvider.GetService<ITracer>();

        var set = ClientSetting.Current;

        // 产品编码、产品密钥从IoT管理平台获取，设备编码支持自动注册
        var client = new HttpDevice(set)
        {
            Tracer = _tracer,
            Log = XTrace.Log,
        };

        _device = client;

        // 打开连接并自动登录。首次登录失败（如服务端冷启动响应慢）时，内部定时器自动重试
        client.Open();

        // 进程退出时注销
        NewLife.Model.Host.RegisterExit(() => client.TryDispose());

        var life = serviceProvider.GetService<IHostApplicationLifetime>();
        life.ApplicationStopping.Register(() => client.TryDispose());
    }
}
