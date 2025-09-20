# NewLife.Remoting 关键时序图

本文件给出典型调用与处理的时序，包括：客户端发起 RPC、401 触发登录重试、服务端处理请求的完整链路。

## 1. 客户端发起 RPC 调用（成功）

```mermaid
sequenceDiagram
    autonumber
    participant App as Application
    participant AC as ApiClient
    participant CL as Cluster
    participant SC as ISocketClient
    participant ENC as IEncoder
    participant Svr as IApiServer

    App->>AC: InvokeAsync(action, args)
    AC->>AC: Open()
    AC->>CL: Get()
    CL-->>AC: ISocketClient
    AC->>ENC: CreateRequest(action, args)
    ENC-->>AC: IMessage(req)
    AC->>SC: SendMessageAsync(req)
    SC->>Svr: req
    Svr-->>SC: rsp
    SC-->>AC: IMessage(rsp)
    AC->>ENC: Decode(rsp)
    ENC-->>AC: ApiMessage(code,data)
    AC-->>App: TResult
    AC->>CL: Return(client)
```

## 2. 客户端 401 触发登录后重发

```mermaid
sequenceDiagram
    autonumber
    participant App as Application
    participant AC as ApiClient
    participant CL as Cluster
    participant SC as ISocketClient
    participant ENC as IEncoder
    participant Svr as IApiServer

    App->>AC: InvokeAsync(action, args)
    AC->>CL: Get()
    AC->>ENC: CreateRequest()
    AC->>SC: SendMessageAsync()
    SC->>Svr: req
    Svr-->>SC: rsp(401)
    SC-->>AC: IMessage(rsp)
    AC->>ENC: Decode(rsp)
    ENC-->>AC: ApiMessage(Unauthorized)
    AC->>AC: catch ApiException(401)
    AC->>AC: OnLoginAsync(force=true)
    AC->>SC: 登录动作（实现方自定义）
    AC->>ENC: CreateRequest()
    AC->>SC: SendMessageAsync()
    SC->>Svr: req
    Svr-->>SC: rsp(200)
    SC-->>AC: IMessage(rsp)
    AC->>ENC: Decode(rsp)
    ENC-->>AC: ApiMessage(Ok, data)
    AC-->>App: TResult
    AC->>CL: Return(client)
```

## 3. 服务端处理请求

```mermaid
sequenceDiagram
    autonumber
    participant SC as ISocketClient
    participant Svr as IApiServer
    participant AS as ApiServer
    participant ENC as IEncoder
    participant H as IApiHandler
    participant M as IApiManager

    SC->>Svr: IMessage(req)
    Svr->>AS: Process(session, msg, sp)
    AS->>ENC: Decode(msg)
    ENC-->>AS: ApiMessage(action,args)
    AS->>AS: Tracer.NewSpan("rps:"+action)
    AS->>AS: Received?.Invoke(...)
    AS->>H: Execute(session, action, args, msg, sp)
    H->>M: Find(action)/CreateController
    M-->>H: ApiAction/Controller
    H-->>AS: result
    AS->>ENC: CreateResponse(msg, action, code, result)
    ENC-->>AS: IMessage(rsp)
    AS-->>Svr: rsp
```

备注：
- 单向调用：`msg.OneWay == true` 时，服务端 `Process` 直接返回 `null`，不应答。
- 慢追踪：客户端 `InvokeWithClientAsync` 与服务端 `Process` 均在 `finally` 记录超过 `SlowTrace` 的调用/处理日志。
