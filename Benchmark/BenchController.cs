using NewLife.Data;

namespace NewLife.Remoting.Benchmarks;

/// <summary>性能测试控制器。提供多种场景的API接口，用于RPC性能基准测试</summary>
public class BenchController
{
    /// <summary>无参返回Int32</summary>
    public Int32 NoArg() => 42;

    /// <summary>String出入参，直接返回收到的字符串</summary>
    /// <param name="input">输入字符串</param>
    public String EchoString(String input) => input;

    /// <summary>多基础类型参数</summary>
    /// <param name="a">Int32参数</param>
    /// <param name="b">Int64参数</param>
    /// <param name="c">String参数</param>
    /// <param name="d">Boolean参数</param>
    /// <param name="e">Double参数</param>
    public Int32 MultiArgs(Int32 a, Int64 b, String c, Boolean d, Double e) => a;

    /// <summary>IPacket出入参，直接返回收到的数据包</summary>
    /// <param name="pk">数据包</param>
    public IPacket EchoPacket(IPacket pk) => pk;
}
