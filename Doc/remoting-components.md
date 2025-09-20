# NewLife.Remoting 组件关系图

本文件说明 NewLife.Remoting 中 RPC 客户端/服务端的核心组件关系。仅聚焦 `ApiClient` / `ApiServer` 与其关键依赖，便于理解架构与扩展点。

```mermaid
flowchart LR
    subgraph Client[Client]
        AC[ApiClient]
        CL[Cluster<br/>ClientSingleCluster / ClientPoolCluster]
        SC[ISocketClient]
        ENC_C[IEncoder<br/>JsonEncoder / HttpEncoder]
    end

    subgraph Server[Server]
        AS[ApiServer]
        Svr[IApiServer<br/>ApiNetServer / ApiHttpServer]
        H[IApiHandler<br/>ApiHandler]
        M[IApiManager<br/>ApiManager]
        ENC_S[IEncoder<br/>JsonEncoder / HttpEncoder]
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

    AS --> Svr --> IM
    AS --> H
    AS --> M
    AS --> ENC_S --> AM --> IM
    AS --> TR
    AS --> LOG
    AS --> CNT

    H -. Execute .-> M
    M -. Find/Register .-> H

    SC -- SendMessage --> Svr
    Svr -- Receive --> SC
```

说明：
- ApiClient
  - 通过 `Cluster` 管理 `ISocketClient` 连接（单连接或者连接池）。
  - 依赖 `IEncoder` 进行请求/响应的编解码，统一使用 `IMessage`/`ApiMessage` 作为跨网络的消息体。
  - 可选注入 `ILog`、`ITracer`、`ICounter` 实现日志、链路追踪、统计。
- ApiServer
  - 通过 `IApiServer`（`ApiNetServer`/`ApiHttpServer`）承载网络会话，处理 `IMessage`。
  - `IApiHandler.Execute` 负责将 `ApiMessage` 路由到具体控制器动作；`IApiManager` 负责控制器/动作的注册与发现。
  - 依赖 `IEncoder` 统一编解码，支持 `UseHttpStatus` 切换 HTTP 返回语义。
- Shared
  - 公共契约位于 `NewLife.*` 基础库，跨客户端/服务端共享。
