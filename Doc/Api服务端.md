# Api服务端开发指南

本文档介绍基于 `NewLife.Remoting.Extensions` 构建 HTTP/WebSocket 设备接入服务端的架构设计与使用方法。

---

## 目录

- [概述](#概述)
- [核心架构](#核心架构)
- [快速开始](#快速开始)
- [核心组件详解](#核心组件详解)
  - [IDeviceService 设备服务接口](#ideviceservice-设备服务接口)
  - [DefaultDeviceService 默认实现](#defaultdeviceservice-默认实现)
  - [BaseController 控制器基类](#basecontroller-控制器基类)
  - [BaseDeviceController 设备控制器](#basedevicecontroller-设备控制器)
  - [TokenService 令牌服务](#tokenservice-令牌服务)
  - [SessionManager 会话管理](#sessionmanager-会话管理)
- [数据模型](#数据模型)
- [WebSocket 长连接](#websocket-长连接)
- [扩展开发](#扩展开发)
- [最佳实践](#最佳实践)
- [常见问题](#常见问题)

---

## 概述

`NewLife.Remoting.Extensions` 提供了一套完整的 ASP.NET Core 设备接入解决方案，支持：

- **设备登录/注销**：支持动态注册、密钥验证、令牌颁发
- **心跳保活**：设备周期性心跳，更新在线状态，自动续期令牌
- **命令下发**：通过 WebSocket 实时下发命令，支持同步等待响应
- **事件上报**：设备批量上报事件到服务端
- **升级检查**：设备查询可用升级版本
- **会话管理**：管理 WebSocket 长连接，支持跨进程消息分发

### 适用场景

| 场景 | 说明 |
|------|------|
| IoT 设备接入 | 智能设备通过 HTTP 登录，WebSocket 接收指令 |
| 边缘节点管理 | 边缘计算节点向中心平台注册、心跳、接收任务 |
| 客户端应用 | 桌面/移动应用与服务端保持长连接，接收推送 |
| 分布式调度 | 任务调度节点登录、领取任务、上报结果 |

---

## 核心架构

```
┌─────────────────────────────────────────────────────────────────┐
│                         客户端 (设备/应用)                        │
│  ClientBase / ApiHttpClient                                      │
└─────────────────┬───────────────────────────────┬───────────────┘
                  │ HTTP (Login/Ping/Upgrade)     │ WebSocket (Notify)
                  ▼                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                      ASP.NET Core WebApi                         │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │              BaseDeviceController                        │    │
│  │  - Login()      登录接口                                 │    │
│  │  - Logout()     注销接口                                 │    │
│  │  - Ping()       心跳接口                                 │    │
│  │  - Upgrade()    升级检查                                 │    │
│  │  - Notify()     WebSocket 长连接                         │    │
│  │  - SendCommand() 发送命令                                │    │
│  │  - PostEvents() 上报事件                                 │    │
│  └────────────────────────┬────────────────────────────────┘    │
│                           │                                      │
│  ┌────────────────────────▼────────────────────────────────┐    │
│  │               IDeviceService / IDeviceService2           │    │
│  │  - Login()         设备登录验证                          │    │
│  │  - Authorize()     密钥验证                              │    │
│  │  - Register()      动态注册                              │    │
│  │  - Ping()          心跳处理                              │    │
│  │  - SendCommand()   命令下发                              │    │
│  │  - WriteHistory()  写历史日志                            │    │
│  └────────────────────────┬────────────────────────────────┘    │
│                           │                                      │
│  ┌────────────────────────▼────────────────────────────────┐    │
│  │                   支撑服务层                              │    │
│  │  ┌──────────────┐ ┌──────────────┐ ┌──────────────────┐ │    │
│  │  │ TokenService │ │SessionManager│ │ IPasswordProvider│ │    │
│  │  │  令牌颁发/验证 │ │  会话管理     │ │   密钥验证        │ │    │
│  │  └──────────────┘ └──────────────┘ └──────────────────┘ │    │
│  └─────────────────────────────────────────────────────────┘    │
│                           │                                      │
│  ┌────────────────────────▼────────────────────────────────┐    │
│  │                    数据/缓存层                            │    │
│  │  ┌──────────────┐ ┌──────────────┐ ┌──────────────────┐ │    │
│  │  │   XCode ORM  │ │  ICache      │ │  ITracer         │ │    │
│  │  │  设备/在线表  │ │  Redis缓存    │ │  链路追踪        │ │    │
│  │  └──────────────┘ └──────────────┘ └──────────────────┘ │    │
│  └─────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────┘
```

---

## 快速开始

### 1. 安装 NuGet 包

```bash
dotnet add package NewLife.Remoting.Extensions
```

### 2. 配置服务 (Program.cs)

```csharp
var builder = WebApplication.CreateBuilder(args);

// 添加控制器
builder.Services.AddControllers();

// 注册令牌配置
builder.Services.AddSingleton<ITokenSetting>(new TokenSetting
{
    TokenSecret = "HS256:your-secret-key-at-least-32-characters",
    TokenExpire = 7200  // 令牌有效期（秒）
});

// 注册核心服务
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddSingleton<ISessionManager, SessionManager>();
builder.Services.AddSingleton<IPasswordProvider, SaltPasswordProvider>();
builder.Services.AddSingleton<ICacheProvider, MemoryCacheProvider>();

// 注册设备服务（需要自定义实现）
builder.Services.AddSingleton<IDeviceService, MyDeviceService>();

var app = builder.Build();

// 启用 WebSocket
app.UseWebSockets();

app.MapControllers();
app.Run();
```

### 3. 实现设备控制器

```csharp
[ApiController]
[Route("api/[controller]")]
public class DeviceController : BaseDeviceController
{
    public DeviceController(IServiceProvider serviceProvider) : base(serviceProvider) { }
    
    // 可以重写基类方法添加自定义逻辑
    public override ILoginResponse Login([FromBody] ILoginRequest request)
    {
        // 自定义登录前逻辑
        var response = base.Login(request);
        // 自定义登录后逻辑
        return response;
    }
}
```

### 4. 实现设备服务

```csharp
public class MyDeviceService : DefaultDeviceService<Device, DeviceOnline>
{
    public MyDeviceService(
        ISessionManager sessionManager,
        IPasswordProvider passwordProvider,
        ICacheProvider cacheProvider,
        IServiceProvider serviceProvider)
        : base(sessionManager, passwordProvider, cacheProvider, serviceProvider)
    {
        Name = "Device";  // 服务名称，用于日志和缓存键前缀
    }
    
    // 重写方法实现自定义业务逻辑
    protected override void OnRegister(DeviceContext context, ILoginRequest request)
    {
        if (context.Device is Device device)
        {
            // 填充设备信息
            if (request is ILoginRequest2 req)
            {
                device.IP = req.IP;
                device.Version = req.Version;
            }
        }
        base.OnRegister(context, request);
    }
}
```

---

## 核心组件详解

### IDeviceService 设备服务接口

`IDeviceService` 定义了设备服务的核心能力：

```csharp
public interface IDeviceService
{
    // 查找设备
    IDeviceModel? QueryDevice(String code);
    
    // 设备登录
    ILoginResponse Login(DeviceContext context, ILoginRequest request, String source);
    
    // 设备注销
    IOnlineModel? Logout(DeviceContext context, String? reason, String source);
    
    // 设备心跳
    IPingResponse Ping(DeviceContext context, IPingRequest? request, IPingResponse? response = null);
    
    // 设置在线状态
    void SetOnline(DeviceContext context, Boolean online);
    
    // 发送命令（内部）
    Task<Int32> SendCommand(IDeviceModel device, CommandModel command, CancellationToken ct = default);
    
    // 发送命令（外部平台调用）
    Task<CommandReplyModel?> SendCommand(DeviceContext context, CommandInModel model, CancellationToken ct = default);
    
    // 命令响应
    Int32 CommandReply(DeviceContext context, CommandReplyModel model);
    
    // 上报事件
    Int32 PostEvents(DeviceContext context, EventModel[] events);
    
    // 升级检查
    IUpgradeInfo? Upgrade(DeviceContext context, String? channel);
    
    // 写历史日志
    void WriteHistory(DeviceContext context, String action, Boolean success, String remark);
}
```

`IDeviceService2` 扩展接口提供更细粒度的控制：

```csharp
public interface IDeviceService2 : IDeviceService
{
    // 验证设备合法性
    Boolean Authorize(DeviceContext context, ILoginRequest request);
    
    // 自动注册设备
    IDeviceModel Register(DeviceContext context, ILoginRequest request);
    
    // 登录后处理
    void OnLogin(DeviceContext context, ILoginRequest request);
    
    // 获取设备（带缓存）
    IDeviceModel? GetDevice(String code);
    
    // 心跳处理
    IOnlineModel OnPing(DeviceContext context, IPingRequest? request);
    
    // 在线对象管理
    IOnlineModel? QueryOnline(String sessionId);
    IOnlineModel? GetOnline(DeviceContext context);
    IOnlineModel CreateOnline(DeviceContext context);
    Int32 RemoveOnline(DeviceContext context);
    
    // 获取下行命令
    CommandModel[] AcquireCommands(DeviceContext context);
}
```

### DefaultDeviceService 默认实现

`DefaultDeviceService<TDevice, TOnline>` 提供了完整的默认实现：

#### 登录流程

```
Login()
  ├── QueryDevice() - 查找设备
  ├── Authorize() - 验证密钥
  │     ├── 密钥为空时跳过验证
  │     ├── 明文匹配
  │     └── PasswordProvider 加密验证
  ├── Register() - 动态注册（验证失败时）
  │     ├── OnRegister() - 填充并保存设备
  │     └── 生成随机密钥
  ├── OnLogin() - 登录后处理
  │     ├── GetOnline/CreateOnline - 创建在线记录
  │     └── WriteHistory - 写登录日志
  └── 返回 LoginResponse
```

#### 心跳流程

```
Ping()
  ├── OnPing() - 更新在线信息
  │     ├── GetOnline/CreateOnline
  │     └── Save() - 保存心跳数据
  ├── AcquireCommands() - 获取待下发命令
  └── 返回 PingResponse（含 Period、Commands、NewServer）
```

#### 命令下发流程

```
SendCommand()
  ├── WriteHistory() - 记录发送日志
  ├── SessionManager.PublishAsync() - 发布到事件总线
  │     ├── 进程内：直接调用会话
  │     └── 跨进程：通过 Redis 队列
  └── 等待响应（可选）
      ├── CacheProvider.GetQueue() - 获取响应队列
      └── TakeOneAsync() - 等待超时
```

### BaseController 控制器基类

`BaseController` 提供统一的令牌验证与异常处理：

#### 核心功能

1. **令牌解析**：从请求头/查询参数/Cookie 提取 Token
2. **JWT 验证**：解码验证令牌，提取 Subject 和 ClientId
3. **设备上下文**：通过对象池管理 `DeviceContext`，高效复用
4. **异常处理**：统一返回 `{ code, message, traceId }` 格式
5. **Claims 映射**：将 JWT 信息映射到 `HttpContext.User`

#### 使用示例

```csharp
public class MyController : BaseController
{
    public MyController(IServiceProvider sp) : base(sp) { }
    
    [HttpGet("info")]
    public Object GetInfo()
    {
        // 访问设备上下文
        var code = Context.Code;
        var device = Context.Device;
        var clientId = Context.ClientId;
        
        return new { code, device?.Name, clientId };
    }
    
    // 自定义鉴权逻辑
    protected override Boolean OnAuthorize(String token, DeviceContext context)
    {
        // 调用基类验证
        if (!base.OnAuthorize(token, context)) return false;
        
        // 自定义验证逻辑
        if (context.Device?.Enable != true) return false;
        
        return true;
    }
}
```

### BaseDeviceController 设备控制器

`BaseDeviceController` 提供完整的设备接入 API：

| 接口 | 方法 | 说明 | 认证 |
|------|------|------|------|
| `Login` | POST | 设备登录，返回令牌 | 匿名 |
| `Logout` | GET/POST | 设备注销 | 需认证 |
| `Ping` | GET/POST | 心跳保活，更新状态 | 需认证 |
| `Upgrade` | GET/POST | 检查升级版本 | 需认证 |
| `Notify` | GET (WebSocket) | 建立长连接 | 需认证 |
| `CommandReply` | POST | 命令执行结果回复 | 需认证 |
| `SendCommand` | POST | 向设备发送命令 | 匿名* |
| `PostEvents` | POST | 批量上报事件 | 需认证 |

> *SendCommand 为平台级接口，使用独立的令牌验证

### TokenService 令牌服务

`TokenService` 负责 JWT 令牌的颁发与验证：

```csharp
public interface ITokenService
{
    // 颁发令牌
    TokenModel IssueToken(String name, String? id = null);
    
    // 解码验证令牌
    (JwtBuilder, Exception?) DecodeToken(String token);
}
```

#### 配置说明

```csharp
public interface ITokenSetting
{
    // 令牌密钥，格式：算法:密钥，如 HS256:your-secret-key
    String TokenSecret { get; set; }
    
    // 令牌有效期（秒），默认 7200
    Int32 TokenExpire { get; set; }
}
```

#### 令牌结构

```json
{
  "iss": "AppName",          // 颁发者（应用名）
  "sub": "device001",        // 主题（设备编码）
  "jti": "client001",        // 令牌ID（客户端标识）
  "exp": 1735689600          // 过期时间
}
```

### SessionManager 会话管理

`SessionManager` 管理 WebSocket 长连接会话：

```csharp
public interface ISessionManager
{
    // 添加会话
    void Add(ICommandSession session);
    
    // 删除会话
    void Remove(ICommandSession session);
    
    // 获取会话
    ICommandSession? Get(String key);
    
    // 发布消息到会话
    Task<Int32> PublishAsync(String code, CommandModel command, String? message, CancellationToken ct);
}
```

#### 消息分发模式

```
单机模式：
  PublishAsync() → EventBus<String> → OnMessage() → Session.HandleAsync()

集群模式（Redis）：
  PublishAsync() → Redis Stream → 各节点订阅 → OnMessage() → Session.HandleAsync()
```

#### 会话清理

- 定时器周期（默认10秒）检查不活跃会话
- 会话销毁时自动从管理器移除
- 进程退出时关闭所有会话

---

## 数据模型

### DeviceContext 设备上下文

```csharp
public class DeviceContext : IExtend
{
    // 设备编码
    public String? Code { get; set; }
    
    // 设备对象
    public IDeviceModel? Device { get; set; }
    
    // 在线信息
    public IOnlineModel? Online { get; set; }
    
    // 访问令牌
    public String? Token { get; set; }
    
    // 客户端标识
    public String? ClientId { get; set; }
    
    // 用户IP
    public String? UserHost { get; set; }
    
    // 扩展数据
    public IDictionary<String, Object?> Items { get; }
    
    // 索引器访问扩展数据
    public Object? this[String key] { get; set; }
}
```

### 登录请求/响应

```csharp
// 登录请求
public class LoginRequest : ILoginRequest, ILoginRequest2
{
    public String? Code { get; set; }      // 设备编码
    public String? Secret { get; set; }    // 密钥
    public String? ClientId { get; set; }  // 客户端标识
    public String? Version { get; set; }   // 版本号
    public String? IP { get; set; }        // 本地IP
    public String? Macs { get; set; }      // MAC地址
    public String? UUID { get; set; }      // 唯一标识
    public Int64 Time { get; set; }        // 本地时间（UTC毫秒）
    public Int64 Compile { get; set; }     // 编译时间
}

// 登录响应
public class LoginResponse : ILoginResponse
{
    public String? Code { get; set; }      // 下发的编码（动态注册）
    public String? Secret { get; set; }    // 下发的密钥
    public String? Name { get; set; }      // 设备名称
    public String? Token { get; set; }     // 访问令牌
    public Int32 Expire { get; set; }      // 令牌有效期（秒）
    public Int64 Time { get; set; }        // 请求时间（回显）
    public Int64 ServerTime { get; set; }  // 服务器时间（UTC毫秒）
}
```

### 心跳请求/响应

```csharp
// 心跳请求
public class PingRequest : IPingRequest, IPingRequest2
{
    public UInt64 Memory { get; set; }          // 内存大小
    public UInt64 AvailableMemory { get; set; } // 可用内存
    public Double CpuRate { get; set; }         // CPU使用率
    public Double Temperature { get; set; }     // 温度
    public String? IP { get; set; }             // 本地IP
    public Int32 Uptime { get; set; }           // 开机时长（秒）
    public Int64 Time { get; set; }             // 本地时间
    public Int32 Delay { get; set; }            // 网络延迟（毫秒）
}

// 心跳响应
public class PingResponse : IPingResponse2
{
    public Int64 Time { get; set; }             // 请求时间（回显）
    public Int64 ServerTime { get; set; }       // 服务器时间
    public Int32 Period { get; set; }           // 心跳周期（秒）
    public String? Token { get; set; }          // 新令牌（续期）
    public String? NewServer { get; set; }      // 新服务器地址（迁移）
    public CommandModel[]? Commands { get; set; } // 下发命令
}
```

### 命令模型

```csharp
// 命令
public class CommandModel
{
    public Int64 Id { get; set; }           // 命令ID
    public String Command { get; set; }     // 命令名称
    public String? Argument { get; set; }   // 命令参数（JSON）
    public DateTime StartTime { get; set; } // 开始时间
    public DateTime Expire { get; set; }    // 过期时间
    public String? TraceId { get; set; }    // 追踪ID
}

// 命令响应
public class CommandReplyModel
{
    public Int64 Id { get; set; }           // 命令ID
    public CommandStatus Status { get; set; } // 执行状态
    public String? Data { get; set; }       // 返回数据
}

// 命令状态
public enum CommandStatus
{
    就绪 = 0,
    处理中 = 1,
    已完成 = 2,
    取消 = 3,
    错误 = 4
}
```

### 事件模型

```csharp
public class EventModel
{
    public Int64 Time { get; set; }     // 发生时间（UTC毫秒）
    public String? Type { get; set; }   // 事件类型（info/alert/error）
    public String? Name { get; set; }   // 事件名称
    public String? Remark { get; set; } // 事件内容
}
```

---

## WebSocket 长连接

### 建立连接

客户端通过 `GET /Device/Notify` 建立 WebSocket 连接：

```
GET /Device/Notify HTTP/1.1
Upgrade: websocket
Connection: Upgrade
Authorization: Bearer <token>
```

### 消息格式

#### 心跳消息
```
客户端 → 服务端: "Ping"
服务端 → 客户端: "Pong"
```

#### 命令下发
```json
{
    "Id": 123,
    "Command": "restart",
    "Argument": "{\"delay\":5}",
    "StartTime": "2024-01-01T00:00:00",
    "Expire": "2024-01-01T01:00:00",
    "TraceId": "trace123"
}
```

### WsCommandSession

`WsCommandSession` 管理单个 WebSocket 连接：

```csharp
public class WsCommandSession : CommandSession, IEventHandler<IPacket>
{
    // 连接是否活跃
    public override Boolean Active { get; }
    
    // 数据分发器（EventHub 场景）
    public IEventHandler<IPacket>? Dispatcher { get; set; }
    
    // 处理服务端下发的命令
    public override Task HandleAsync(CommandModel command, String? message, CancellationToken ct);
    
    // 发送文本消息
    public Task SendAsync(String message, CancellationToken ct = default);
    
    // 发送二进制数据
    public Task SendAsync(IPacket data, CancellationToken ct = default);
    
    // 阻塞等待连接结束
    public virtual Task WaitAsync(HttpContext context, ISpan? span, CancellationToken ct);
}
```

---

## 扩展开发

### 自定义设备服务

```csharp
public class MyDeviceService : DefaultDeviceService<Device, DeviceOnline>
{
    public MyDeviceService(...) : base(...) { }
    
    // 自定义登录验证
    public override Boolean Authorize(DeviceContext context, ILoginRequest request)
    {
        // 调用基类验证
        if (!base.Authorize(context, request)) return false;
        
        // 自定义验证：检查设备是否在白名单
        var device = context.Device as Device;
        if (device?.AllowLogin != true)
        {
            WriteHistory(context, "登录拒绝", false, "设备不在白名单");
            return false;
        }
        
        return true;
    }
    
    // 自定义注册逻辑
    protected override void OnRegister(DeviceContext context, ILoginRequest request)
    {
        if (context.Device is Device device && request is ILoginRequest2 req)
        {
            device.IP = req.IP;
            device.Version = req.Version;
            device.Macs = req.Macs;
            device.UUID = req.UUID;
            device.CreateTime = DateTime.Now;
        }
        base.OnRegister(context, request);
    }
    
    // 自定义升级检查
    public override IUpgradeInfo? Upgrade(DeviceContext context, String? channel)
    {
        var device = context.Device as Device;
        if (device == null) return null;
        
        // 根据设备版本和通道查询可用升级
        var upgrade = DeviceUpgrade.FindLatest(device.ProductCode, channel);
        if (upgrade == null) return null;
        
        return new UpgradeInfo
        {
            Version = upgrade.Version,
            Source = upgrade.DownloadUrl,
            FileHash = upgrade.FileHash,
            Force = upgrade.Force
        };
    }
    
    // 自定义命令获取
    public override CommandModel[] AcquireCommands(DeviceContext context)
    {
        var device = context.Device;
        if (device == null) return [];
        
        // 从数据库获取待执行命令
        var list = DeviceCommand.FindAllByDeviceId(device.Id)
            .Where(e => e.Status == CommandStatus.就绪)
            .Select(e => new CommandModel
            {
                Id = e.Id,
                Command = e.Command,
                Argument = e.Argument,
                StartTime = e.StartTime,
                Expire = e.ExpireTime
            })
            .ToArray();
        
        return list;
    }
}
```

### 自定义控制器

```csharp
[ApiController]
[Route("api/[controller]")]
public class MyDeviceController : BaseDeviceController
{
    private readonly ILogger<MyDeviceController> _logger;
    
    public MyDeviceController(IServiceProvider sp, ILogger<MyDeviceController> logger) 
        : base(sp)
    {
        _logger = logger;
    }
    
    // 重写登录接口
    public override ILoginResponse Login([FromBody] ILoginRequest request)
    {
        _logger.LogInformation("设备登录: {Code}", request.Code);
        
        var response = base.Login(request);
        
        // 登录成功后的额外处理
        if (Context.Device != null)
        {
            // 发送欢迎消息、同步配置等
        }
        
        return response;
    }
    
    // 添加自定义接口
    [HttpPost("config")]
    public Object GetConfig()
    {
        var device = Context.Device;
        if (device == null) return new { code = 401, message = "未登录" };
        
        // 返回设备配置
        return new
        {
            code = 0,
            data = new
            {
                heartbeat = 60,
                logLevel = "Info",
                features = new[] { "upgrade", "command" }
            }
        };
    }
    
    // 添加数据上报接口
    [HttpPost("data")]
    public Object PostData([FromBody] DataModel data)
    {
        var device = Context.Device;
        if (device == null) return new { code = 401, message = "未登录" };
        
        // 处理上报数据
        // ...
        
        WriteLog("数据上报", true, $"收到 {data.Items?.Length ?? 0} 条数据");
        
        return new { code = 0 };
    }
}
```

### 自定义令牌服务

```csharp
public class MyTokenService : TokenService
{
    private readonly IMyUserService _userService;
    
    public MyTokenService(ITokenSetting setting, ITracer tracer, IMyUserService userService)
        : base(setting, tracer)
    {
        _userService = userService;
    }
    
    // 自定义令牌内容
    public override TokenModel IssueToken(String name, String? id = null)
    {
        var token = base.IssueToken(name, id);
        
        // 可以在令牌中添加自定义 Claims
        // 或记录令牌颁发日志
        
        return token;
    }
    
    // 自定义验证逻辑
    public override (JwtBuilder, Exception?) DecodeToken(String token)
    {
        var (jwt, ex) = base.DecodeToken(token);
        
        if (ex == null && jwt != null)
        {
            // 额外验证：检查用户是否被禁用
            var user = _userService.GetUser(jwt.Subject);
            if (user?.Enabled != true)
            {
                ex = new ApiException(ApiCode.Forbidden, "用户已被禁用");
            }
        }
        
        return (jwt, ex);
    }
}
```

---

## 最佳实践

### 1. 性能优化

```csharp
// 使用缓存减少数据库查询
public override IDeviceModel? GetDevice(String code)
{
    // 默认实现已包含缓存，缓存时间60秒
    return base.GetDevice(code);
}

// 批量操作使用事务
public override Int32 PostEvents(DeviceContext context, EventModel[] events)
{
    if (events.Length > 100)
    {
        // 分批处理大量数据
        var batches = events.Chunk(100);
        var total = 0;
        foreach (var batch in batches)
        {
            total += base.PostEvents(context, batch);
        }
        return total;
    }
    return base.PostEvents(context, events);
}
```

### 2. 异常处理

```csharp
// 使用 ApiException 返回业务错误
if (device == null)
    throw new ApiException(ApiCode.NotFound, "设备不存在");

if (!device.Enable)
    throw new ApiException(ApiCode.Forbidden, "设备已禁用");

// 控制器基类会自动处理异常，返回统一格式
// { "code": 404, "message": "设备不存在", "traceId": "xxx" }
```

### 3. 日志追踪

```csharp
// 使用 WriteHistory 记录关键操作
WriteHistory(context, "配置下发", true, $"下发配置: {config.ToJson()}");

// 使用 ITracer 进行链路追踪
using var span = _tracer?.NewSpan("ProcessData", new { deviceCode, count });
try
{
    // 业务逻辑
}
catch (Exception ex)
{
    span?.SetError(ex, null);
    throw;
}
```

### 4. 安全建议

```csharp
// 1. 使用强密钥
TokenSecret = "HS256:your-very-long-secret-key-at-least-32-characters"

// 2. 合理设置令牌有效期
TokenExpire = 7200  // 2小时

// 3. 验证客户端信息一致性
public override Boolean Authorize(DeviceContext context, ILoginRequest request)
{
    if (!base.Authorize(context, request)) return false;
    
    // 验证 UUID 是否变化（防止配置拷贝）
    if (request is ILoginRequest2 req && context.Device is Device device)
    {
        if (!device.UUID.IsNullOrEmpty() && device.UUID != req.UUID)
        {
            WriteHistory(context, "UUID异常", false, $"UUID变化: {device.UUID} → {req.UUID}");
            return false;
        }
    }
    
    return true;
}

// 4. 敏感操作记录日志
WriteHistory(context, "密钥重置", true, $"管理员: {operatorName}");
```

---

## 常见问题

### Q: 如何实现集群部署？

A: 配置 Redis 缓存提供者，`SessionManager` 会自动使用 Redis 事件总线进行跨进程消息分发：

```csharp
// 使用 Redis 缓存
services.AddSingleton<ICacheProvider>(sp =>
{
    var redis = new FullRedis("server=127.0.0.1:6379;db=1");
    return new RedisCacheProvider(redis);
});
```

### Q: 如何处理大量设备同时登录？

A: 
1. 使用对象池（`DeviceContext` 已默认池化）
2. 批量写入数据库
3. 使用缓存减少查询
4. 考虑限流策略

### Q: 如何实现设备分组/多租户？

A: 在设备模型中添加租户字段，重写 `QueryDevice` 方法加入租户过滤：

```csharp
public override IDeviceModel? QueryDevice(String code)
{
    var tenantId = GetCurrentTenantId();
    return Device.FindByCodeAndTenant(code, tenantId);
}
```

### Q: 心跳间隔如何配置？

A: 通过设备模型的 `Period` 属性返回，客户端会使用服务端下发的心跳周期：

```csharp
// 在 IDeviceModel2 实现中
public Int32 Period => 60;  // 60秒心跳
```

### Q: 如何实现自定义协议？

A: 继承 `BaseController`，自行处理请求和响应，但仍可复用令牌验证和上下文管理：

```csharp
public class CustomController : BaseController
{
    [HttpPost("custom")]
    public async Task<IActionResult> HandleCustomProtocol()
    {
        // 读取原始请求体
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        
        // 自定义解析和处理
        var result = ProcessCustomProtocol(body);
        
        return Content(result, "application/octet-stream");
    }
}
```

---

## 相关文档

- [README.md](../Readme.MD) - 项目总览
- [SRMP.md](SRMP.md) - SRMP 协议说明
- [NewLife.Core 文档](https://newlifex.com/core)
- [NewLife.XCode 文档](https://newlifex.com/xcode)

---

## 版本历史

| 版本 | 日期 | 说明 |
|------|------|------|
| 1.0 | 2024-01 | 初始版本 |

---

*文档由 NewLife 团队维护，如有问题欢迎反馈。*
