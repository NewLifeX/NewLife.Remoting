using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Serialization;
using Xunit;

namespace XUnitTest.Integration;

/// <summary>流式调用集成测试。验证 Server-Streaming 全链路</summary>
public class StreamingIntegrationTests : DisposeBase
{
    private readonly ApiServer _Server;
    private readonly Int32 _Port;

    public StreamingIntegrationTests()
    {
        _Server = new ApiServer(0)
        {
            Log = XTrace.Log,
            ShowError = true,
        };
        _Server.Register<StreamingController>();
        _Server.Start();

        _Port = _Server.Port;
    }

    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        _Server.TryDispose();
    }

    [Fact(DisplayName = "流式调用_逐条接收Int32")]
    public async Task StreamInt32Test()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");
        client.Open();

        var list = new List<Int32>();
        await foreach (var item in client.InvokeStreamAsync<Int32>("Streaming/Range", new { count = 5 }))
        {
            list.Add(item);
        }

        Assert.Equal(5, list.Count);
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, list);
    }

    [Fact(DisplayName = "流式调用_逐条接收String")]
    public async Task StreamStringTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");
        client.Open();

        var list = new List<String>();
        await foreach (var item in client.InvokeStreamAsync<String>("Streaming/Logs", new { count = 3 }))
        {
            list.Add(item);
        }

        Assert.Equal(3, list.Count);
        Assert.All(list, s => Assert.StartsWith("Log #", s));
    }

    [Fact(DisplayName = "流式调用_空结果")]
    public async Task StreamEmptyTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");
        client.Open();

        var list = new List<Int32>();
        await foreach (var item in client.InvokeStreamAsync<Int32>("Streaming/Range", new { count = 0 }))
        {
            list.Add(item);
        }

        Assert.Empty(list);
    }

    [Fact(DisplayName = "流式调用_中途取消")]
    public async Task StreamCancellationTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");
        client.Open();

        var cts = new CancellationTokenSource();
        var list = new List<Int32>();
        var exCount = 0;

        try
        {
            await foreach (var item in client.InvokeStreamAsync<Int32>("Streaming/Infinite", null, cts.Token))
            {
                list.Add(item);
                if (list.Count >= 3)
                    cts.Cancel();
            }
        }
        catch (OperationCanceledException)
        {
            exCount++;
        }

        Assert.True(list.Count >= 3, $"应至少收到3条数据，实际 {list.Count}");
        Assert.Equal(1, exCount);
    }

    [Fact(DisplayName = "流式调用_单条数据")]
    public async Task StreamSingleItemTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");
        client.Open();

        var list = new List<String>();
        await foreach (var item in client.InvokeStreamAsync<String>("Streaming/Logs", new { count = 1 }))
        {
            list.Add(item);
        }

        Assert.Single(list);
    }

    #region 测试控制器
    class StreamingController
    {
        public async IAsyncEnumerable<Int32> Range(Int32 count)
        {
            for (var i = 0; i < count; i++)
            {
                yield return i;
            }
        }

        public async IAsyncEnumerable<String> Logs(Int32 count)
        {
            for (var i = 0; i < count; i++)
            {
                yield return $"Log #{i}: {DateTime.Now:HH:mm:ss}";
            }
        }

        public async IAsyncEnumerable<Int32> Infinite()
        {
            var i = 0;
            while (true)
            {
                await Task.Delay(1);
                yield return i++;
            }
        }
    }
    #endregion
}
