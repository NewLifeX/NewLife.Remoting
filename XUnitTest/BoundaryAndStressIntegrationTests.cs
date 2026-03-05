using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Security;
using Xunit;

namespace XUnitTest.Remoting;

/// <summary>边界条件与压力集成测试</summary>
/// <remarks>
/// 验证空参数/空返回、零字节/单字节 Packet、超大并发连接、
/// 快速创建销毁客户端（连接抖动）、void 返回等边界场景。
/// </remarks>
public class BoundaryAndStressIntegrationTests : DisposeBase
{
    private readonly ApiServer _Server;
    private readonly Int32 _Port;

    public BoundaryAndStressIntegrationTests()
    {
        _Server = new ApiServer(0)
        {
            Log = XTrace.Log,
            ShowError = true,
        };
        _Server.Register<BoundaryController>();
        _Server.Start();

        _Port = _Server.Port;
    }

    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        _Server.TryDispose();
    }

    #region 空参数与空返回
    [Fact(DisplayName = "边界_无参方法正常调用")]
    public async Task NoParameterCallTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var result = await client.InvokeAsync<String>("Boundary/NoParam");
        Assert.Equal("NoParam", result);
    }

    [Fact(DisplayName = "边界_返回null")]
    public async Task NullReturnTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var result = await client.InvokeAsync<String>("Boundary/ReturnNull");
        Assert.Null(result);
    }

    [Fact(DisplayName = "边界_void返回方法")]
    public async Task VoidReturnTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        // void 方法不抛异常即为成功
        await client.InvokeAsync<Object>("Boundary/VoidAction");
    }

    [Fact(DisplayName = "边界_空字符串参数")]
    public async Task EmptyStringParameterTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        // 空字符串经 JSON 序列化后反序列化为 null
        var result = await client.InvokeAsync<String>("Boundary/Echo", new { msg = "" });
        Assert.True(result.IsNullOrEmpty());
    }

    [Fact(DisplayName = "边界_返回空字符串")]
    public async Task EmptyStringReturnTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        // 空字符串经 JSON 序列化后反序列化为 null
        var result = await client.InvokeAsync<String>("Boundary/ReturnEmpty");
        Assert.True(result.IsNullOrEmpty());
    }
    #endregion

    #region Packet边界
    [Fact(DisplayName = "边界_零字节Packet")]
    public async Task ZeroBytePacketTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var data = Array.Empty<Byte>();
        var result = await client.InvokeAsync<Packet>("Boundary/EchoPacket", data);

        // 零字节场景下，结果可能为空或长度为0
        if (result != null)
            Assert.Equal(0, result.Total);
    }

    [Fact(DisplayName = "边界_单字节Packet")]
    public async Task SingleBytePacketTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var data = new Byte[] { 0xAB };
        var result = await client.InvokeAsync<Packet>("Boundary/EchoPacket", data);

        Assert.NotNull(result);
        Assert.Equal(1, result.Total);
        Assert.Equal(0xAB, result.ToArray()[0]);
    }

    [Theory(DisplayName = "边界_多种大小Packet回显")]
    [InlineData(1)]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(1023)]
    [InlineData(1024)]
    [InlineData(4096)]
    [InlineData(65535)]
    public async Task VariousSizePacketTest(Int32 size)
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var data = Rand.NextBytes(size);
        var result = await client.InvokeAsync<Packet>("Boundary/EchoPacket", data);

        Assert.NotNull(result);
        Assert.Equal(size, result.Total);
        Assert.True(data.SequenceEqual(result.ToArray()));
    }
    #endregion

    #region 多连接并发
    [Fact(DisplayName = "压力_20个客户端同时工作")]
    public async Task TwentyClientsConcurrentTest()
    {
        var clientCount = 20;
        var clients = new List<ApiClient>();
        var tasks = new List<Task<String>>();

        try
        {
            for (var i = 0; i < clientCount; i++)
            {
                var client = new ApiClient($"tcp://127.0.0.1:{_Port}");
                clients.Add(client);
                tasks.Add(client.InvokeAsync<String>("Boundary/Echo", new { msg = $"Client{i}" }));
            }

            var results = await Task.WhenAll(tasks);

            for (var i = 0; i < clientCount; i++)
            {
                Assert.Equal($"Client{i}", results[i]);
            }

            // 验证服务端会话数
            Assert.Equal(clientCount, _Server.Server.AllSessions.Length);
        }
        finally
        {
            foreach (var c in clients) c.TryDispose();
        }
    }

    [Fact(DisplayName = "压力_单客户端200次连续调用")]
    public async Task SingleClientHighVolumeTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        for (var i = 0; i < 200; i++)
        {
            var result = await client.InvokeAsync<Int32>("Boundary/Add", new { a = i, b = 1 });
            Assert.Equal(i + 1, result);
        }
    }

    [Fact(DisplayName = "压力_多客户端并发密集调用")]
    public async Task MultiClientIntensiveTest()
    {
        var clientCount = 5;
        var callsPerClient = 50;
        var clients = new List<ApiClient>();

        try
        {
            var allTasks = new List<Task>();

            for (var c = 0; c < clientCount; c++)
            {
                var client = new ApiClient($"tcp://127.0.0.1:{_Port}");
                clients.Add(client);

                var clientIndex = c;
                allTasks.Add(Task.Run(async () =>
                {
                    for (var i = 0; i < callsPerClient; i++)
                    {
                        var result = await client.InvokeAsync<Int32>("Boundary/Add", new { a = clientIndex * 1000 + i, b = 1 });
                        Assert.Equal(clientIndex * 1000 + i + 1, result);
                    }
                }));
            }

            await Task.WhenAll(allTasks);
        }
        finally
        {
            foreach (var c in clients) c.TryDispose();
        }
    }
    #endregion

    #region 连接抖动
    [Fact(DisplayName = "抖动_快速创建销毁客户端")]
    public async Task RapidConnectDisconnectTest()
    {
        for (var i = 0; i < 10; i++)
        {
            using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");
            var result = await client.InvokeAsync<String>("Boundary/NoParam");
            Assert.Equal("NoParam", result);
        }

        // 等待服务端完成会话清理
        await Task.Delay(500);
        Assert.Empty(_Server.Server.AllSessions);
    }

    [Fact(DisplayName = "抖动_并发创建销毁客户端")]
    public async Task ConcurrentConnectDisconnectTest()
    {
        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");
            var result = await client.InvokeAsync<String>("Boundary/Echo", new { msg = $"Rapid{i}" });
            Assert.Equal($"Rapid{i}", result);
        }).ToList();

        await Task.WhenAll(tasks);

        // 等待服务端完成会话清理
        await Task.Delay(500);
        Assert.Empty(_Server.Server.AllSessions);
    }
    #endregion

    #region 特殊字符与数据
    [Theory(DisplayName = "边界_特殊字符参数")]
    [InlineData("Hello World")]
    [InlineData("中文测试")]
    [InlineData("特殊符号!@#$%^&*()")]
    [InlineData("换行\n制表\t回车\r")]
    [InlineData("引号\"和反斜杠\\")]
    public async Task SpecialCharacterParameterTest(String input)
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var result = await client.InvokeAsync<String>("Boundary/Echo", new { msg = input });
        Assert.Equal(input, result);
    }

    [Fact(DisplayName = "边界_超长字符串")]
    public async Task VeryLongStringTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var longStr = new String('X', 10_000);
        var result = await client.InvokeAsync<String>("Boundary/Echo", new { msg = longStr });
        Assert.Equal(longStr, result);
    }

    [Fact(DisplayName = "边界_数值溢出边界")]
    public async Task IntegerBoundaryTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var result = await client.InvokeAsync<Int64>("Boundary/AddLong", new { a = Int32.MaxValue, b = 1L });
        Assert.Equal((Int64)Int32.MaxValue + 1, result);
    }
    #endregion

    #region 多种返回类型
    [Fact(DisplayName = "边界_返回数组")]
    public async Task ArrayReturnTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var result = await client.InvokeAsync<Int32[]>("Boundary/GetArray", new { count = 5 });
        Assert.NotNull(result);
        Assert.Equal(5, result.Length);
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(i, result[i]);
        }
    }

    [Fact(DisplayName = "边界_返回字典")]
    public async Task DictionaryReturnTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var result = await client.InvokeAsync<IDictionary<String, Object>>("Boundary/GetMap");
        Assert.NotNull(result);
        Assert.Equal("Value1", result["Key1"]?.ToString());
        Assert.Equal("Value2", result["Key2"]?.ToString());
    }

    [Fact(DisplayName = "边界_返回布尔值")]
    public async Task BooleanReturnTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var resultTrue = await client.InvokeAsync<Boolean>("Boundary/IsPositive", new { value = 1 });
        Assert.True(resultTrue);

        var resultFalse = await client.InvokeAsync<Boolean>("Boundary/IsPositive", new { value = -1 });
        Assert.False(resultFalse);
    }
    #endregion

    #region 辅助类
    class BoundaryController
    {
        public String NoParam() => "NoParam";

        public String? ReturnNull() => null;

        public void VoidAction() { }

        public String Echo(String msg) => msg;

        public String ReturnEmpty() => "";

        public IPacket EchoPacket(IPacket pk) => pk;

        public Int32 Add(Int32 a, Int32 b) => a + b;

        public Int64 AddLong(Int64 a, Int64 b) => a + b;

        public Int32[] GetArray(Int32 count)
        {
            var arr = new Int32[count];
            for (var i = 0; i < count; i++) arr[i] = i;
            return arr;
        }

        public IDictionary<String, Object> GetMap() => new Dictionary<String, Object>
        {
            ["Key1"] = "Value1",
            ["Key2"] = "Value2",
        };

        public Boolean IsPositive(Int32 value) => value > 0;
    }
    #endregion
}
