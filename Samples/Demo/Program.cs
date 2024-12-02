using System.Net;
using System.Text;
using NewLife.Data;
using NewLife.Log;
using NewLife.Net;
using NewLife.Remoting;

namespace Demo;

internal class Program
{
    static void Main(String[] args)
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
            XTrace.WriteLine("通知：{0} 参数：{1}", e.ApiMessage?.Action, e.ApiMessage?.Data?.ToStr());
        };

        var rs = client.Invoke<Int32>("Big/Sum", new { a = 123, b = 456 });
        XTrace.WriteLine("{0}+{1}={2}", 123, 456, rs);

        //Big Json Test 当返回值json超级大10MB 报错：System.Exception:“解码错误，无法找到服务名！” 小json一切正常
        var resBigJsonTest = client.Invoke<string>("Big/BigJsonTest");
        XTrace.WriteLine($"resBigJsonTest.Length={resBigJsonTest.Length}");
    }

    class MyClient : ApiClient
    {
        public MyClient(String urls) : base(urls) { }

    }

    class BigController : IApi
    {
        public IApiSession Session { get; set; }

        public Int32 Sum(Int32 a, Int32 b)
        {
            Task.Run(async () =>
            {
                await Task.Delay(1000);

                Session.InvokeOneWay("test", new { name = "Stone", company = "NewLife" }, 3);
            });

            return a + b;
        }
        public String ToUpper(String str) => str.ToUpper();


        public IPacket Test(IPacket pk)
        {
            var buf = pk.ReadBytes().Select(e => (Byte)(e ^ 'x')).ToArray();

            return (ArrayPacket)buf;
        }
        /// <summary>
        /// 当循环1000次 拼接 就报错超时了， System.TimeoutException:“请求[Big/BigJsonTest]超时(01 Seq=02 [32](8, 16)<16>)！”
        /// 当循环10次拼接  正常
        /// </summary>
        /// <returns></returns>
        public string BigJsonTest()
        {
            StringBuilder sb = new StringBuilder();
            //拼接10万次   就报错了
            for (int i = 0; i < 10000; i++)
            {
                sb.AppendLine("big json big json big json big json big json big json big json big json big json big json big json big json big json big json big json big json big json big json big json ");
            }
            string str = sb.ToString();
            File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + "bigJsonTest.txt", str);
            Console.WriteLine($"sb.Length={str.Length}");
            return str;
        }
    }
}