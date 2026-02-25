using BenchmarkDotNet.Running;
using NewLife.Remoting.Benchmarks;

// 支持服务端吞吐量压力测试模式
if (args.Length > 0 && args[0].Equals("throughput", StringComparison.OrdinalIgnoreCase))
{
    var clientCount = args.Length > 1 ? Int32.Parse(args[1]) : 100;
    var duration = args.Length > 2 ? Int32.Parse(args[2]) : 10;
    ServerThroughputTest.RunNetworkTest(clientCount, duration);
    return;
}

// 服务端纯处理能力测试（绕过TCP网络栈）
if (args.Length > 0 && args[0].Equals("direct", StringComparison.OrdinalIgnoreCase))
{
    var threadCount = args.Length > 1 ? Int32.Parse(args[1]) : 0;
    var duration = args.Length > 2 ? Int32.Parse(args[2]) : 10;
    ServerThroughputTest.RunDirectTest(threadCount, duration);
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
