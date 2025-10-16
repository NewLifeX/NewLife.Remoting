# ClientBase 使用说明

## 适用范围
- 运行时：.NET Framework 4.5+ / .NET Standard 2.0+ / .NET 5~9
- 命名空间：`NewLife.Remoting.Clients`
- 类型：`ClientBase`（抽象基类）

## 概述
- `ClientBase` 是应用层客户端基类，封装登录、心跳、下行通知、命令执行、事件上报与在线升级。
- 同时支持基于 `ApiClient`（TCP/UDP）与 `ApiHttpClient`（HTTP/HTTPS + WebSocket）的通信模型。
- 内置自动重登、时间校准与失败重试队列；不改变对外 API 的同时简化典型业务接入。

## 通信模型
- RPC：`ApiClient` 长连接直连服务端，登录后服务端可直接下发命令。
- HTTP：`ApiHttpClient` 调用 REST 接口，支持通过 WebSocket 接收下行通知。
- OAuth：在 HTTP 模型上叠加 OAuth 登录，获得令牌并随每次请求携带。

## 快速开始
1. 定义客户端派生类（最小实现）
```csharp
using NewLife.Remoting;
using NewLife.Remoting.Clients;

// 依据服务端接口前缀，自定义动作路径映射
public sealed class MyClient : ClientBase
{
    public MyClient() : base() { InitFeatures(); }
    public MyClient(IClientSetting setting) : base(setting) { InitFeatures(); }

    private void InitFeatures()
    {
        // 默认已启用 Login/Logout/Ping，可按需追加
        Features |= Features.Notify | Features.Upgrade | Features.CommandReply | Features.PostEvent;

        // 可覆盖动作前缀；与服务端控制器/路由保持一致
        SetActions("Device/");
    }
}
```

2. 配置与使用
```csharp
using NewLife.Security;
using NewLife.Log;
using NewLife.Remoting.Models;

var setting = new MySetting // 亦可实现 IClientSetting 并持久化保存
{
    Server = "https://api.example.com",
    Code = "dev-001",
    Secret = "secret"
};

var client = new MyClient(setting)
{
    Log = XTrace.Log,                          // 可选：注入日志
    PasswordProvider = new SaltPasswordProvider{ Algorithm = "md5", SaltTime = 60 } // 可选：保护密钥传输
};

client.OnLogined += (s,e) => XTrace.WriteLine("Logined: {0}", e.Response?.Token);
client.Received += (s,e) => XTrace.WriteLine("Command: {0}", e.Model?.Command);

// 方案A：自动重试登录
client.Open();

// 方案B：显式登录
await client.Login("manual");

// 统一远程调用（自动判断 HTTP GET/POST、自动重登一次）
var ping = await client.InvokeAsync<IPingResponse>("Device/Ping", new PingRequest());

// 注销（或 using/Dispose）。Dispose 内部会尝试 Logout
await client.Logout("bye");
```

## 常用属性/配置项
- 连接与鉴权
  - `Server`：服务端地址，支持多地址逗号分隔（客户端负载均衡）。
  - `Code`/`Secret`：客户端编码与密钥；支持自动注册时服务端下发新值并保存到 `IClientSetting`。
  - `PasswordProvider`：可选，用于对 `Secret` 做哈希保护传输（建议 `SaltPasswordProvider`）。
- 调用与序列化
  - `Timeout`：调用超时时间（毫秒），默认 15000。
  - `JsonHost`：序列化提供者，默认 `JsonHelper.Default`。
  - `ServiceProvider`：依赖注入容器，自动注册/解析登录、心跳等模型。
- 功能开关
  - `Features`：`Login`/`Logout`/`Ping`/`Upgrade`/`Notify`/`CommandReply`/`PostEvent` 可按需启用。
  - `Actions`：各功能对应的动作路径字典，默认前缀 `Device/`，可在 `SetActions` 中调整。
- 诊断
  - `Log`：日志；`Tracer`：链路追踪。
  - `Delay`：最近一次往返延迟（毫秒）；`GetNow()`：按服务端校准后的当前时间（本地时区）。

## 生命周期
- `Open()`：定时尝试登录，网络就绪后自动登录并启动心跳/升级定时器。
- `Login(source?)`：登录成功后自动设置 Token、修正时间偏移并触发 `OnLogined`。
- `Logout(reason?)`：调用服务端注销，停止定时器并重置状态。
- `Dispose()`：内部尝试注销并释放资源。

## 远程调用
- `InvokeAsync<TResult>(action, args)`：统一调用入口。
  - HTTP 自动判定 GET/POST：参数为空/基础类型、或 `action` 以 `Get` 开头/包含 `/get` 时使用 GET。
  - 若返回 401（Unauthorized），按需自动重登一次后重试当前请求。
- `GetAsync<TResult>(action, args)`：HTTP 专用 GET（可直接调用）。
- `Invoke<TResult>(...)`：同步封装。

## 心跳与时间校准
- `Ping()`：上报运行状态与性能。失败入队列（最多 `MaxFails`）等待后续重试；服务端可通过响应调整心跳周期。
- `FixTime()`：内部计算往返延迟 `Delay` 和时间偏移 `_span`，`GetNow()` 返回对齐后的本地时区时间。

## 下行通知与命令
- 启用 `Features.Notify` 后，HTTP 模式自动维护 WebSocket 长连接以接收下行通知。
- 命令处理
  - 事件：`Received`（收到命令时触发）。
  - 去重：相同 `Id` 的命令只执行一次。
  - 过期/定时：按 `Expire`/`StartTime` 自动丢弃或延迟执行；延迟执行在后台排队。
  - 执行完成可通过 `CommandReply` 上报（需启用 `Features.CommandReply`）。

## 事件上报
- `WriteEvent(type, name, remark)`：写入事件队列并由定时器批量上报 `PostEvents`。
- 失败事件进入本地失败队列，待网络恢复后重试。

## 在线升级
- 启用 `Features.Upgrade` 后，调用 `Upgrade(channel?)`：查询更新、下载、哈希校验、解压与覆盖，可选执行预安装脚本/执行器；强制更新后可 `Restart()`。
- `BuildUrl(relativeUrl)`：在 HTTP 模式下，将相对地址转为基于当前服务地址的绝对地址。

## 扩展点（常用重写）
- `SetActions(prefix)`：统一定义动作路径前缀与映射。
- `CreateHttp(urls)`/`CreateRpc(urls)`：自定义底层客户端配置（如 Header、重试策略、SocketLog 等）。
- `BuildLoginRequest()`/`FillLoginRequest(req)`：补充版本、编译时间、网络信息、设备唯一标识、时间戳等。
- `BuildPingRequest()`/`FillPingRequest(req)`：丰富心跳上报内容。
- `UpgradeAsync(channel)`：自定义升级信息获取。
- `OnPing(state)`：心跳周期内自定义行为（例如额外巡检）。

## 常见问题
- 如何区分 HTTP GET/POST？
  - 默认规则：参数为空/基础类型，或动作名以 `Get` 开头/包含 `/get`（忽略大小写）→ GET；否则 POST。
- 为什么会自动重登？
  - 当调用返回 401（Unauthorized）时，如果启用了 `Login` 能力，将切换到待登录并执行一次登录，然后重试原请求。
- 多地址如何负载均衡？
  - `Server` 支持逗号分隔多个地址；运行时变更地址会自动切换。
- 如何获取与服务端一致的当前时间？
  - 使用 `GetNow()`，其基于最近一次心跳/登录的时间差进行本地时间校准。

## 完整示例（带命令回执）
```csharp
var client = new MyClient(new MySetting
{
    Server = "https://api.example.com",
    Code = "dev-001",
    Secret = "secret"
})
{
    Log = XTrace.Log
};

client.Received += async (s, e) =>
{
    if (e.Model?.Command == "Reboot")
    {
        // TODO: 执行重启
        await client.CommandReply(new CommandReplyModel
        {
            Id = e.Model.Id,
            Status = CommandStatus.成功,
            Data = "已执行重启"
        });
    }
};

client.Open();
```

## 注意事项
- `ClientBase` 为抽象类型，请定义派生类后使用。
- 高并发下的状态扩展请自行保证线程安全。
- 日志与追踪：建议在生产开启必要的 Info 级别日志，Debug 级别仅用于定位问题。

## 变更记录
- 文档首次添加：介绍基于 `ClientBase` 的典型使用方式与最佳实践。
