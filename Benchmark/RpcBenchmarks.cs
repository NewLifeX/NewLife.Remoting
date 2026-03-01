using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using NewLife.Data;
using NewLife.Remoting;

#pragma warning disable CS0618 // Packet obsolete

namespace NewLife.Remoting.Benchmarks;

/// <summary>RPC基准测试。对ApiClient+ApiServer进行全面性能测试，覆盖多种数据场景和并发级别</summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class RpcBenchmarks
{
    private ApiServer _server = null!;
    private ApiClient[] _clients = null!;

    private String _smallString = null!;
    private String _largeString = null!;
    private Byte[] _smallPacketData = null!;
    private Byte[] _largePacketData = null!;

    /// <summary>并发线程数。动态包含当前CPU核心数</summary>
    public static IEnumerable<Int32> ThreadCounts
    {
        get
        {
            var cores = Environment.ProcessorCount;
            var set = new SortedSet<Int32> { 1, 4, 16, 32 };
            set.Add(cores);
            return set;
        }
    }

    /// <summary>并发数</summary>
    [ParamsSource(nameof(ThreadCounts))]
    public Int32 Concurrency { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // 创建服务端，使用随机端口，禁用日志避免IO干扰
        _server = new ApiServer(0);
        _server.Register<BenchController>();
        _server.Start();

        var port = _server.Port;

        // 为每个并发线程创建独立的客户端连接
        _clients = new ApiClient[Concurrency];
        for (var i = 0; i < Concurrency; i++)
        {
            _clients[i] = new ApiClient($"tcp://127.0.0.1:{port}");
            // 预热连接，确保TCP握手完成
            _clients[i].Invoke<String[]>("Api/All");
        }

        // 准备测试数据
        _smallString = new String('A', 16);
        _largeString = new String('B', 2000);
        _smallPacketData = new Byte[16];
        _largePacketData = new Byte[2000];
        Random.Shared.NextBytes(_smallPacketData);
        Random.Shared.NextBytes(_largePacketData);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (_clients != null)
        {
            foreach (var client in _clients)
                client?.TryDispose();
        }
        _server?.TryDispose();
    }

    #region 基础调用
    /// <summary>无参返回Int32</summary>
    [Benchmark(Description = "无参返回Int32")]
    public async Task NoArg_ReturnInt32()
    {
        var tasks = new Task<Int32>[Concurrency];
        for (var i = 0; i < Concurrency; i++)
            tasks[i] = _clients[i].InvokeAsync<Int32>("Bench/NoArg");
        await Task.WhenAll(tasks);
    }

    /// <summary>String出入参(16字符)</summary>
    [Benchmark(Description = "String出入参(16字符)")]
    public async Task EchoString_Small()
    {
        var tasks = new Task<String>[Concurrency];
        for (var i = 0; i < Concurrency; i++)
            tasks[i] = _clients[i].InvokeAsync<String>("Bench/EchoString", new { input = _smallString });
        await Task.WhenAll(tasks);
    }

    /// <summary>String出入参(2000字符)</summary>
    [Benchmark(Description = "String出入参(2000字符)")]
    public async Task EchoString_Large()
    {
        var tasks = new Task<String>[Concurrency];
        for (var i = 0; i < Concurrency; i++)
            tasks[i] = _clients[i].InvokeAsync<String>("Bench/EchoString", new { input = _largeString });
        await Task.WhenAll(tasks);
    }

    #endregion

    #region 复杂参数
    /// <summary>多基础类型参数</summary>
    [Benchmark(Description = "多基础类型参数")]
    public async Task MultiArgs()
    {
        var tasks = new Task<Int32>[Concurrency];
        for (var i = 0; i < Concurrency; i++)
            tasks[i] = _clients[i].InvokeAsync<Int32>("Bench/MultiArgs", new
            {
                a = 42,
                b = (Int64)123456,
                c = "test",
                d = true,
                e = 3.14
            });
        await Task.WhenAll(tasks);
    }

    #endregion

    #region 二进制数据
    /// <summary>IPacket出入参(16字节)</summary>
    [Benchmark(Description = "IPacket出入参(16字节)")]
    public async Task EchoPacket_Small()
    {
        var tasks = new Task<Packet>[Concurrency];
        for (var i = 0; i < Concurrency; i++)
            tasks[i] = _clients[i].InvokeAsync<Packet>("Bench/EchoPacket", _smallPacketData);
        await Task.WhenAll(tasks);
    }

    /// <summary>IPacket出入参(2000字节)</summary>
    [Benchmark(Description = "IPacket出入参(2000字节)")]
    public async Task EchoPacket_Large()
    {
        var tasks = new Task<Packet>[Concurrency];
        for (var i = 0; i < Concurrency; i++)
            tasks[i] = _clients[i].InvokeAsync<Packet>("Bench/EchoPacket", _largePacketData);
        await Task.WhenAll(tasks);
    }
    #endregion
}
