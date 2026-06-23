# RPC 重构架构

## 1. 架构概览

本次重构聚焦三项核心改进，不改动现有架构骨架：

```
ApiClient ──[SRMP Streaming]──→ ApiServer ──→ Controller (IAsyncEnumerable<T>)
    │                                │
    ├─ Headers (TraceId) ───────────→ ControllerContext.Items["Headers"]
    │                                │
    └─ [性能优化] Encoder预计算     └─ [性能优化] EnsureOwnedPayload修复
```

### 设计原则
- **最小侵入**：不改 `IMessage`、`IActionFilter`、`ICluster` 等现有接口
- **向后兼容**：旧客户端可连接新服务端（不请求流式即可）；新客户端可连接旧服务端（服务端不理睬 Streaming Flag 则 Fallback）
- **渐进增强**：先 SRMP 二进制流，HTTP 通过 SSE 天然兼容

## 2. 接口设计

### 2.1 流式调用

#### 客户端

```csharp
// ApiClient 新增方法
public IAsyncEnumerable<TResult> InvokeStreamAsync<TResult>(
    String action, Object? args = null, CancellationToken cancellationToken = default);
```

#### 服务端

```csharp
// Controller Action 返回 IAsyncEnumerable<T> 时自动流式推送
public class MyController : ApiController
{
    public async IAsyncEnumerable<String> GetLogs(Int32 count)
    {
        for (var i = 0; i < count; i++)
        {
            await Task.Delay(100);
            yield return $"Log #{i}: {DateTime.Now:HH:mm:ss}";
        }
    }
}
```

#### SRMP 协议扩展

```
首帧:  Flag(Streaming=1) + Seq + Len + [action] + [元数据/args] + [data]
中间帧: Flag(Streaming=1) + Seq + Len + [data]
末帧:  Flag(Streaming=1, EndOfStream=1) + Seq + 0
```

- `Flag` 字节 bit5 标记 Streaming，bit4 标记 EndOfStream
- 客户端收到 EndOfStream 后结束枚举
- 中间帧不带 action（节省带宽）

#### HTTP SSE 兼容

```
HTTP/1.1 200 OK
Content-Type: text/event-stream
Cache-Control: no-cache
Connection: keep-alive

data: {"code":0,"data":"Log #0: 12:00:01"}

data: {"code":0,"data":"Log #1: 12:00:01"}

data: {"code":0,"data":""}

```

- 每个流式块转为一行 `data: {json}\n\n`
- 最后一帧 `data: {"code":0,"data":""}\n\n` 标记结束

### 2.2 Headers 传递

#### ApiClient

```csharp
public class ApiClient : ApiHost, IApiClient
{
    // 新增属性
    public IDictionary<String, String?> Headers { get; set; } = new Dictionary<String, String?>();
    
    // InvokeAsync 自动注入
    // 1. 若 Headers 非空，合并到 args 字典：args["__headers"] = Headers
    // 2. 若 Tracer?.TraceId 不为空，自动加入 Headers["TraceId"]
}
```

#### 服务端提取

```csharp
// ApiServer.Process 中
var headers = args is IDictionary<String, Object?> dic 
    && dic.TryGetValue("__headers", out var h) 
    ? h as IDictionary<String, String?> 
    : null;
ControllerContext.Current.Items["Headers"] = headers;
```

## 3. 技术选型

| 领域 | 选型 | 理由 |
|------|------|------|
| 流式客户端 API | `IAsyncEnumerable<T>` | C# 8.0+ 原生支持，`await foreach` 语法简洁 |
| 流式服务端检测 | `typeof(IAsyncEnumerable<>).IsAssignableFrom(returnType)` | 反射检测，不依赖新接口 |
| SRMP 流式标识 | Flag 位 bit5/bit4 | 复用现有 1 字节 Flag，向下兼容 |
| HTTP 流式协议 | SSE (`text/event-stream`) | 标准协议，浏览器原生支持，curl 可调试 |
| Headers 透传 | 参数字典 `__headers` key | 不改 IMessage，不扩展协议 |
| 性能优化 | SpanWriter 预计算 + ArrayPool 隔离 | 减少热路径分配 |

## 4. 关键设计决策

| 决策点 | 方案 | 备选方案 | 选择理由 |
|--------|------|---------|---------|
| 流式协议 | SRMP Flag 扩展 | 新消息类型 | Flag 扩展最小改动，旧客户端忽略未知 bit |
| Headers 传递 | 参数字典 `__headers` | SRMP 协议 Headers 段 | 不改协议、不改 IMessage，最小实现 |
| SSE 结束标记 | 最后一帧 data 为空 | `event: end` | 与现有 ApiMessage JSON 格式一致 |
| 流式编码器 | `EncoderBase.EncodeStreamChunk` | 独立 `IStreamEncoder` | 复用现有 Encoder 体系 |
| 性能修复 | `EnsureOwnedPayload` Clone 改用独立 ArrayPool | 增大 SAEA buffer | 隔离池竞争，更安全 |

## 5. 风险与缓解

| 风险 | 影响 | 缓解措施 |
|------|------|---------|
| 流式帧乱序 | 客户端收到错序数据 | 复用 Sequence 号，客户端按序交付 |
| 旧客户端连接新服务端 | 旧客户端忽略 Streaming Flag | 仅当客户端显式调用 `InvokeStreamAsync` 时才触发流式 |
| SSE 兼容性 | 旧 HTTP 客户端不理解 SSE | `InvokeStreamAsync` 仅在新 `ApiHttpClient` 路径实现 |
| ArrayPool 隔离增加内存 | 独立池占用更多内存 | 仅在高并发时使用独立池，低并发退化为 Clone |

## 6. 变更文件清单

### 现有文件修改
- `NewLife.Remoting/ApiClient.cs` — 新增 `InvokeStreamAsync`、`Headers`
- `NewLife.Remoting/ApiServer.cs` — `Process` 检测流式、提取 Headers
- `NewLife.Remoting/EncoderBase.cs` — 新增 `EncodeStreamChunk`，优化 `Encode` 分配
- `NewLife.Remoting/JsonEncoder.cs` — 实现流式块编码
- `NewLife.Remoting/HttpEncoder.cs` — SSE 编码
- `NewLife.Remoting/ApiAction.cs` — 新增 `IsStreaming` 属性
- `NewLife.Remoting/IApiHandler.cs` — `ApiHandler.Execute` 支持流式 Action
- `NewLife.Remoting/ApiNetSession.cs` — 流式帧发送
- `NewLife.Remoting/Clients/ClientBase.cs` — `InvokeStreamAsync` 代理

### 新增文件
- 无（所有改动在现有文件中）
