using NewLife.Data;
using NewLife.Log;
using NewLife.Net;
using NewLife.Remoting;
using NewLife.Serialization;

namespace Test;

class Program
{
    static void Main(String[] args)
    {
        XTrace.UseConsole();

        try
        {
            //TestHyperLogLog();
            Test1();
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }

        Console.WriteLine("OK!");
        Console.ReadKey();
    }

    static ApiServer _Server;
    static void Test1()
    {
        var obj = new { state = "abcd", state2 = 1234 };
        var buf = Binary.FastWrite(obj);
        var str = buf.ToHex();

        var server = new ApiServer(5500)
        {
            Log = XTrace.Log,
            EncoderLog = XTrace.Log
        };

        server.Start();

        _Server = server;
    }
}