# NewLife.Remoting — 统一远程通信框架 (RPC + HTTP + WebSocket + SRMP)

![GitHub top language](https://img.shields.io/github/languages/top/newlifex/NewLife.Remoting?logo=github)
![GitHub License](https://img.shields.io/github/license/newlifex/NewLife.Remoting?logo=github)
![Nuget Downloads](https://img.shields.io/nuget/dt/NewLife.Remoting?logo=nuget)
![Nuget](https://img.shields.io/nuget/v/NewLife.Remoting?logo=nuget)
![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/NewLife.Remoting?label=dev%20nuget&logo=nuget)

![Nuget Downloads](https://img.shields.io/nuget/dt/NewLife.Remoting.Extensions?logo=nuget)
![Nuget](https://img.shields.io/nuget/v/NewLife.Remoting.Extensions?logo=nuget)
![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/NewLife.Remoting.Extensions?label=dev%20nuget&logo=nuget)

NewLife.Remoting 是 NewLife 技术栈的统一远程通信基础设施，由两个 NuGet 包组成：

| 包 | 定位 | 覆盖 |
|:---|:-----|:------|
| **NewLife.Remoting** | 核心通信库 | RPC 客户端/服务端、协议编解码、连接管理、流式调用、客户端基类、指令下发管线、自动更新 |
| **NewLife.Remoting.Extensions** | 扩展库 | 服务端控制器基类、设备管理服务、Token 鉴权、命令响应总线 |

源码：https://github.com/NewLifeX/NewLife.Remoting  
> NuGet：`NewLife.Remoting` / `NewLife.Remoting.Extensions`

## 文档索引

| 文档 | 说明 |
|------|------|
| [Doc/需求文档.md](Doc/需求文档.md) | 需求定义：愿景、核心目标、功能需求（RPC/STRM/CLNT/UPGD/SRV/CMD） |
| [Doc/架构设计.md](Doc/架构设计.md) | 架构设计：分层概览、核心模块架构、内存管理、测试策略 |
| [Doc/SRV-服务端架构.md](Doc/SRV-服务端架构.md) | 服务端业务架构：服务层设计、核心业务流程（登录/注册/注销/心跳/升级/事件上报/指令下发）、在线会话生命周期、集群部署 |
| [Doc/功能清单.md](Doc/功能清单.md) | 功能追踪：61 个功能点的实现/测试/注释状态 |
| [Doc/竞品分析.md](Doc/竞品分析.md) | 竞品分析：RPC 框架对比 + IoT 指令下发对比矩阵 |
| [Doc/SRMP.MD](Doc/SRMP.MD) | SRMP 协议规范 |
| [Doc/RPC内存管理设计.md](Doc/RPC内存管理设计.md) | 零拷贝内存管理 |
| [Doc/自动更新机制.md](Doc/自动更新机制.md) | 自动更新全链路设计 |
| [Doc/重试策略.md](Doc/重试策略.md) | 重试策略：IRetryPolicy 接口与指数退避示例 |

---

## 亮点

| 核心能力 | 说明 |
|:---------|------|
| **RPC 核心框架** | SRMP 协议、JSON/二进制编解码、请求处理流水线、客户端/服务端、Cluster 连接管理、元数据传递、重试策略 |
| **STRM 流式调用** | Server-Streaming：`IAsyncEnumerable<T>` 逐条推送，SRMP + SSE 双协议 |
| **CLNT 客户端基类** | 登录/心跳/升级/指令/事件一站式封装，WS/SSE 双通道自动切换 |
| **UPGD 自动更新** | 检测→下载→校验→解压→覆盖→重启全链路 |
| **SRV 服务端基础** | ApiController/BaseController/BaseDeviceController、TokenService、IDeviceService |
| **CMD 指令下发** | SessionManager + 事件总线广播 + WS/SSE/Ping 多通道 |

## 快速开始

**5 行服务端，5 行客户端，即刻跑通 RPC 调用：**

```csharp
// === 服务端 ===
var server = new ApiServer(12345);
server.Register<MyController>();
server.Start();

public class MyController : ApiController
{
    public String Ping(String name) => $"Hello {name}, {DateTime.Now:HH:mm:ss}";
    // 流式推送：返回 IAsyncEnumerable<T> 自动逐条下发
    public async IAsyncEnumerable<String> StreamLogs(Int32 count)
    {
        for (var i = 0; i < count; i++)
        {
            await Task.Delay(100);
            yield return $"Log #{i}: {DateTime.Now:HH:mm:ss}";
        }
    }
}

// === 客户端 ===
var client = new ApiClient("tcp://127.0.0.1:12345");
client.Open();

// 普通调用
var rs = await client.InvokeAsync<String>("My/Ping", new { name = "dev" });

// 流式调用 — 逐条接收服务端推送
await foreach (var log in client.InvokeStreamAsync<String>("My/StreamLogs", new { count = 10 }))
    Console.WriteLine(log);

// 元数据传递（自动注入 TraceId）
client.Headers["TenantId"] = "tenant-01";
```
## 性能指标

| 指标 | 典型值 | 实验峰值 |
|:----|:------:|:--------:|
| TCP 长连接数 | 10,000 | 4,000,000 |
| RPC 吞吐 | 100,000 TPS | 22,660,000 TPS |
| 单次 RPC 延迟 | ~41 μs | — |
| 框架兼容 | net45 / net461 / netstandard2.0 / netstandard2.1 / net5.0 ~ net10.0 |

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

## 贡献与反馈

欢迎：Bug 反馈 / 性能优化 PR / 新协议编解码 / 控制器示例 / 文档补充。
Issue 讨论请尽量提供：运行环境、目标框架、最小可复现代码、日志摘要。

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
## License

MIT © NewLife. 欢迎商用与二次开发。
