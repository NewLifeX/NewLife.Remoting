# NewLife.Remoting - 统一高性能远程通信框架 (RPC + HTTP + WebSocket + SRMP)

![GitHub top language](https://img.shields.io/github/languages/top/newlifex/NewLife.Remoting?logo=github)
![GitHub License](https://img.shields.io/github/license/newlifex/NewLife.Remoting?logo=github)
![Nuget Downloads](https://img.shields.io/nuget/dt/NewLife.Remoting?logo=nuget)
![Nuget](https://img.shields.io/nuget/v/NewLife.Remoting?logo=nuget)
![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/NewLife.Remoting?label=dev%20nuget&logo=nuget)

![Nuget Downloads](https://img.shields.io/nuget/dt/NewLife.Remoting.Extensions?logo=nuget)
![Nuget](https://img.shields.io/nuget/v/NewLife.Remoting.Extensions?logo=nuget)
![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/NewLife.Remoting.Extensions?label=dev%20nuget&logo=nuget)

> 简单、统一、可扩展、跨多目标框架 (net45 ~ net9.0) 的远程通信基础设施。单一生态内同时覆盖：
> - 二进制高性能 RPC (长连接 / 主动下发 / 海量连接与吞吐)
> - 标准 HTTP / REST (易集群 / 生态丰富 / 负载均衡)
> - WebSocket 指令下发与事件推送
> - SRMP (Simple Remote Message Protocol) 远程消息协议
> - 统一应用客户端基类 ClientBase（登录 / 心跳 / 升级 / 指令 / 事件）
>
> 源码：https://github.com/NewLifeX/NewLife.Remoting  
> NuGet：`NewLife.Remoting` / `NewLife.Remoting.Extensions`

---
## 目录导航
- [核心特性](#核心特性)
- [架构概览](#架构概览)
- [适用场景对比](#适用场景对比)
- [快速开始](#快速开始)
  - [RPC 服务端最小示例](#rpc-服务端最小示例)
  - [RPC 客户端调用示例](#rpc-客户端调用示例)
  - [HTTP + WebSocket 设备接入](#http--websocket-设备接入)
- [统一客户端 ClientBase 能力](#统一客户端-clientbase-能力)
- [SRMP 协议简介](#srmp-协议简介)
- [性能指标](#性能指标)
- [认证与安全](#认证与安全)
- [扩展与二次开发](#扩展与二次开发)
- [与其它 NewLife 组件协同](#与其它-newlife-组件协同)
- [多目标框架兼容策略](#多目标框架兼容策略)
- [项目生态矩阵](#项目生态矩阵)
- [贡献与反馈](#贡献与反馈)
- [团队与版权](#团队与版权)

---
## 核心特性
1. 双架构：同一套模型同时支持【高性能二进制 RPC】与【标准 HTTP/REST】
2. 海量连接：单机典型 1 万 TCP 长连接，实验峰值 400 万；吞吐典型 10 万 TPS，实验峰值 2266 万 TPS（依赖 NewLife.Net 内核）
3. 零第三方重依赖：日志 / 序列化 / 网络栈 / 对象池 均为自研生态，可控可裁剪
4. 可观测性：内置性能统计、慢调用跟踪、分布式追踪接口 (ITracer)
5. 统一客户端生命周期：登录、心跳、升级、指令、事件上报 一站式封装
6. SRMP 协议：Header + Body 简洁帧格式，支持粘拆包、双向消息、OneWay、Reply 标识
7. 安全：令牌颁发 + 可插拔密码/签名提供者 + Token 续期策略
8. WebSocket 通道：HTTP 架构下的实时指令下发与推送
9. 控制器模型：与 WebApi 类似的 Controller/Action 调用体验（ApiServer / ApiController）
10. 扩展组建：`NewLife.Remoting.Extensions` 提供 ASP.NET Core 设备接入基类、令牌服务、会话管理、模型绑定器

---
## 架构概览
```
          +-------------------+                     +--------------------+
          |  客户端 ClientBase |                     |  服务端 ApiServer   |
          |  (ApiClient/HTTP) |  <--- SRMP/TCP --->  |  控制器/Handler     |
          +---------+---------+                     +----+-----------+----+
                    | HTTP/REST 负载均衡                  | 注册服务/依赖注入
                    v                                   v
          +-------------------+   WebSocket   +--------------------------+
          | ApiHttpClient     |<------------->|  BaseDeviceController    |
          +-------------------+  指令/事件     +-----------+--------------+
                                                     | TokenService
                                                     | SessionManager
                                                     v
                                              +---------------+
                                              | 业务/存储/缓存 |
                                              +---------------+
```
- RPC：长连接 / 二进制 / 低开销 / 服务端可主动推送
- HTTP：标准化 / 容易接入网关 / 每次独立认证 / WebSocket 下发指令
- 二者共享：登录模型、心跳语义、令牌机制、序列化策略、事件与指令抽象

---
## 适用场景对比
| 维度 | RPC 架构 | HTTP 架构 |
| ---- | -------- | --------- |
| 连接模型 | 长连接 (TCP/UDP) | 短连接 + 可选 WebSocket |
| 吞吐/延迟 | 极致性能/低延迟 | 受 HTTP 栈 & 负载均衡影响 |
| 推送能力 | 服务端直接下发 | 需 WebSocket 通道 |
| 负载均衡 | 客户端挑选一个节点保持 | 请求级（云原生友好） |
| 二进制大包 | 友好（自定义序列化） | 不适合（默认 JSON） |
| 接入复杂度 | 需要 SDK/协议 | 任意 HTTP 客户端即可 |
| 典型场景 | 任务调度、物联网网关、工业采集 | 设备管理平台、通用 REST、混合接入 |

---
## 快速开始
### 安装模板（推荐）
```
dotnet new install NewLife.Templates
```

### RPC 服务端最小示例
```
dotnet new rpcserver --name RpcServer
cd RpcServer
dotnet run
```
核心代码（示意）：
```csharp
var server = new ApiServer(12345);
server.Register(new MyController(), null); // 暴露控制器全部 Action
server.Start();

public class MyController : ApiController
{
    public String Ping(String name) => $"Hello {name}, {DateTime.Now:HH:mm:ss}";
}
```

### RPC 客户端调用示例
```csharp
var client = new ApiClient("tcp://127.0.0.1:12345");
await client.OpenAsync();
var rs = await client.InvokeAsync<String>("My/Ping", new { name = "dev" });
Console.WriteLine(rs);
```

### HTTP + WebSocket 设备接入
Program.cs 中：
```csharp
builder.Services.AddRemoting();
var app = builder.Build();
app.UseRemoting();
app.MapControllers();
app.Run();

public class DeviceController : BaseDeviceController
{
    public DeviceController(IServiceProvider p) : base(p) { }
}
```
客户端使用 ApiHttpClient 登录 + 周期 Ping，服务端可通过 WebSocket 通道下发指令。

---
## 统一客户端 ClientBase 能力
ClientBase 抽象了设备 / 节点 / 应用常见生命周期：
- 登录：多种模式（应用 AppId/AppSecret、设备 Code/Secret、OAuth）
- 心跳：带服务器时间、令牌续期、保持在线状态
- 升级：统一 Upgrade 查询，返回版本与下载地址（自动补全绝对路径）
- 指令：RPC 直接下发；HTTP 通过 WebSocket 推送；客户端统一 CommandReply 回执
- 事件：批量上报 EventModel[]
- 安全：内置时间戳 + 签名/令牌 验证流程

---
## SRMP 协议简介
SRMP (Simple Remote Message Protocol)：
- 面向消息的远程调用与推送协议
- 支持标志位：OneWay / Reply / Flag 扩展
- 设计目标：比 HTTP 更低的开销；比原始 TCP 更易解析与调试
- 在 ApiNetServer 中通过 WebSocketServerCodec + 标准消息编解码器组合使用
详见：`Doc/SRMP.MD`。

---
## 性能指标
| 指标 | 典型值 | 实验峰值 | 说明 |
| ---- | ------ | -------- | ---- |
| TCP 长连接数 | 10,000 | 4,000,000 | 依赖内核与系统参数调优 |
| RPC 吞吐 | 100,000 TPS | 22,660,000 TPS | 简单回显场景，基于 NewLife.Net | 
| HTTP 并发连接 | 1,000 | (可水平扩展) | 受 Kestrel/网关限制 |
| 心跳开销 | 微秒级 | - | 轻量协议 + 对象池复用 |
> 建议结合实际业务压测，关注序列化体积与对象分配。

---
## 认证与安全
- 登录：设备 / 应用 / OAuth 三模式；成功返回访问令牌 + 过期时间
- 令牌续期：心跳内置检测，10 分钟内到期可自动刷新（可扩展策略）
- 密钥保护：SaltPasswordProvider（可替换）避免明文密钥直接传输
- 会话管理：SessionManager（WebSocket 长连接登记、关闭释放）
- 异常：统一 ApiException + ApiCode，避免泄漏内部实现细节

---
## 扩展与二次开发
可扩展点：
1. 自定义序列化：实现 IJsonHost / 二进制编码器替换 Encoder
2. 自定义认证：替换 TokenService 或 IPasswordProvider
3. 控制器解析：通过 ApiServer.Register(controller, method)
4. 消息拦截：订阅 ApiServer.Received 统一埋点 / 鉴权 / 限流
5. 协议扩展：在 ApiNetServer.Init 中追加自定义 Codec
6. 事件与指令：实现 IDeviceService / 使用 CommandReply & PostEvents
7. 日志与追踪：注入 ILog / ITracer，实现链路关联

---
## 与其它 NewLife 组件协同
| 组件 | 协同价值 |
| ---- | -------- |
| NewLife.Core | 日志、配置、序列化、对象池、TimerX、性能追踪 |
| NewLife.Net | 高性能网络栈、WebSocket/编码器、端口复用 |
| NewLife.Redis | 分布式缓存 / 消息队列，扩展会话共享或推送 fanout |
| Stardust | 节点注册、配置中心、APM、发布中心；可托管 ApiServer 节点 |
| AntJob | 使用 ClientBase 接入任务调度与分布式计算 |
| NewLife.MQTT / Modbus / IoT | 设备层协议采集后通过 Remoting 上行 |

---
## 多目标框架兼容策略
- 支持：net45 / net461 / netstandard2.0 / netstandard2.1 / net5.0~net9.0
- 通过条件编译隔离新旧 API（Span / ValueTask / Socket 特性等）
- 遵循：不主动移除旧 TFM；新增功能优先判断可用性再启用

---
## 项目生态矩阵
完整生态与说明见下方 [新生命项目矩阵](#新生命项目矩阵)。Remoting 作为“通信基座”横向衔接 网关设备接入 / 调度平台 / 分布式服务治理 / IoT 协议栈。

---
## 贡献与反馈
欢迎：Bug 反馈 / 性能优化 PR / 新协议编解码 / 控制器示例 / 文档补充。  
提交前请阅读 `.github/copilot-instructions.md` 了解编码规范 & Multi‑TFM 原则。  
Issue 讨论请尽量提供：运行环境、目标框架、最小可复现代码、日志摘要。

---
## 快速命令回顾
```
# 安装模板
dotnet new install NewLife.Templates
# 创建 RPC 服务端骨架
dotnet new rpcserver --name RpcServer
# 运行
dotnet run
```

---
## 新生命项目矩阵
各项目默认支持 net9.0/netstandard2.1/netstandard2.0/net4.62/net4.5，旧版（2024.0801）支持 net4.0/net2.0  

| 项目 | 年份 | 说明 |
| :--: | :--: | ---- |
| [NewLife.Core](https://github.com/NewLifeX/X) | 2002 | 核心库，日志、配置、缓存、网络、序列化、APM性能追踪 |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | 2005 | 大数据中间件，单表百亿级，自动分表，读写分离 |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 2005 | 网络库，2266万 tps / 百万连接 |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 2011 | 本项目，双协议通信框架 |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 2010 | 快速开发平台，权限 / OAuth / SSO |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | 2008 | 服务安装 / 守护进程 / Systemd |
| [NewLife.Zero](https://github.com/NewLifeX/NewLife.Zero) | 2020 | 项目脚手架模板集合 |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | 2017 | 高性能 Redis 客户端/消息队列 |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 2018 | 纯托管 RocketMQ 客户端 |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | 2019 | 物联网 MqttClient/MqttServer |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | 2022 | IoT 通信标准模型库 |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | 2022 | Modbus 协议族 |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | 2022 | 西门子 PLC 通信 |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 2022 | 地图聚合组件 |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | 2023 | 音频编解码 |
| Stardust | 2018 | 分布式服务平台 (注册/配置/APM/发布) |
| AntJob | 2019 | 分布式大数据计算平台 |
| 等等 | - | 更多参见官网 |

---
## 团队与版权
![XCode](https://newlifex.com/logo.png)

新生命团队（NewLife）成立于 2002 年，80+ 开源项目，NuGet 累计下载 400+ 万。全部项目采用 MIT 协议，可自由修改再发行（无需声明来源）。  
网站：https://newlifex.com  
开源：https://github.com/NewLifeX  
QQ群：1600800 / 1600838  

> `新生命团队始于2002年，部分开源项目具有20年以上历史，源码库保留2010年以来全部记录。`

微信公众号：  
![智能大石头](https://newlifex.com/stone.jpg)

---
## 变更记录
请关注 Release / Tag 说明或提交历史。后续可引入 CHANGELOG 简述关键性能与兼容性变更。

---
## License
MIT © NewLife. 欢迎商用与二次开发。
