using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Caching;
using NewLife.Data;
using NewLife.Log;
using NewLife.Messaging;
using NewLife.Model;
using NewLife.Remoting;
using NewLife.Security;
using NewLife.Serialization;
using Xunit;

namespace XUnitTest.Remoting;

/// <summary>ApiServer与ApiClient集成测试</summary>
public class ApiServerClientIntegrationTests : DisposeBase
{
    private readonly ApiServer _Server;
    private readonly Int32 _Port;

    public ApiServerClientIntegrationTests()
    {
        _Server = new ApiServer(0)
        {
            Log = XTrace.Log,
            ShowError = true,
        };
        _Server.Register<IntegrationController>();
        _Server.Start();

        _Port = _Server.Port;
    }

    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        _Server.TryDispose();
    }

    #region 基础RPC测试
    [Fact(DisplayName = "完整RPC调用流程")]
    public async Task FullRpcFlowTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        // 1. 获取API列表
        var apis = await client.InvokeAsync<String[]>("Api/All");
        Assert.NotNull(apis);
        Assert.True(apis.Length > 0);

        // 2. 简单调用
        var greeting = await client.InvokeAsync<String>("Integration/Greet", new { name = "World" });
        Assert.Equal("Hello, World!", greeting);

        // 3. 复杂参数
        var result = await client.InvokeAsync<CalculateResult>("Integration/Calculate", new
        {
            a = 10,
            b = 3,
            operation = "add"
        });
        Assert.NotNull(result);
        Assert.Equal(13, result.Value);

        // 4. 异常处理
        var ex = await Assert.ThrowsAsync<ApiException>(
            () => client.InvokeAsync<Object>("Integration/Error"));
        Assert.Equal(500, ex.Code);
    }

    [Theory(DisplayName = "多种参数类型测试")]
    [InlineData("add", 10, 5, 15)]
    [InlineData("sub", 10, 5, 5)]
    [InlineData("mul", 10, 5, 50)]
    [InlineData("div", 10, 5, 2)]
    public async Task MultipleParameterTypesTest(String operation, Int32 a, Int32 b, Int32 expected)
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var result = await client.InvokeAsync<CalculateResult>("Integration/Calculate", new { a, b, operation });

        Assert.NotNull(result);
        Assert.Equal(expected, result.Value);
        Assert.Equal(operation, result.Operation);
    }
    #endregion

    #region 服务端下发测试
    [Fact(DisplayName = "服务端主动下发消息")]
    public async Task ServerPushMessageTest()
    {
        var receivedMessages = new List<String>();
        var messageReceived = new TaskCompletionSource<Boolean>();

        using var client = new ReceivableApiClient($"tcp://127.0.0.1:{_Port}");
        client.MessageReceived += (action, data) =>
        {
            receivedMessages.Add($"{action}:{data}");
            // 收到 ServerNotify 消息时完成
            if (action == "ServerNotify")
                messageReceived.TrySetResult(true);
        };

        // 建立连接
        await client.InvokeAsync<String[]>("Api/All");

        // 获取会话并下发消息
        var session = _Server.Server.AllSessions.FirstOrDefault();
        Assert.NotNull(session);

        session.InvokeOneWay("ServerNotify", new { message = "Hello from server" });

        // 等待消息到达
        var completed = await Task.WhenAny(messageReceived.Task, Task.Delay(2000));
        Assert.True(messageReceived.Task.IsCompleted, "应收到服务端推送");

        // 应包含 ServerNotify 消息
        Assert.Contains(receivedMessages, m => m.StartsWith("ServerNotify:"));
    }

    [Fact(DisplayName = "广播消息到所有客户端")]
    public async Task BroadcastToAllClientsTest()
    {
        var receivedCounts = new Int32[3];
        var allReceived = new TaskCompletionSource<Boolean>();
        var clients = new List<ReceivableApiClient>();

        try
        {
            // 创建多个客户端
            for (var i = 0; i < 3; i++)
            {
                var index = i;
                var client = new ReceivableApiClient($"tcp://127.0.0.1:{_Port}");
                client.MessageReceived += (action, data) =>
                {
                    Interlocked.Increment(ref receivedCounts[index]);
                    if (receivedCounts.All(c => c >= 1))
                        allReceived.TrySetResult(true);
                };
                await client.InvokeAsync<String[]>("Api/All");
                clients.Add(client);
            }

            Assert.Equal(3, _Server.Server.AllSessions.Length);

            // 广播消息
            var count = _Server.InvokeAll("Broadcast", new { data = "BroadcastData" });
            Assert.Equal(3, count);

            // 等待所有客户端收到
            await Task.WhenAny(allReceived.Task, Task.Delay(3000));

            for (var i = 0; i < 3; i++)
            {
                Assert.True(receivedCounts[i] >= 1, $"客户端{i}应收到广播消息");
            }
        }
        finally
        {
            foreach (var client in clients)
            {
                client.TryDispose();
            }
        }
    }
    #endregion

    #region 大数据传输测试
    [Theory(DisplayName = "大数据传输测试")]
    [InlineData(1024)]          // 1KB
    [InlineData(32 * 1024)]     // 32KB
    [InlineData(64 * 1024)]     // 64KB
    public async Task LargeDataTransferTest(Int32 size)
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var data = Rand.NextBytes(size);
        var result = await client.InvokeAsync<Packet>("Integration/Echo", data);

        Assert.NotNull(result);
        Assert.Equal(size, result.Total);
        Assert.True(data.SequenceEqual(result.ToArray()));
    }

    [Fact(DisplayName = "大消息分片处理")]
    public async Task LargeMessageChunkTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        // 发送多个中等大小的数据
        for (var i = 0; i < 5; i++)
        {
            var data = Rand.NextBytes(10 * 1024);
            var result = await client.InvokeAsync<Packet>("Integration/Echo", data);

            Assert.NotNull(result);
            Assert.Equal(data.Length, result.Total);
        }
    }
    #endregion

    #region 并发压力测试
    [Fact(DisplayName = "高并发调用测试")]
    public async Task HighConcurrencyTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var tasks = new List<Task<Int32>>();
        var count = 100;

        for (var i = 0; i < count; i++)
        {
            var index = i;
            tasks.Add(client.InvokeAsync<Int32>("Integration/Add", new { a = index, b = 1 }));
        }

        var results = await Task.WhenAll(tasks);

        for (var i = 0; i < count; i++)
        {
            Assert.Equal(i + 1, results[i]);
        }
    }

    [Fact(DisplayName = "多客户端并发测试")]
    public async Task MultiClientConcurrencyTest()
    {
        var clientCount = 5;
        var callsPerClient = 20;
        var clients = new List<ApiClient>();
        var allTasks = new List<Task<Int32>>();

        try
        {
            for (var c = 0; c < clientCount; c++)
            {
                var client = new ApiClient($"tcp://127.0.0.1:{_Port}");
                clients.Add(client);

                for (var i = 0; i < callsPerClient; i++)
                {
                    var index = c * callsPerClient + i;
                    allTasks.Add(client.InvokeAsync<Int32>("Integration/Add", new { a = index, b = 1 }));
                }
            }

            var results = await Task.WhenAll(allTasks);

            // 验证所有结果
            Assert.Equal(clientCount * callsPerClient, results.Length);
            for (var i = 0; i < results.Length; i++)
            {
                Assert.Equal(i + 1, results[i]);
            }
        }
        finally
        {
            foreach (var client in clients)
            {
                client.TryDispose();
            }
        }
    }
    #endregion

    #region 连接断开重连测试
    [Fact(DisplayName = "客户端断开后服务端会话清理")]
    public async Task ClientDisconnectSessionCleanupTest()
    {
        // 创建并连接客户端
        var client = new ApiClient($"tcp://127.0.0.1:{_Port}");
        await client.InvokeAsync<String[]>("Api/All");

        Assert.Single(_Server.Server.AllSessions);

        // 关闭客户端
        client.Close("测试断开");
        client.Dispose();

        // 等待服务端检测到断开
        await Task.Delay(500);

        // 会话应被清理
        Assert.Empty(_Server.Server.AllSessions);
    }
    #endregion

    #region 异常场景测试
    [Fact(DisplayName = "参数验证异常")]
    public async Task ParameterValidationTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        // 缺少必要参数
        var ex = await Assert.ThrowsAsync<ApiException>(
            () => client.InvokeAsync<CalculateResult>("Integration/Calculate", new { a = 10 }));

        // 应返回服务端异常
        Assert.True(ex.Code >= 400);
    }

    [Fact(DisplayName = "除零异常")]
    public async Task DivideByZeroTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => client.InvokeAsync<CalculateResult>("Integration/Calculate", new
            {
                a = 10,
                b = 0,
                operation = "div"
            }));

        Assert.Equal(500, ex.Code);
    }
    #endregion

    #region 编码器测试
    [Fact(DisplayName = "Json编码器测试")]
    public async Task JsonEncoderTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        // 复杂嵌套对象
        var request = new
        {
            user = new { id = 1, name = "Test" },
            items = new[] { "a", "b", "c" },
            count = 3
        };

        var result = await client.InvokeAsync<ComplexResult>("Integration/ProcessComplex", request);

        Assert.NotNull(result);
        Assert.Equal(1, result.UserId);
        Assert.Equal("Test", result.UserName);
        Assert.Equal(3, result.ItemCount);
    }
    #endregion

    #region 服务端处理统计测试
    [Fact(DisplayName = "服务端处理统计")]
    public async Task ServerStatProcessTest()
    {
        // 启用统计
        _Server.StatPeriod = 10;

        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        for (var i = 0; i < 10; i++)
        {
            await client.InvokeAsync<String>("Integration/Greet", new { name = $"User{i}" });
        }

        // 统计应有记录
        Assert.NotNull(_Server.StatProcess);
        Assert.True(_Server.StatProcess.Value >= 10);
    }
    #endregion

    #region 测试辅助类
    class IntegrationController
    {
        public String Greet(String name) => $"Hello, {name}!";

        public Int32 Add(Int32 a, Int32 b) => a + b;

        public CalculateResult Calculate(Int32 a, Int32 b, String operation)
        {
            var value = operation?.ToLower() switch
            {
                "add" => a + b,
                "sub" => a - b,
                "mul" => a * b,
                "div" => a / b,
                _ => throw new ArgumentException($"未知操作: {operation}")
            };

            return new CalculateResult
            {
                Value = value,
                Operation = operation ?? "",
                A = a,
                B = b
            };
        }

        public void Error() => throw new InvalidOperationException("测试异常");

        public IPacket Echo(IPacket pk) => pk;

        public ComplexResult ProcessComplex(IDictionary<String, Object> user, String[] items, Int32 count)
        {
            return new ComplexResult
            {
                UserId = user["id"].ToInt(),
                UserName = user["name"]?.ToString() ?? "",
                ItemCount = items?.Length ?? 0
            };
        }
    }

    class CalculateResult
    {
        public Int32 Value { get; set; }
        public String Operation { get; set; } = "";
        public Int32 A { get; set; }
        public Int32 B { get; set; }
    }

    class ComplexResult
    {
        public Int32 UserId { get; set; }
        public String UserName { get; set; } = "";
        public Int32 ItemCount { get; set; }
    }

    class ReceivableApiClient : ApiClient
    {
        public event Action<String, String?>? MessageReceived;

        public ReceivableApiClient(String uri) : base(uri) { }

        protected override void OnReceive(IMessage message, ApiReceivedEventArgs e)
        {
            base.OnReceive(message, e);

            var action = e.ApiMessage?.Action ?? "";
            var data = e.ApiMessage?.Data?.ToStr();
            MessageReceived?.Invoke(action, data);
        }
    }
    #endregion
}
