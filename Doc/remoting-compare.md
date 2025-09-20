# 与主流框架的对比（gRPC / ASP.NET Core Minimal APIs）

本文对比 `NewLife.Remoting` 与常见 .NET RPC/Web 框架的差异与取舍，便于在不同场景下做选型。

## 总览
- 传输层
  - NewLife.Remoting：`Tcp`/`Http`/`WebSocket`，可长连接，支持连接池与单连接模式。
  - gRPC：HTTP/2（必需），双向流、服务端/客户端流、Unary 调用。
  - Minimal APIs：HTTP/1.1/2/3，REST 友好，面向请求/响应。
- 契约与序列化
  - NewLife.Remoting：消息契约（`IMessage`/`ApiMessage`），默认 JSON，可替换 `IEncoder`（`JsonEncoder`/`HttpEncoder`）。
  - gRPC：强契约（.proto），默认 Protobuf（高性能、跨语言）。
  - Minimal APIs：HTTP/JSON，OpenAPI/Swagger 生态完善。
- 连接与并发
  - NewLife.Remoting：长连接/连接池、`Multiplex` 并发复用、`ISocketClient` 事件回推（服务端可主动推单向消息）。
  - gRPC：HTTP/2 多路复用、流式调用天然支持。
  - Minimal APIs：短连接或 HTTP/2 复用，无“会话级”API 概念。
- 认证与拦截
  - NewLife.Remoting：`Token` 注入、`IApiHandler`/`IApiManager` 扩展、`Received` 事件、`ServiceProvider` 注入。
  - gRPC：拦截器、元数据（Metadata）、TLS/Token 生态成熟。
  - Minimal APIs：中间件（Middleware）+ 身份认证/授权组件。
- 可观测性
  - NewLife.Remoting：`ILog`、`ITracer`、慢调用/处理日志、`ICounter` 定时统计。`client.Tracer` 受日志级别控制（Debug 打开详细 Trace）。
  - gRPC：普遍采用 OpenTelemetry/Activity；生态完备。
  - Minimal APIs：同 ASP.NET Core 管道生态。
- 错误表达
  - NewLife.Remoting：`ApiCode`（或 `UseHttpStatus` 切换 HTTP 状态），异常映射可定制，数据库异常脱敏。
  - gRPC：`StatusCode` + `RpcException`。
  - Minimal APIs：HTTP 状态码 + 约定的 JSON 错误体。

## 适用场景
- NewLife.Remoting
  - 内网/设备场景需要长连接、双向消息（服务端主动推送）。
  - 更轻的部署依赖；无需 .proto；编码器可替换。
  - 自定义协议/编码/统计需求强；需接入现有 `NewLife.*` 生态。
- gRPC
  - 跨语言、强契约、高性能二进制；双向/流式交互丰富；生态成熟。
- Minimal APIs
  - 面向 Web/REST 的开放 API；与前端/第三方集成友好；工具链完善。

## 能力映射
- 认证
  - NewLife.Remoting：参数注入 `Token`，或在 `OnLoginAsync` 中执行登录动作；服务端可在 `IApiHandler` 中统一校验。
  - gRPC：Metadata + Interceptor。
  - Minimal APIs：Auth 中间件 + 过滤器。
- 负载与连接复用
  - NewLife.Remoting：`ICluster`（单连接/连接池）、`SetServer` 平滑切换、可选 `IRetryPolicy` 重试。
  - gRPC：HTTP/2 多路复用，服务发现/负载均衡通常由网关或 Sidecar 提供。
- 可观测性
  - NewLife.Remoting：`SlowTrace` 阈值日志、`ITracer` 链路段（按 Debug 级别打开底层 Socket Trace）、`ICounter` 周期统计。

## 取舍与注意
- 轻量与自定义优先：默认 JSON，便于排障与集成；如需更高性能，可自定义 `IEncoder`（二进制）。
- 连接模型：长连接与单向推送对设备/内网友好；面向开放 Web 的情况下，可优先选 REST/gRPC。
- 可观测性：常规运行降低 Trace 量级；排障时提升日志级别至 Debug 即可启用 Socket Trace。
- 重试：`IRetryPolicy` 与 `MaxRetries` 默认关闭，仅对非 401 异常生效；401 内置登录后重发，不计入重试。

## 快速对照表（摘要）
- 契约：消息（JSON/可替换） | .proto/Protobuf | HTTP/JSON
- 连接：长连接/池/复用 | HTTP/2 复用/流 | HTTP/1.1/2/3
- 推送：支持（单向） | 支持（流） | 需 WebSocket/SignalR
- 认证：Token 注入/登录钩子 | Metadata/Interceptor | Middleware/Auth
- 可观测性：ILog/ITracer/Counter | OTel/Activity | ASP.NET Core 生态

## 结论
- 若你需要轻量、可自定义协议/编码、长连接/设备友好、统一日志/追踪/统计，`NewLife.Remoting` 是合适选择。
- 若你需要强契约、跨语言、二进制高性能、成熟生态，倾向 gRPC。
- 面向 Web/第三方集成优先 REST，选择 Minimal APIs。
