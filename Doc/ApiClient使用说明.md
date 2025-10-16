# ApiClient 使用说明

本文介绍 `ApiClient` 的功能与用法，涵盖连接、调用、认证、重试、可观测性与在 ASP.NET Core Razor Pages 中的集成建议。

## 1. 概述
- 作用：面向应用的 RPC 客户端，保持到服务端的长连接，支持请求-响应与服务端单向推送。
- 传输：Tcp / Http / WebSocket（由服务端与地址决定）。
- 负载：支持地址列表与连接池（高吞吐）或单连接（低时延）。

## 2. 快速开始
```csharp
var client = new ApiClient("tcp://127.0.0.1:12345");
client.Log = XTrace.Log; // 可选
client.Open();

var hello = await client.InvokeAsync<string>("demo/hello", new { name = "world" });
Console.WriteLine(hello);

client.Close("done");
```

- 指定多个地址（逗号/分号分隔）以做简单的负载与切换：`new ApiClient("tcp://a:1234,tcp://b:1234")`。
- Http 场景可直接使用 `ApiHttpClient`：`new ApiHttpClient("http://host:port")`。

## 3. 连接与集群
- `Servers`：服务端地址集合。
- `UsePool`：是否使用连接池；true 为多连接（高吞吐），false 为单连接（低时延）。
- `Local`：多 IP 环境下绑定本地地址。
- `Cluster`：内部使用 `ClientSingleCluster` 或 `ClientPoolCluster` 管理 `ISocketClient`。

## 4. 远程调用
- `InvokeAsync<TResult>(action, args, ct)`：异步请求-响应。
- `Invoke<TResult>(action, args)`：同步阻塞等待（内部创建超时令牌）。
- `InvokeOneWay(action, args, flag=0)`：单向发送，不等待应答。
- 服务端主动推送：订阅 `Received` 事件接收来自服务端的通知消息。

## 5. 认证与 Token
- `Token`：若设置，调用时会自动注入到参数集合（键 `Token`）。
  - 字典参数：原地注入；对象参数：尝试写入属性 `Token`，否则转换成字典后注入。
- 连接建立或断线重连后，客户端会触发 `OnNewSession`，默认异步调用 `OnLoginAsync(client, force=true)`，可重写实现登录。
- 显式登录：`await client.LoginAsync()` 会对集群中连接执行登录。

## 6. 错误与重试
- 401（Unauthorized）：框架自动调用 `OnLoginAsync(force=true)`，并在同一连接上重发一次，不计入重试次数。
- 可选重试：
  - `IRetryPolicy? RetryPolicy` + `int MaxRetries`（默认 0）用于对非 401 异常进行有限次重试。
  - 策略可控制是否等待、等待时长、是否在重试前切换连接。
- 超时：`Timeout`（毫秒）。当触发超时，会以简短消息抛出 `TaskCanceledException`。

## 7. 编解码与序列化
- `Encoder`：默认 `JsonEncoder`。
- `JsonHost`：自定义 JSON 序列化行为（如大小写、日期格式、忽略空值等）。
- `EncoderLog`：编码器日志（可定向到 `XTrace.Log`）。

## 8. 可观测性与统计
- 日志：`ILog Log`、`ILog SocketLog`。
- 追踪：`ITracer? Tracer`。`client.Tracer` 的底层 Socket Trace 受日志级别控制——仅当日志级别为 Debug 时打开，以减少常规运行期间的埋点量与开销。
- 慢调用：`SlowTrace`（毫秒），超过阈值输出慢调用日志。
- 统计：`ICounter? StatInvoke`，设置 `StatPeriod`（秒）后周期性输出调用统计。

## 9. WebSocket / Http 与管线
- 当地址为 WebSocket 时，客户端管线会注入 `WebSocketClientCodec` 并清空默认处理器，开启基于消息包的通信。
- Http 客户端建议使用 `ApiHttpClient`，其在 `Encoder` 与 Filter（如 `TokenHttpFilter`）方面更贴近 Http 语义。

## 10. 资源释放
- `Close(reason)`：关闭集群与连接，释放定时器。
- `Dispose()`：析构时自动关闭（GC）或显式释放（Dispose）。

## 11. 在 ASP.NET Core Razor Pages 中的集成
- 注册为单例并在启动时 Open：
```csharp
builder.Services.AddSingleton(sp =>
{
    var cli = new ApiClient("tcp://127.0.0.1:12345")
    {
        Log = XTrace.Log,
        Token = "your-token",
        UsePool = false,
    };
    cli.Open();
    return cli;
});
```
- 在 `PageModel` 中使用：
```csharp
public class IndexModel : PageModel
{
    private readonly ApiClient _client;
    public IndexModel(ApiClient client) => _client = client;

    public async Task OnGet()
    {
        var info = await _client.InvokeAsync<IDictionary<string, object>>("api/info");
        ViewData["ServerInfo"] = info;
    }
}
```
- 注意：
  - 保持单例与长连接，避免每次请求新建连接。
  - 结合应用日志级别控制是否开启底层 Trace。

## 12. 常见问题
- Q：如何指定使用本机某网卡地址？A：设置 `Local`。
- Q：如何平滑切换服务端地址？A：调用 `SetServer(newUris)`，内部会在下次获取连接时生效。
- Q：如何接收服务端主动消息？A：订阅 `Received` 事件。
