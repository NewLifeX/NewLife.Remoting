using NewLife;
using NewLife.Log;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;

namespace ZeroClient;

/// <summary>客户端测试入口。主程序通过反射调用</summary>
public static class ClientTest
{
    private static ITracer _tracer;
    private static NodeClient _device;

    public static async Task Process(IServiceProvider serviceProvider)
    {
        await Task.Delay(3_000);

        XTrace.WriteLine("开始Node客户端测试");

        // 降低日志等级，输出通信详情。生产环境不建议这么做
        XTrace.Log.Level = NewLife.Log.LogLevel.Debug;

        _tracer = serviceProvider.GetService<ITracer>();

        var set = ClientSetting.Current;

        // 产品编码、产品密钥从IoT管理平台获取，设备编码支持自动注册
        var client = new NodeClient(set)
        {
            Tracer = _tracer,
            Log = XTrace.Log,
        };

        await client.Login();

        _device = client;

        // 进程退出时注销
        NewLife.Model.Host.RegisterExit(() => client.TryDispose());

        var life = serviceProvider.GetService<IHostApplicationLifetime>();
        life.ApplicationStopping.Register(() => client.TryDispose());

        // 启动自测：延迟等待SSE通道建立后，验证响应总线端到端流程
        _ = Task.Run(async () =>
        {
            await Task.Delay(10_000);
            await SelfTest(serviceProvider, client.Code, XTrace.Log);
        });
    }

    /// <summary>自测：发送命令并通过响应总线等待设备回复，验证完整调用链</summary>
    private static async Task SelfTest(IServiceProvider serviceProvider, String code, ILog log)
    {
        using var span = _tracer?.NewSpan("SelfTest:ResponseBus");

        var deviceService = serviceProvider.GetService<IDeviceService>();
        var sessionManager = serviceProvider.GetService<ISessionManager>();

        if (deviceService == null)
        {
            XTrace.WriteLine("[SelfTest] ❌ IDeviceService 未注册，跳过响应总线测试");
            return;
        }
        if (sessionManager == null)
        {
            XTrace.WriteLine("[SelfTest] ❌ ISessionManager 未注册，响应总线未启用");
            return;
        }

        XTrace.WriteLine("========== [SelfTest] 开始响应总线端到端测试 ==========");
        XTrace.WriteLine($"[SelfTest] 目标设备: {code}");
        XTrace.WriteLine($"[SelfTest] 会话管理器: SessionManager (Topic=Commands, ResponseTopic=CommandsReplies)");

        // 查找设备
        var device = deviceService.QueryDevice(code);
        if (device == null)
        {
            XTrace.WriteLine($"[SelfTest] ❌ 未找到设备 {code}");
            return;
        }
        XTrace.WriteLine($"[SelfTest] 设备名称: {device.Name}");

        // 构建测试命令
        var cmd = new CommandModel
        {
            Command = "test/ping",
            Argument = "hello-response-bus",
            Expire = DateTime.UtcNow.AddSeconds(30),
        };

        using var stepSpan = _tracer?.NewSpan("SelfTest:SendAndWait", cmd);

        try
        {
            // 通过新 API 一步完成「发送命令 + 阻塞等待响应」（内部由 SessionManager.PublishAsync 闭环）
            XTrace.WriteLine($"[SelfTest] → 发送命令并等待响应 (超时10s): {cmd.Command}({cmd.Argument})");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var reply = await deviceService.SendCommand(device, cmd, 10, CancellationToken.None);
            sw.Stop();

            if (reply != null)
            {
                XTrace.WriteLine($"[SelfTest] ← 收到响应! 耗时={sw.ElapsedMilliseconds}ms");
                XTrace.WriteLine($"[SelfTest]   Status: {reply.Status}");
                XTrace.WriteLine($"[SelfTest]   Data: {reply.Data}");
                XTrace.WriteLine($"[SelfTest]   Code: {reply.Code}");
                XTrace.WriteLine($"[SelfTest]   SenderNodeId: {reply.SenderNodeId}");
                XTrace.WriteLine("[SelfTest] ✅ 响应总线端到端测试通过！");
                stepSpan?.AppendTag($"Success: {sw.ElapsedMilliseconds}ms, {reply.Data}");
            }
            else
            {
                XTrace.WriteLine($"[SelfTest] ← 超时! 耗时={sw.ElapsedMilliseconds}ms，未收到响应");
                XTrace.WriteLine("[SelfTest] ⚠️ 可能原因：客户端SSE通道未建立，或命令处理器未注册");
                stepSpan?.AppendTag($"Timeout: {sw.ElapsedMilliseconds}ms");
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteLine($"[SelfTest] ❌ 异常: {ex.Message}");
            stepSpan?.SetError(ex, null);
        }

        XTrace.WriteLine("========== [SelfTest] 响应总线测试结束 ==========");
    }
}
