# ApiServer 使用说明

本文介绍 `ApiServer` 的功能与用法，涵盖服务启动、控制器注册、请求处理、认证、可观测性、广播与与 Razor Pages 的集成建议（作为后台服务或边车）。

## 1. 概述
- 作用：应用层 RPC 服务器，基于 `IApiServer`（`ApiNetServer` 或 `ApiHttpServer`）承载网络会话。
- 传输：Tcp / Udp / Http / WebSocket（取决于 `Use(NetUri)` 的地址类型）。
- 契约：基于 `IMessage`/`ApiMessage` 与 `IEncoder`；默认 JSON，可切换 Http 语义。

## 2. 快速开始（Tcp）
```csharp
var server = new ApiServer(12345)
{
    Log = XTrace.Log
};
server.Register(new DemoController());
server.Start();

Console.ReadLine();
server.Stop("quit");
```

- 也可使用 `Use(new NetUri("tcp://*:12345"))` 指定监听地址。
- Http 监听：`Use(new NetUri("http://*:8080"))` 将使用 `ApiHttpServer`。

## 3. 控制器与动作注册
- 自动注册：构造函数已注册内置 `ApiController`，提供 `api/all`、`api/info` 等通用接口。
- 手动注册：
```csharp
server.Register(new MyController());         // 注册控制器全部公开方法
server.Register(new MyController(), "Echo"); // 仅注册命名方法
server.Register<MyController>();             // 注册类型（内部会创建实例）
```
- 依赖注入：设置 `ServiceProvider` 后，创建控制器实例时可从容器解析。

## 4. 请求处理流程
- `Process(session, msg, serviceProvider)`：
  - 解码 `IMessage` 为 `ApiMessage`，按 `action` 路由到 `IApiHandler` 执行。
  - 捕获异常并映射为 `ApiCode` 与错误消息（数据库异常脱敏）。
  - `OneWay` 请求不返回响应。
  - 若 `UseHttpStatus=true`，对 Http 场景使用 HTTP 状态码表达结果。
  - finally 中记录慢处理日志（超过 `SlowTrace`）。

## 5. 认证与会话
- token 模式：
  - 客户端可在参数携带 `Token`。
  - 服务端可在 `IApiHandler.Prepare` 或控制器中校验 Token，并将与该 Token 关联的状态放入 `IApiSession.Items`。
- `OnProcess`：默认委托给 `Handler.Execute`，可重写以实现统一的鉴权与拦截。

## 6. 可观测性与统计
- 日志：`ILog Log`（服务器日志）、`SessionLog`（会话日志）。
- 追踪：`ITracer? Tracer`。
- 慢处理：`SlowTrace`（毫秒）。超过阈值输出包含 Action、Code、耗时的慢处理日志。
- 统计：`ICounter? StatProcess` + `StatPeriod`（秒）定期输出处理统计与底层网络状态。

## 7. WebSocket 与 Http
- WebSocket：自动切换到 `WebSocketClientCodec` 进行消息帧编解码。
- Http：`ApiHttpServer` 配合 `HttpEncoder` 支持 GET/POST 映射到 `ApiMessage`；可通过 `UseHttpStatus` 切换为 HTTP 状态码表达结果。

## 8. 端口与地址复用
- `ReuseAddress`：允许多进程复用端口，用于滚动重启或多进程监听（需系统支持）。
- `Multiplex`：同一 TCP 连接允许在未完成的请求期间并行处理新请求（提升吞吐）。

## 9. 广播
- `InvokeAll(action, args)`：向所有会话单向发送通知；返回成功会话数。

## 10. 资源释放
- `Stop(reason)`：停止网络服务器与统计定时器。
- `Dispose()`：释放资源并解除与默认 `ApiController` 的相互引用。

## 11. 在 ASP.NET Core Razor Pages 中的集成（作为后台服务）
- 作为宿主内的后台服务（不直接暴露在公网）示例：
```csharp
builder.Services.AddSingleton(sp =>
{
    var svr = new ApiServer(12345)
    {
        Log = XTrace.Log,
        UseHttpStatus = false
    };
    // 注册业务控制器
    svr.Register(new MyController());
    // 启动
    svr.Start();
    return svr;
});
```
- 注意：
  - 使用 `UseHttpStatus=false` 保持与默认 JSON 语义兼容；若面向纯 Http 客户端可设为 true。
  - 将服务监听在内网地址，供同机的 Razor Pages 调用，或通过反向代理暴露必要接口。

## 12. 常见问题
- Q：如何查看当前有哪些已注册动作？A：`ShowService()` 会在启动时输出服务列表。
- Q：如何自定义路由或拦截？A：提供自定义 `IApiHandler` 并赋给 `Handler`，或重写 `OnProcess`。
- Q：是否支持 Udp？A：底层 `ApiNetServer` 支持 NetType；一般推荐 Tcp/Http/WebSocket。
