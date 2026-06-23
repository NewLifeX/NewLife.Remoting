# RPC 重构需求

## 1. 背景与目标

### 背景
NewLife.Remoting 已有成熟的 RPC 通信架构（ApiClient/ApiServer），支持 TCP/UDP/WebSocket 多协议长连接通信、SRMP 二进制协议、JSON 编解码。但与主流 RPC 框架（如 gRPC）相比，仍缺少以下能力：
- Server-Streaming 流式调用（服务端持续推送多条响应）
- 通用元数据/上下文透传机制（当前仅支持单一 Token 字段）
- 高并发大字符串场景存在稳定性问题（Benchmark 32 并发 2000 字符 N/A）

### 目标
1. **流式调用**：支持 Controller 返回 `IAsyncEnumerable<T>`，服务端持续推送多条响应，客户端流式接收。SRMP 二进制流 + HTTP 自动兼容 SSE
2. **元数据传递**：在 Token 之外支持任意键值对透传（如 TraceId），不改 IMessage 接口
3. **性能修复**：解决高并发大字符串 Benchmark 失败问题，优化热路径分配
4. **文档完善**：更新 README 和 Doc 文档，突出 RPC 核心亮点

## 2. 用户角色

| 角色 | 说明 | 核心诉求 |
|------|------|---------|
| 后端开发者 | 使用 Remoting 构建微服务 | 流式推送数据、传递链路追踪 ID |
| IoT 设备开发者 | 通过 Remoting 连接设备 | 流式上报/下发数据 |
| 系统架构师 | 评估 RPC 框架选型 | 对标 gRPC，了解差异和优势 |

## 3. 功能需求

### 3.1 流式调用 (Server-Streaming)
- **描述**：Controller Action 可返回 `IAsyncEnumerable<T>`，服务端持续推送多条响应帧，客户端逐条接收直到结束标记
- **用户故事**：作为后端开发者，我希望 Controller 能逐条推送日志/进度数据，以便客户端实时展示而不必等全部完成
- **验收条件**：
  - [ ] TCP SRMP 协议支持 Streaming Flag 和 EndOfStream 标记
  - [ ] Controller 返回 `IAsyncEnumerable<T>` 时自动流式推送
  - [ ] `ApiClient.InvokeStreamAsync<T>()` 返回 `IAsyncEnumerable<T>`，支持 `CancellationToken` 中途取消
  - [ ] HTTP 模式自动兼容 SSE (`text/event-stream`)
  - [ ] 客户端断开连接时服务端自动停止枚举
- **优先级**：Must

### 3.2 元数据/Headers 传递
- **描述**：`ApiClient.Headers` 字典在每次调用时自动透传键值对，服务端可通过 `ControllerContext` 读取
- **用户故事**：作为后端开发者，我希望在 RPC 调用中自动传递 TraceId，以便分布式链路追踪
- **验收条件**：
  - [ ] `ApiClient.Headers` 属性（`IDictionary<String, String?>`）
  - [ ] 每次 `InvokeAsync` 自动注入 Headers 到参数字典
  - [ ] 自动注入当前 `Tracer?.TraceId`（若已配置）
  - [ ] 服务端从 `ControllerContext.Current.Items["Headers"]` 可读取
  - [ ] 不改 `IMessage` 接口，不破坏协议兼容
- **优先级**：Must

### 3.3 高并发性能修复
- **描述**：定位并修复 Benchmark 中 32 并发 2000 字符场景失败问题，优化热路径内存分配
- **用户故事**：作为系统架构师，我希望 Remoting 在高并发大消息场景下稳定运行
- **验收条件**：
  - [ ] Benchmark 32 并发 2000 字符场景通过（不再 N/A）
  - [ ] 热路径分配优化（Encoder 预计算长度、SpanWriter 合并写入）
  - [ ] 前后 TPS 对比无退化
- **优先级**：Must

### 3.4 文档与 README 更新
- **描述**：更新 README 展示 RPC 核心亮点和快速用例，更新 Doc 文档覆盖新功能
- **用户故事**：作为新用户，我希望通过 README 5 分钟内了解 Remoting RPC 的核心能力并跑通第一个示例
- **验收条件**：
  - [ ] README 顶部强化 RPC 核心亮点 + 最简 5 行示例
  - [ ] 新增流式调用章节（含代码示例）
  - [ ] 新增元数据传递小节
  - [ ] Doc 文档更新（SRMP 协议、组件图、时序图、对比表）
  - [ ] 代码示例可复制粘贴运行
- **优先级**：Must

## 4. 非功能需求

- **性能**：流式调用首帧延迟与普通 RPC 一致；后续帧延迟 < 1ms（内网 TCP）；32 并发 2000 字符不失败
- **安全**：流式调用仍需通过 Token 认证（首帧验证，后续帧复用会话）
- **兼容性**：遵循现有框架兼容性（net45~net10.0）；旧客户端可连接新服务端（不发送 Streaming 请求即可）；不改 `IMessage` 等 NewLife.Core 基础接口

## 5. 边界与约束

- **不做什么**：
  - 不实现客户端流式（Client Streaming）、双向流式（Bidirectional Streaming）——仅 Server-Streaming
  - 不引入 Protobuf 等新序列化方案
  - 不改 `IMessage` / `IActionFilter` / 现有负载均衡机制
  - 不新增外部 NuGet 依赖
- **已知限制**：流式调用不支持 UDP（UDP 天然无连接，流式语义不适合）
- **技术债务**：`EnsureOwnedPayload` 在 32 并发下可能存在 ArrayPool 竞争

## 6. 术语表

| 术语 | 定义 |
|------|------|
| SRMP | Simple Remote Message Protocol，NewLife 自研 RPC 协议 |
| SSE | Server-Sent Events，HTTP 单向流式推送标准 |
| Server-Streaming | 服务端流式调用：一次请求，多次响应 |
| Headers | 元数据键值对，在 RPC 调用中透传 |
| SAEA | SocketAsyncEventArgs，.NET 高性能网络 IO 模型 |
