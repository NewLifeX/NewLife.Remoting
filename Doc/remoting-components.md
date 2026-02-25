# NewLife.Remoting 组件关系图

本文件说明 NewLife.Remoting 中 RPC 客户端/服务端的核心组件关系。仅聚焦 `ApiClient` / `ApiServer` 与其关键依赖，便于理解架构与扩展点。

```mermaid
flowchart LR
    subgraph Client[Client]
        AC[ApiClient]
        CL[Cluster\nClientSingleCluster | ClientPoolCluster]
        SC[ISocketClient]
        ENC_C[IEncoder\nJsonEncoder | HttpEncoder]
        RP[IRetryPolicy\n(可选)]
    end

    subgraph Server[Server]
        AS[ApiServer]
        Svr[IApiServer\nApiNetServer | ApiHttpServer]
        H[IApiHandler\nApiHandler]
        M[IApiManager\nApiManager]
        ENC_S[IEncoder\nJsonEncoder | HttpEncoder]
    end

    subgraph Shared[Shared Contracts]
        IM[IMessage]
        AM[ApiMessage]
        PK[IPacket]
        LOG[ILog]
        TR[ITracer]
        CNT[ICounter]
    end

    AC --> CL --> SC --> IM
    AC --> ENC_C --> AM --> IM
    AC --> TR
    AC --> LOG
    AC --> CNT
    AC --- RP

    AS --> Svr --> IM
    AS --> H
    AS --> M
    AS --> ENC_S --> AM --> IM
    AS --> TR
    AS --> LOG
    AS --> CNT

    H -.->|Execute| M
    M -.->|Find/Register| H

    SC <-->|SendMessage / Receive| Svr
```

说明：
- ApiClient
  - 通过 `Cluster` 管理 `ISocketClient` 连接（单连接或者连接池）。
  - 依赖 `IEncoder` 进行请求/响应的编解码，统一使用 `IMessage`/`ApiMessage` 作为跨网络的消息体。
  - 可选注入 `ILog`、`ITracer`、`ICounter` 实现日志、链路追踪、统计。
  - 可选注入 `IRetryPolicy` 与 `MaxRetries`，仅当两者有效时对非 401 异常触发有限次重试；401 仍由内置登录后重发处理。
- ApiServer
  - 通过 `IApiServer`（`ApiNetServer`/`ApiHttpServer`）承载网络会话，处理 `IMessage`。
  - `IApiHandler.Execute` 负责将 `ApiMessage` 路由到具体控制器动作；`IApiManager` 负责控制器/动作的注册与发现。
  - 依赖 `IEncoder` 统一编解码，支持 `UseHttpStatus` 切换 HTTP 返回语义。
- Shared
  - 公共契约位于 `NewLife.*` 基础库，跨客户端/服务端共享。


## 内存管理

RPC 流水线采用零拷贝的 `IOwnerPacket` 所有权转移机制，Controller 返回的 `OwnerPacket` 直接挂载到 `IMessage.Payload` 链，由上层 `using IMessage` 级联释放归还 `ArrayPool` 缓冲区。详见 [RPC内存管理设计](RPC内存管理设计.md)。

关键路径：
- `ApiServer.Process`：跟踪响应是否成功创建，仅在未纳入响应时释放 result
- `EncoderBase.Encode`：将 value 挂载到 OwnerPacket 链的 Next 节点
- `ApiNetSession.OnReceive`：`using var rs` 确保 IMessage 在发送后正确释放
- `HttpMessage.Dispose`：级联释放 Header 和 Payload 中的 IOwnerPacket
