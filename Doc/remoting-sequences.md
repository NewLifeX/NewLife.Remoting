# NewLife.Remoting �ؼ�ʱ��ͼ

���ļ��������͵����봦���ʱ�򣬰������ͻ��˷��� RPC��401 ������¼���ԡ�����˴��������������·��

## 1. �ͻ��˷��� RPC ���ã��ɹ���

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

## 2. �ͻ��� 401 ������¼���ط�

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
    AC->>SC: ��¼������ʵ�ַ��Զ��壩
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

## 3. ����˴�������

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

��ע��
- ������ã�`msg.OneWay == true` ʱ������� `Process` ֱ�ӷ��� `null`����Ӧ��
- ��׷�٣��ͻ��� `InvokeWithClientAsync` ������ `Process` ���� `finally` ��¼���� `SlowTrace` �ĵ���/������־��
