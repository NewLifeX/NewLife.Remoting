using NewLife.Messaging;

namespace NewLife.Remoting;

/// <summary>流式泵委托。把 IAsyncEnumerable&lt;T&gt; 实例逐条编码为流式帧发送</summary>
/// <param name="streamObj">IAsyncEnumerable&lt;T&gt; 实例</param>
/// <param name="session">会话</param>
/// <param name="msg">原始请求消息</param>
/// <param name="action">动作名</param>
/// <param name="enc">编码器</param>
/// <param name="cancellationToken">取消令牌</param>
/// <returns>异步任务</returns>
public delegate Task ApiStreamPumpDelegate(Object streamObj, IApiSession session, IMessage msg, String action, IEncoder enc, CancellationToken cancellationToken);

/// <summary>流式泵。逐条迭代 IAsyncEnumerable&lt;T&gt; 并发送流式帧</summary>
/// <remarks>
/// <para>IAsyncEnumerable&lt;T&gt; 的泛型实参在服务端运行期才确定，运行期泛型实例化（MakeGenericMethod）在 NativeAOT 下不可用。</para>
/// <para>因此由 NewLife.Remoting 源生成器在编译期为动作的元素类型生成静态泵注册（<see cref="ApiActionTable.RegisterStreamPump"/>），
/// 本类的泛型方法提供可被静态扎根的泵实现。</para>
/// </remarks>
public static class ApiStreamPump
{
    /// <summary>逐条迭代并发送流式帧</summary>
    /// <typeparam name="T">元素类型（值类型或引用类型）</typeparam>
    /// <param name="streamObj">IAsyncEnumerable&lt;T&gt; 实例</param>
    /// <param name="session">会话</param>
    /// <param name="msg">原始请求消息</param>
    /// <param name="action">动作名</param>
    /// <param name="enc">编码器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    public static async Task PumpAsync<T>(Object streamObj, IApiSession session, IMessage msg, String action, IEncoder enc, CancellationToken cancellationToken)
    {
        var stream = (IAsyncEnumerable<T>)streamObj;
#pragma warning disable CA2007 // 库内部线程池调用，无需 ConfigureAwait
        await foreach (var item in stream.WithCancellation(cancellationToken))
        {
            using var rs = enc.CreateStreamResponse(msg, action, 0, item, false);
            session.SendMessage(rs);
        }
#pragma warning restore CA2007
    }
}
