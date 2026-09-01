using NewLife.Log;
using NewLife.Model;

namespace NewLife.Remoting;

/// <summary>支持多协议的服务解析器。在 ConfigServiceResolver 基础上增加 tcp/udp/ws/wss 协议支持</summary>
/// <remarks>
/// 优先按首个地址的协议头路由：
/// ws/wss → WsClient（WebSocket 长连接）；
/// tcp/udp → ApiClient（SRMP 长连接）；
/// http/https → ApiHttpClient（HTTP 短连接）。
/// 同名服务复用同一 IApiClient 实例。
/// </remarks>
/// <remarks>实例化，指定配置提供者</remarks>
/// <param name="serviceProvider">服务提供者</param>
// CS9107：主构造函数参数同时传给基类又被本类捕获使用（BuildClient 内 GetService），属有意设计
#pragma warning disable CS9107
public class RemotingServiceResolver(IServiceProvider serviceProvider) : ConfigServiceResolver(serviceProvider)
#pragma warning restore CS9107
{
    /// <summary>根据地址字符串构建客户端，支持 http/https/tcp/udp/ws/wss 协议</summary>
    /// <param name="name">服务名，用作节点标识</param>
    /// <param name="address">地址，支持逗号或分号分隔的多地址（协议须一致）</param>
    protected override IApiClient BuildClient(String name, String address)
    {
        var addrs = address.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries);
        if (addrs.Length == 0) throw new InvalidOperationException($"服务[{name}]地址不能为空");

        var first = addrs[0].Trim();

#if !NET40
        if (first.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
        {
            var ws = new WsClient(address);
            ws.Open();
            return ws;
        }
#endif

        if (first.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("udp://", StringComparison.OrdinalIgnoreCase))
        {
            var client = new ApiClient(address)
            {
                Tracer = serviceProvider.GetService<ITracer>(),
                Log = serviceProvider.GetService<ILog>()!,
            };
            client.Open();
            return client;
        }

        return base.BuildClient(name, address);
    }
}
