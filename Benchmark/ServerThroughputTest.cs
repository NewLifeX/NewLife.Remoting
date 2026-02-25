using System.Diagnostics;
using NewLife.Data;
using NewLife.Log;
using NewLife.Messaging;
using NewLife.Remoting;

#pragma warning disable CS0618 // Packet obsolete

namespace NewLife.Remoting.Benchmarks;

/// <summary>服务端吞吐量压力测试。模拟多客户端并发对ApiServer施加压力，测量服务端处理能力</summary>
public class ServerThroughputTest
{
    /// <summary>运行服务端吞吐量测试（通过TCP网络）</summary>
    /// <param name="clientCount">客户端连接数</param>
    /// <param name="durationSeconds">测试持续时间（秒）</param>
    /// <param name="warmupSeconds">预热时间（秒）</param>
    public static void RunNetworkTest(Int32 clientCount = 100, Int32 durationSeconds = 10, Int32 warmupSeconds = 3)
    {
        Console.WriteLine("========================================");
        Console.WriteLine("  网络吞吐量测试（TCP端到端）");
        Console.WriteLine("========================================");
        Console.WriteLine();
        Console.WriteLine($"客户端连接数：{clientCount}");
        Console.WriteLine($"测试持续时间：{durationSeconds} 秒");
        Console.WriteLine($"预热时间：{warmupSeconds} 秒");
        Console.WriteLine();

        // 创建服务端
        var server = new ApiServer(0)
        {
            Log = Logger.Null,
            EncoderLog = Logger.Null,
            StatPeriod = 0,
        };
        server.Register<BenchController>();
        server.Start();

        var port = server.Port;
        Console.WriteLine($"服务端启动完成，端口：{port}");

        // 创建客户端连接
        Console.Write($"正在创建 {clientCount} 个客户端连接...");
        var clients = new ApiClient[clientCount];
        for (var i = 0; i < clientCount; i++)
        {
            clients[i] = new ApiClient($"tcp://127.0.0.1:{port}") { Log = Logger.Null };
            clients[i].Invoke<String[]>("Api/All");
        }
        Console.WriteLine(" 完成");
        Console.WriteLine();

        RunScenario("NoArg_ReturnInt32", clients, clientCount, durationSeconds, warmupSeconds,
            (client) => client.InvokeAsync<Int32>("Bench/NoArg"));

        RunScenario("EchoPacket_16B", clients, clientCount, durationSeconds, warmupSeconds,
            (client) => client.InvokeAsync<Packet>("Bench/EchoPacket", new Byte[16]));

        foreach (var client in clients) client?.TryDispose();
        server.TryDispose();
    }

    /// <summary>运行服务端纯处理能力测试（绕过TCP网络栈）</summary>
    /// <param name="threadCount">并发线程数</param>
    /// <param name="durationSeconds">测试持续时间（秒）</param>
    /// <param name="warmupSeconds">预热时间（秒）</param>
    public static void RunDirectTest(Int32 threadCount = 0, Int32 durationSeconds = 10, Int32 warmupSeconds = 3)
    {
        if (threadCount <= 0) threadCount = Environment.ProcessorCount;

        Console.WriteLine("========================================");
        Console.WriteLine("  服务端纯处理能力测试（绕过TCP）");
        Console.WriteLine("========================================");
        Console.WriteLine();
        Console.WriteLine($"并发线程数：{threadCount}");
        Console.WriteLine($"CPU逻辑核心数：{Environment.ProcessorCount}");
        Console.WriteLine($"测试持续时间：{durationSeconds} 秒");
        Console.WriteLine($"预热时间：{warmupSeconds} 秒");
        Console.WriteLine();

        // 创建服务端（不需要监听端口）
        var server = new ApiServer(0)
        {
            Log = Logger.Null,
            EncoderLog = Logger.Null,
            StatPeriod = 0,
        };
        server.Register<BenchController>();
        server.Start();

        var encoder = server.Encoder;

        // 预创建请求消息模板（NoArg场景）
        Console.Write("预创建请求消息模板...");
        var noArgTemplate = CreateRequestPayload(encoder, "Bench/NoArg", null);
        var echoPacketTemplate = CreateRequestPayload(encoder, "Bench/EchoPacket", new Byte[16]);
        Console.WriteLine(" 完成");
        Console.WriteLine();

        // 创建模拟会话
        var sessions = new MockApiSession[threadCount];
        for (var i = 0; i < threadCount; i++)
            sessions[i] = new MockApiSession(server);

        // 测试NoArg场景
        RunDirectScenario("NoArg_ReturnInt32（纯处理）", server, sessions, noArgTemplate, threadCount, durationSeconds, warmupSeconds);

        // 测试EchoPacket场景
        RunDirectScenario("EchoPacket_16B（纯处理）", server, sessions, echoPacketTemplate, threadCount, durationSeconds, warmupSeconds);

        server.TryDispose();

        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine("  所有测试完成");
        Console.WriteLine("========================================");
    }

    /// <summary>创建请求消息的Payload模板</summary>
    private static Byte[] CreateRequestPayload(IEncoder encoder, String action, Object? args)
    {
        using var msg = encoder.CreateRequest(action, args);
        return msg.Payload!.ToArray();
    }

    /// <summary>运行直接处理场景测试</summary>
    private static void RunDirectScenario(String name, ApiServer server, MockApiSession[] sessions, Byte[] requestTemplate, Int32 threadCount, Int32 durationSeconds, Int32 warmupSeconds)
    {
        Console.WriteLine($"--- 场景：{name} ---");

        var totalRequests = 0L;
        var errors = 0L;
        var running = true;

        // 预热
        Console.Write($"  预热 {warmupSeconds} 秒...");
        var warmupCts = new CancellationTokenSource();
        var warmupTasks = new Thread[threadCount];
        for (var i = 0; i < threadCount; i++)
        {
            var session = sessions[i];
            warmupTasks[i] = new Thread(() =>
            {
                while (!warmupCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var msg = CreateMessage(requestTemplate);
                        using var rs = server.Process(session, msg, session);
                        rs?.Payload?.TryDispose();
                    }
                    catch { }
                }
            });
            warmupTasks[i].IsBackground = true;
            warmupTasks[i].Start();
        }
        Thread.Sleep(warmupSeconds * 1000);
        warmupCts.Cancel();
        foreach (var t in warmupTasks) t.Join(3000);
        Console.WriteLine(" 完成");

        // GC 基线
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var gen0Before = GC.CollectionCount(0);
        var gen1Before = GC.CollectionCount(1);
        var gen2Before = GC.CollectionCount(2);
        var memBefore = GC.GetTotalMemory(false);

        // 正式测试
        var sw = Stopwatch.StartNew();
        var threads = new Thread[threadCount];
        for (var i = 0; i < threadCount; i++)
        {
            var session = sessions[i];
            threads[i] = new Thread(() =>
            {
                var localCount = 0L;
                var localErrors = 0L;
                while (running)
                {
                    try
                    {
                        var msg = CreateMessage(requestTemplate);
                        using var rs = server.Process(session, msg, session);
                        rs?.Payload?.TryDispose();
                        localCount++;
                    }
                    catch
                    {
                        localErrors++;
                    }
                }
                Interlocked.Add(ref totalRequests, localCount);
                Interlocked.Add(ref errors, localErrors);
            });
            threads[i].IsBackground = true;
            threads[i].Start();
        }

        Thread.Sleep(durationSeconds * 1000);
        running = false;
        foreach (var t in threads) t.Join(3000);
        sw.Stop();

        var gen0After = GC.CollectionCount(0);
        var gen1After = GC.CollectionCount(1);
        var gen2After = GC.CollectionCount(2);
        var memAfter = GC.GetTotalMemory(false);

        var elapsed = sw.Elapsed.TotalSeconds;
        var rps = totalRequests / elapsed;

        Console.WriteLine($"  总请求数：{totalRequests:N0}");
        Console.WriteLine($"  错误数：{errors:N0}");
        Console.WriteLine($"  耗时：{elapsed:F2} 秒");
        Console.WriteLine($"  吞吐量：{rps:N0} RPC/s");
        Console.WriteLine($"  每请求分配：{(memAfter - memBefore) * 1.0 / totalRequests:F0} B/req（估算）");
        Console.WriteLine($"  GC: Gen0={gen0After - gen0Before}, Gen1={gen1After - gen1Before}, Gen2={gen2After - gen2Before}");
        Console.WriteLine();
    }

    /// <summary>从模板创建IMessage</summary>
    private static DefaultMessage CreateMessage(Byte[] template)
    {
        var payload = new ArrayPacket(template);
        return new DefaultMessage { Payload = payload };
    }

    private static void RunScenario(String name, ApiClient[] clients, Int32 clientCount, Int32 durationSeconds, Int32 warmupSeconds, Func<ApiClient, Task> action)
    {
        Console.WriteLine($"--- 场景：{name} ---");

        var totalRequests = 0L;
        var errors = 0L;
        var running = true;

        // 预热
        Console.Write($"  预热 {warmupSeconds} 秒...");
        var warmupCts = new CancellationTokenSource();
        var warmupTasks = new Task[clientCount];
        for (var i = 0; i < clientCount; i++)
        {
            var client = clients[i];
            warmupTasks[i] = Task.Run(async () =>
            {
                while (!warmupCts.Token.IsCancellationRequested)
                {
                    try { await action(client); } catch { }
                }
            });
        }
        Thread.Sleep(warmupSeconds * 1000);
        warmupCts.Cancel();
        try { Task.WaitAll(warmupTasks, 5000); } catch { }
        Console.WriteLine(" 完成");

        // GC 基线
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var gen0Before = GC.CollectionCount(0);
        var gen1Before = GC.CollectionCount(1);
        var gen2Before = GC.CollectionCount(2);
        var memBefore = GC.GetTotalMemory(false);

        var sw = Stopwatch.StartNew();
        var tasks = new Task[clientCount];
        for (var i = 0; i < clientCount; i++)
        {
            var client = clients[i];
            tasks[i] = Task.Run(async () =>
            {
                while (running)
                {
                    try
                    {
                        await action(client);
                        Interlocked.Increment(ref totalRequests);
                    }
                    catch
                    {
                        Interlocked.Increment(ref errors);
                    }
                }
            });
        }

        Thread.Sleep(durationSeconds * 1000);
        running = false;
        try { Task.WaitAll(tasks, 5000); } catch { }
        sw.Stop();

        var gen0After = GC.CollectionCount(0);
        var gen1After = GC.CollectionCount(1);
        var gen2After = GC.CollectionCount(2);
        var memAfter = GC.GetTotalMemory(false);

        var elapsed = sw.Elapsed.TotalSeconds;
        var rps = totalRequests / elapsed;
        var avgLatencyUs = elapsed * 1_000_000 * clientCount / totalRequests;

        Console.WriteLine($"  总请求数：{totalRequests:N0}");
        Console.WriteLine($"  错误数：{errors:N0}");
        Console.WriteLine($"  耗时：{elapsed:F2} 秒");
        Console.WriteLine($"  吞吐量：{rps:N0} RPC/s");
        Console.WriteLine($"  平均延迟：{avgLatencyUs:F1} μs/请求");
        Console.WriteLine($"  GC: Gen0={gen0After - gen0Before}, Gen1={gen1After - gen1Before}, Gen2={gen2After - gen2Before}");
        Console.WriteLine();
    }
}

/// <summary>模拟Api会话，用于绕过TCP直接测试服务端处理能力</summary>
class MockApiSession : IApiSession, IServiceProvider
{
    private readonly ApiServer _host;
    private IDictionary<String, Object?>? _items;

    public MockApiSession(ApiServer host) => _host = host;

    public IApiHost Host => _host;
    public DateTime LastActive => DateTime.Now;
    public IApiSession[] AllSessions => [this];
    public String? Token { get; set; }
    public IDictionary<String, Object?> Items => _items ??= new Dictionary<String, Object?>();

    public Object? this[String key]
    {
        get => _items != null && _items.TryGetValue(key, out var v) ? v : null;
        set => Items[key] = value;
    }

    public Int32 InvokeOneWay(String action, Object? args = null, Byte flag = 0) => 0;

    public Object? GetService(Type serviceType) => (_host as IServiceProvider).GetService(serviceType);
}
