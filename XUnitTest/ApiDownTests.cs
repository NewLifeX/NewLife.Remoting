using System;
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

/// <summary>RPC下行指令测试</summary>
public class ApiDownTests : DisposeBase
{
    private readonly ApiServer _Server;
    private readonly MyClient _Client;
    private readonly String _Uri;

    public ApiDownTests()
    {
        var port = Rand.Next(10000, 65535);

        _Server = new ApiServer(port)
        {
            Log = XTrace.Log,
            //EncoderLog = XTrace.Log,
            ShowError = true,
        };
        _Server.Start();

        _Uri = $"tcp://127.0.0.1:{port}";

        var client = new MyClient()
        {
            Servers = new[] { _Uri },
            //Log = XTrace.Log
        };
        //client.EncoderLog = XTrace.Log;
        _Client = client;
    }

    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        _Server.TryDispose();
    }

    [Fact]
    public async Task Test1()
    {
        var apis = await _Client.InvokeAsync<String[]>("api/all");
        Assert.NotNull(apis);

        // 记录当前消息，用于判断是否有新消息到达
        var previousMessage = _Client.LastMessage;

        // 服务端主动下发
        var ss = _Server.Server.AllSessions[0];
        var args = new { name = "Stone", age = 36 };
        ss.InvokeOneWay("CustomCommand", args);

        // 等待新消息到达（与之前的消息不同）
        for (var i = 0; i < 50 && _Client.LastMessage == previousMessage; i++)
        {
            await Task.Delay(50);
        }

        var msg = _Client.LastMessage;
        Assert.NotNull(msg);
        Assert.NotSame(previousMessage, msg);

        // 解码消息
        var messge = _Client.Encoder.Decode(msg);
        Assert.NotNull(messge);
        Assert.Equal("CustomCommand", messge.Action);
        Assert.Equal(0, messge.Code);
        Assert.Equal(JsonHelper.ToJson(args), messge.Data.ToStr());
    }

    private class MyClient : ApiClient
    {
        public IMessage LastMessage { get; set; }

        protected override void OnReceive(IMessage message, ApiReceivedEventArgs e) => LastMessage = message;
    }
}