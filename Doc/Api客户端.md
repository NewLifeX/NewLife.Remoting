# Api客户端使用手册

本文档介绍 NewLife.Remoting 中应用客户端基类 `ClientBase` 的使用方法，适用于设备接入、应用对接等场景。

---

## 1. 概述

`ClientBase` 是应用客户端的抽象基类，实现了对接目标平台的登录、心跳、更新和指令下发等场景操作。

### 1.1 典型应用架构

| 架构 | 协议 | 说明 | 示例 |
|------|------|------|------|
| **RPC应用架构** | TCP/UDP | 客户端通过 ApiClient 连接服务端 ApiServer，服务端直接下发指令 | 蚂蚁调度 |
| **HTTP应用架构** | HTTP/HTTPS | 客户端通过 ApiHttpClient 连接 WebApi，服务端通过 WebSocket 下发指令 | ZeroIot 设备接入 |
| **OAuth应用架构** | HTTP/HTTPS | 客户端通过 OAuth 登录获取令牌，后续请求携带令牌 | 星尘 AppClient |

### 1.2 核心功能

- **登录认证**：使用编码和密钥登录服务端，获取令牌
- **心跳保活**：定时上报客户端性能数据，维持在线状态
- **自动更新**：检测并下载更新包，自动升级应用
- **命令下发**：接收并执行服务端下发的命令
- **事件上报**：向服务端推送各类事件信息

---

## 2. 快速开始

### 2.1 创建自定义客户端

继承 `ClientBase` 并实现业务逻辑：

```csharp
public class MyDeviceClient : ClientBase
{
    public MyDeviceClient() : base()
    {
        // 设置接口路径前缀
        SetActions("Device/");
        
        // 启用功能特性
        Features = Features.Login | Features.Logout | Features.Ping | Features.Notify;
    }

    public MyDeviceClient(IClientSetting setting) : base(setting)
    {
        SetActions("Device/");
        Features = Features.Login | Features.Logout | Features.Ping | Features.Notify;
    }
}
```

### 2.2 配置客户端

```csharp
var client = new MyDeviceClient
{
    Server = "http://localhost:5000",  // 服务端地址
    Code = "device001",                 // 设备编码
    Secret = "your_secret_key",         // 设备密钥
    Timeout = 15_000,                   // 超时时间（毫秒）
    Log = XTrace.Log,                   // 日志输出
};
```

### 2.3 启动客户端

```csharp
// 方式一：异步打开（推荐）
// 在网络未就绪之前会反复尝试登录
client.Open();

// 方式二：直接登录
await client.Login("MyApp");
```

### 2.4 关闭客户端

```csharp
// 注销并释放资源
await client.Logout("应用关闭");
client.Dispose();

// 或直接 Dispose（会自动注销）
client.Dispose();
```

---

## 3. 功能特性

通过 `Features` 枚举控制客户端功能：

| 特性 | 说明 |
|------|------|
| `Login` | 登录功能 |
| `Logout` | 注销功能 |
| `Ping` | 心跳功能 |
| `Upgrade` | 自动更新功能 |
| `Notify` | 下行通知（WebSocket长连接） |
| `CommandReply` | 命令响应上报 |
| `PostEvent` | 事件上报功能 |
| `All` | 启用全部功能 |

```csharp
// 启用多个功能
client.Features = Features.Login | Features.Logout | Features.Ping | Features.Notify;

// 启用全部功能
client.Features = Features.All;
```

---

## 4. 登录与认证

### 4.1 基本登录

```csharp
// 登录并获取响应
var response = await client.Login("来源标识");

if (client.Logined)
{
    Console.WriteLine($"登录成功，令牌：{response?.Token}");
}
```

### 4.2 自定义登录请求

重写 `BuildLoginRequest` 方法添加自定义参数：

```csharp
public class MyDeviceClient : ClientBase
{
    public String ProductKey { get; set; }

    public override ILoginRequest BuildLoginRequest()
    {
        var request = base.BuildLoginRequest();
        
        // 添加自定义参数
        if (request is MyLoginRequest myRequest)
        {
            myRequest.ProductKey = ProductKey;
        }
        
        return request;
    }
}
```

### 4.3 登录成功事件

```csharp
client.OnLogined += (sender, e) =>
{
    var request = e.Request;
    var response = e.Response;
    
    Console.WriteLine($"登录成功：{response.Code}");
    
    // 处理服务端下发的编码和密钥（自动注册场景）
    if (!response.Code.IsNullOrEmpty())
    {
        // 保存新的编码和密钥
    }
};
```

### 4.4 密码保护

使用 `IPasswordProvider` 保护密码传输：

```csharp
client.PasswordProvider = new SaltPasswordProvider 
{ 
    Algorithm = "md5", 
    SaltTime = 60 
};
```

---

## 5. 心跳与保活

### 5.1 自动心跳

登录成功后，客户端会自动启动心跳定时器，默认间隔 60 秒。

### 5.2 手动心跳

```csharp
var response = await client.Ping();

if (response != null)
{
    Console.WriteLine($"心跳成功，服务器时间偏移：{client.Span}");
}
```

### 5.3 自定义心跳数据

重写 `BuildPingRequest` 或 `FillPingRequest` 添加自定义数据：

```csharp
public override IPingRequest BuildPingRequest()
{
    var request = base.BuildPingRequest();
    
    // 添加自定义数据
    if (request is MyPingRequest myRequest)
    {
        myRequest.CustomData = GetCustomData();
    }
    
    return request;
}
```

### 5.4 心跳数据说明

默认心跳会上报以下性能数据：

| 字段 | 说明 |
|------|------|
| `Memory` | 内存大小 |
| `AvailableMemory` | 可用内存 |
| `CpuRate` | CPU 占用率 |
| `Temperature` | 温度 |
| `Battery` | 电量 |
| `TotalSize` | 磁盘大小 |
| `AvailableFreeSpace` | 磁盘可用空间 |
| `IP` | 本地 IP 地址 |
| `Uptime` | 运行时间（秒） |
| `Delay` | 网络延迟（毫秒） |

---

## 6. 命令处理

### 6.1 注册命令处理器

```csharp
// 方式一：简单字符串参数
client.RegisterCommand("Reboot", (String? args) =>
{
    Console.WriteLine($"收到重启命令：{args}");
    return "重启成功";
});

// 方式二：异步处理
client.RegisterCommand("UpdateConfig", async (String? args) =>
{
    await UpdateConfigAsync(args);
    return "配置更新成功";
});

// 方式三：完整命令模型
client.RegisterCommand("Execute", (CommandModel model) =>
{
    return new CommandReplyModel
    {
        Id = model.Id,
        Status = CommandStatus.已完成,
        Data = "执行成功"
    };
});

// 方式四：异步 + 取消令牌
client.RegisterCommand("LongTask", async (CommandModel model, CancellationToken ct) =>
{
    await DoLongTaskAsync(model.Argument, ct);
    return new CommandReplyModel
    {
        Id = model.Id,
        Status = CommandStatus.已完成
    };
});
```

### 6.2 命令事件

```csharp
client.Received += (sender, e) =>
{
    var model = e.Model;
    Console.WriteLine($"收到命令：{model.Command}，参数：{model.Argument}");
    
    // 可以修改命令或设置自定义响应
    e.Reply = new CommandReplyModel
    {
        Id = model.Id,
        Status = CommandStatus.已完成,
        Data = "处理完成"
    };
};
```

### 6.3 主动发送命令

```csharp
// 向命令引擎发送命令，触发已注册的处理器
var reply = await client.SendCommand("Reboot", "立即重启");
```

### 6.4 命令状态

| 状态 | 说明 |
|------|------|
| `就绪` | 等待执行 |
| `处理中` | 正在执行 |
| `已完成` | 执行成功 |
| `错误` | 执行失败 |
| `取消` | 已取消 |

---

## 7. 事件上报

### 7.1 上报事件

```csharp
// 上报信息事件
client.WriteInfoEvent("DeviceStart", "设备启动成功");

// 上报错误事件
client.WriteErrorEvent("SensorError", "温度传感器异常");

// 上报自定义类型事件
client.WriteEvent("alert", "HighTemperature", "温度超过阈值：85°C");
```

### 7.2 事件类型

| 类型 | 说明 |
|------|------|
| `info` | 信息事件 |
| `alert` | 告警事件 |
| `error` | 错误事件 |

### 7.3 批量上报

事件会自动缓存并批量上报，默认间隔 60 秒，每批最多 100 条。

```csharp
// 直接批量上报
await client.PostEvents(new EventModel[]
{
    new() { Type = "info", Name = "Event1", Remark = "事件1" },
    new() { Type = "info", Name = "Event2", Remark = "事件2" },
});
```

---

## 8. 自动更新

### 8.1 启用更新功能

```csharp
client.Features |= Features.Upgrade;
```

### 8.2 手动检查更新

```csharp
var info = await client.Upgrade("stable");  // 指定更新通道

if (info != null)
{
    Console.WriteLine($"发现新版本：{info.Version}");
}
```

### 8.3 更新流程

1. 调用服务端接口查询更新信息
2. 下载更新包
3. 校验文件哈希
4. 解压并覆盖文件
5. 执行预安装脚本（可选）
6. 强制更新时自动重启

---

## 9. 远程调用

### 9.1 调用服务端接口

```csharp
// 异步调用
var result = await client.InvokeAsync<MyResponse>("Device/GetInfo", new { id = 123 });

// 同步调用
var result = client.Invoke<MyResponse>("Device/GetInfo", new { id = 123 });

// GET 请求（仅 HTTP）
var result = await client.GetAsync<MyResponse>("Device/GetStatus", new { id = 123 });
```

### 9.2 自动重新登录

当令牌过期（服务端返回 401）时，客户端会自动重新登录并重试请求。

---

## 10. 时间同步

客户端会自动计算与服务器的时间差，通过 `GetNow()` 获取校准后的时间：

```csharp
// 获取基于服务器的当前时间
var serverTime = client.GetNow();

// 时间差（服务器时间 - 客户端时间）
var span = client.Span;

// 网络延迟（毫秒）
var delay = client.Delay;
```

---

## 11. 日志与追踪

### 11.1 配置日志

```csharp
client.Log = XTrace.Log;
```

### 11.2 链路追踪

```csharp
client.Tracer = new DefaultTracer();
```

---

## 12. 客户端设置

实现 `IClientSetting` 接口持久化配置：

```csharp
public class DeviceSetting : IClientSetting
{
    public String Server { get; set; } = "http://localhost:5000";
    public String Code { get; set; } = "";
    public String? Secret { get; set; }

    public void Save()
    {
        // 保存到文件或数据库
        File.WriteAllText("config.json", this.ToJson());
    }
}
```

使用设置：

```csharp
var setting = new DeviceSetting { Server = "http://localhost:5000" };
var client = new MyDeviceClient(setting);
```

---

## 13. 高级用法

### 13.1 自定义接口路径

```csharp
protected override void SetActions(String prefix)
{
    base.SetActions(prefix);
    
    // 添加自定义接口
    Actions[MyFeatures.CustomAction] = prefix + "CustomAction";
}
```

### 13.2 自定义 HTTP 客户端

```csharp
protected override ApiHttpClient CreateHttp(String urls)
{
    var http = base.CreateHttp(urls);
    
    // 自定义配置
    http.Timeout = 30_000;
    http.DefaultUserAgent = "MyApp/1.0";
    
    return http;
}
```

### 13.3 WebSocket 长连接

启用 `Notify` 特性后，客户端会自动建立 WebSocket 长连接接收服务端推送：

```csharp
client.Features |= Features.Notify;
```

---

## 14. 最佳实践

1. **使用依赖注入**：在 ASP.NET Core 中注册为单例服务
2. **配置持久化**：实现 `IClientSetting` 保存自动注册的编码和密钥
3. **日志追踪**：配置 `Log` 和 `Tracer` 便于问题排查
4. **优雅关闭**：调用 `Dispose()` 确保正确注销和释放资源
5. **命令幂等**：命令处理器应支持重复执行
6. **异常处理**：捕获 `ApiException` 处理业务异常

---

## 15. 示例项目

参考 `Samples` 目录下的示例项目：

- **IoTZero**：物联网设备接入示例
- **Demo**：基础功能演示
- **Zero.Desktop**：桌面应用示例
- **ZeroServer**：服务端示例

---

（完）
