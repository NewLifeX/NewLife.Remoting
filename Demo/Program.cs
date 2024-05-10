using System.Net;
using NewLife.Data;
using NewLife.Log;
using NewLife.Net;
using NewLife.Remoting;

namespace Demo;

internal class Program
{
    static void Main(string[] args)
    {
        XTrace.UseConsole();

        var netUri = new NetUri(NetType.Tcp, IPAddress.Any, 5001);
        using var server = new ApiServer(netUri)
        {
            Log = XTrace.Log,
            EncoderLog = XTrace.Log,
            ShowError = true
        };
        server.Register<BigController>();
        server.Start();

        ClientTest();

        Console.ReadKey();
    }

    static void ClientTest()
    {
        var client = new MyClient("tcp://127.0.0.1:5001")
        {
            Log = XTrace.Log,
            EncoderLog = XTrace.Log
        };
        client.Received += (s, e) =>
        {
            XTrace.WriteLine("通知：{0} 参数：{1}", e.ApiMessage.Action, e.ApiMessage.Data.ToStr());
        };

        var rs = client.Invoke<Int32>("Big/Sum", new { a = 123, b = 456 });
        XTrace.WriteLine("{0}+{1}={2}", 123, 456, rs);
    }

    class MyClient : ApiClient
    {
        public MyClient(String urls) : base(urls) { }

    }

    class BigController : IApi
    {
        public IApiSession Session { get; set; }

        public int Sum(int a, int b)
        {
            Task.Run(async () =>
            {
                await Task.Delay(1000);

                Session.InvokeOneWay("test", new { name = "Stone", company = "NewLife" }, 3);
            });

            return a + b;
        }
        public string ToUpper(string str) => str.ToUpper();

        public Packet Test(Packet pk)
        {
            var buf = pk.ReadBytes().Select(e => (Byte)(e ^ 'x')).ToArray();

            return buf;
        }
    }
}