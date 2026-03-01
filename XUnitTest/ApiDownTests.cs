using System;
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

        // 服务端主动下发
        var ss = _Server.Server.AllSessions[0];
        var args = new { name = "Stone", age = 36 };
        ss.InvokeOneWay("CustomCommand", args);

        // 等待消息到达
        for (var i = 0; i < 50 && !_Client.HasReceivedMessage; i++)
        {
            await Task.Delay(50);
        }

        Assert.True(_Client.HasReceivedMessage);
        Assert.Equal("CustomCommand", _Client.LastAction);
        Assert.Equal(0, _Client.LastCode);
        Assert.Equal(JsonHelper.ToJson(args), _Client.LastData);
    }

    private class MyClient : ApiClient
    {
        public Boolean HasReceivedMessage { get; set; }
        public String? LastAction { get; set; }
        public Int32 LastCode { get; set; }
        public String? LastData { get; set; }

        protected override void OnReceive(IMessage message, ApiReceivedEventArgs e)
        {
            // OnReceive 退出后 message.Payload 和 e.ApiMessage 会被释放，必须在此处捕获数据
            if (e.ApiMessage != null)
            {
                LastAction = e.ApiMessage.Action;
                LastCode = e.ApiMessage.Code;
                LastData = e.ApiMessage.Data?.ToStr();
                HasReceivedMessage = true;
            }
        }
    }
}