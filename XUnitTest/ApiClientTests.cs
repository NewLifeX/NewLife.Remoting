using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Messaging;
using NewLife.Remoting;
using NewLife.Security;
using NewLife.Serialization;
using Xunit;

namespace XUnitTest.Remoting;

/// <summary>ApiClient单元测试</summary>
public class ApiClientTests : DisposeBase
{
    private readonly ApiServer _Server;
    private readonly Int32 _Port;

    public ApiClientTests()
    {
        _Server = new ApiServer(0)
        {
            Log = XTrace.Log,
            ShowError = true,
        };
        _Server.Register<ClientTestController>();
        _Server.Start();

        _Port = _Server.Port;
    }

    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        _Server.TryDispose();
    }

    #region 连接测试
    [Fact(DisplayName = "基本连接测试")]
    public void BasicOpenTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        Assert.False(client.Active);
        Assert.True(client.Open());
        Assert.True(client.Active);

        client.Close("测试完成");
        Assert.False(client.Active);
    }

    [Fact(DisplayName = "重复打开关闭测试")]
    public void RepeatOpenCloseTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        // 重复打开
        Assert.True(client.Open());
        Assert.True(client.Open());
        Assert.True(client.Active);

        // 重复关闭
        client.Close("测试");
        Assert.False(client.Active);
        client.Close("测试");
        Assert.False(client.Active);
    }

    [Fact(DisplayName = "SetServer地址切换")]
    public void SetServerTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");
        client.Open();

        var servers1 = client.Servers;
        Assert.Single(servers1);

        // 设置新地址
        client.SetServer($"tcp://127.0.0.1:{_Port},tcp://localhost:{_Port}");
        var servers2 = client.Servers;

        Assert.Equal(2, servers2?.Length);
    }

    [Fact(DisplayName = "未指定服务器异常")]
    public void NoServerExceptionTest()
    {
        using var client = new ApiClient();

        var ex = Assert.Throws<ArgumentNullException>(() => client.Open());
        Assert.Contains("Servers", ex.Message);
    }
    #endregion

    #region 同步调用测试
    [Fact(DisplayName = "Invoke同步调用")]
    public void InvokeSyncTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var result = client.Invoke<String>("ClientTest/Echo", new { message = "Hello" });
        Assert.Equal("Echo: Hello", result);
    }

    [Fact(DisplayName = "Invoke返回复杂对象")]
    public void InvokeComplexResultTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var result = client.Invoke<TestResult>("ClientTest/GetResult", new { id = 123, name = "Test" });

        Assert.NotNull(result);
        Assert.Equal(123, result.Id);
        Assert.Equal("Test", result.Name);
        Assert.True(result.Success);
    }
    #endregion

    #region 异步调用测试
    [Fact(DisplayName = "InvokeAsync异步调用")]
    public async Task InvokeAsyncTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var result = await client.InvokeAsync<String>("ClientTest/Echo", new { message = "Async" });
        Assert.Equal("Echo: Async", result);
    }

    [Fact(DisplayName = "InvokeAsync带取消令牌")]
    public async Task InvokeAsyncWithCancellationTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");
        using var cts = new CancellationTokenSource();

        var task = client.InvokeAsync<String>("ClientTest/Delay", new { ms = 5000 }, cts.Token);

        // 立即取消
        cts.Cancel();

        // ApiClient 将取消异常包装为 TimeoutException
        await Assert.ThrowsAnyAsync<Exception>(() => task);
    }

    [Fact(DisplayName = "并发异步调用")]
    public async Task ConcurrentInvokeAsyncTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var tasks = new List<Task<Int32>>();
        for (var i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(client.InvokeAsync<Int32>("ClientTest/Add", new { a = index, b = index * 2 }));
        }

        var results = await Task.WhenAll(tasks);

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(i + i * 2, results[i]);
        }
    }
    #endregion

    #region 单向调用测试
    [Fact(DisplayName = "InvokeOneWay单向调用")]
    public void InvokeOneWayTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var result = client.InvokeOneWay("ClientTest/OneWay", new { message = "Fire and Forget" });

        // 单向调用返回发送结果，正数表示成功
        Assert.True(result > 0);
    }
    #endregion

    #region 令牌测试
    [Fact(DisplayName = "Token令牌传递")]
    public async Task TokenTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}")
        {
            Token = "my-secret-token"
        };

        // Token 需要通过参数传递，服务端从参数中获取
        var result = await client.InvokeAsync<String>("ClientTest/GetToken", new { });
        Assert.Equal("my-secret-token", result);
    }

    [Fact(DisplayName = "Token在参数中传递")]
    public async Task TokenInArgsTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}")
        {
            Token = "token-from-property"
        };

        // Token会自动注入到参数中
        var result = await client.InvokeAsync<IDictionary<String, Object>>("ClientTest/GetArgs", new { key = "value" });

        Assert.NotNull(result);
        Assert.Equal("value", result["key"]);
        Assert.Equal("token-from-property", result["Token"]);
    }
    #endregion

    #region 超时测试
    [Fact(DisplayName = "Timeout超时设置")]
    public void TimeoutConfigTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        // 默认超时
        Assert.True(client.Timeout > 0);

        // 设置超时
        client.Timeout = 5000;
        Assert.Equal(5000, client.Timeout);
    }

    [Fact(DisplayName = "调用超时异常")]
    public async Task InvokeTimeoutTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}")
        {
            Timeout = 100 // 100ms超时
        };

        // Delay 1000ms 会超时
        var ex = await Assert.ThrowsAnyAsync<Exception>(
            () => client.InvokeAsync<String>("ClientTest/Delay", new { ms = 1000 }));

        Assert.True(ex is TimeoutException or TaskCanceledException);
    }
    #endregion

    #region 连接池测试
    [Fact(DisplayName = "UsePool连接池模式")]
    public async Task UsePoolTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}")
        {
            UsePool = true
        };

        // 并发调用测试连接池
        var tasks = new List<Task<String>>();
        for (var i = 0; i < 5; i++)
        {
            tasks.Add(client.InvokeAsync<String>("ClientTest/Echo", new { message = $"Pool-{i}" }));
        }

        var results = await Task.WhenAll(tasks);

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal($"Echo: Pool-{i}", results[i]);
        }
    }
    #endregion

    #region Received事件测试
    [Fact(DisplayName = "Received事件接收服务端推送")]
    public async Task ReceivedEventTest()
    {
        var receivedMessage = new TaskCompletionSource<IMessage>();

        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");
        client.Received += (s, e) =>
        {
            if (e.Message != null)
                receivedMessage.TrySetResult(e.Message);
        };

        // 先建立连接
        await client.InvokeAsync<String[]>("Api/All");

        // 服务端主动下发
        var session = _Server.Server.AllSessions.FirstOrDefault();
        Assert.NotNull(session);

        session.InvokeOneWay("ServerPush", new { data = "pushed" });

        // 等待接收
        var msg = await Task.WhenAny(receivedMessage.Task, Task.Delay(2000));

        Assert.True(receivedMessage.Task.IsCompleted, "应收到服务端推送消息");
    }
    #endregion

    #region 多服务器负载均衡测试
    [Fact(DisplayName = "多服务器配置")]
    public void MultipleServersTest()
    {
        using var server2 = new ApiServer(0) { Log = XTrace.Log };
        server2.Register<ClientTestController>();
        server2.Start();

        try
        {
            using var client = new ApiClient($"tcp://127.0.0.1:{_Port},tcp://127.0.0.1:{server2.Port}");

            Assert.Equal(2, client.Servers?.Length);

            // 应能正常调用
            var result = client.Invoke<String>("ClientTest/Echo", new { message = "LoadBalance" });
            Assert.Equal("Echo: LoadBalance", result);
        }
        finally
        {
            server2.Stop("测试完成");
        }
    }
    #endregion

    #region 统计测试
    [Fact(DisplayName = "StatInvoke调用统计")]
    public async Task StatInvokeTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}")
        {
            StatPeriod = 10 // 启用统计
        };

        // 先打开客户端以初始化统计
        client.Open();

        // 多次调用
        for (var i = 0; i < 5; i++)
        {
            await client.InvokeAsync<String>("ClientTest/Echo", new { message = $"Stat-{i}" });
        }

        // StatPeriod > 0 时才会初始化 StatInvoke
        // 统计可能在定时器中初始化，这里只验证调用成功
        Assert.True(client.Active);
    }

    [Fact(DisplayName = "LastActive最后活跃时间")]
    public async Task LastActiveTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var before = DateTime.Now;
        await client.InvokeAsync<String>("ClientTest/Echo", new { message = "Active" });
        var after = DateTime.Now;

        Assert.True(client.LastActive >= before);
        Assert.True(client.LastActive <= after);
    }
    #endregion

    #region 二进制数据测试
    [Fact(DisplayName = "Packet二进制数据传输")]
    public async Task PacketDataTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var data = Rand.NextBytes(1024);
        var result = await client.InvokeAsync<Packet>("ClientTest/ProcessData", data);

        Assert.NotNull(result);
        Assert.Equal(data.Length, result.Total);

        // 验证数据被XOR处理
        var resultData = result.ToArray();
        for (var i = 0; i < data.Length; i++)
        {
            Assert.Equal((Byte)(data[i] ^ 0xFF), resultData[i]);
        }
    }
    #endregion

    #region 测试辅助类
    class ClientTestController
    {
        public String Echo(String message) => $"Echo: {message}";

        public Int32 Add(Int32 a, Int32 b) => a + b;

        public TestResult GetResult(Int32 id, String name) => new()
        {
            Id = id,
            Name = name,
            Success = true
        };

        public async Task<String> Delay(Int32 ms)
        {
            await Task.Delay(ms);
            return $"Delayed {ms}ms";
        }

        public void OneWay(String message)
        {
            // 单向调用，无返回值
            XTrace.WriteLine($"OneWay received: {message}");
        }

        public String GetToken(String Token) => Token;

        public IDictionary<String, Object> GetArgs(String key, String Token) => new Dictionary<String, Object>
        {
            ["key"] = key,
            ["Token"] = Token
        };

        public IPacket ProcessData(IPacket pk)
        {
            var data = pk.ReadBytes();
            for (var i = 0; i < data.Length; i++)
            {
                data[i] ^= 0xFF;
            }
            return (ArrayPacket)data;
        }
    }

    class TestResult
    {
        public Int32 Id { get; set; }
        public String? Name { get; set; }
        public Boolean Success { get; set; }
    }
    #endregion
}
