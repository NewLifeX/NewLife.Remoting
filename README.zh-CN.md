# NewLife.Remoting — 统一远程通信框架 (RPC + HTTP + WebSocket + SRMP)

![GitHub License](https://img.shields.io/github/license/NewLifeX/NewLife.Remoting?logo=github)
![NuGet Downloads](https://img.shields.io/nuget/dt/NewLife.Remoting?logo=nuget)
![NuGet](https://img.shields.io/nuget/v/NewLife.Remoting?logo=nuget)
![NuGet prerelease](https://img.shields.io/nuget/vpre/NewLife.Remoting?label=dev%20nuget&logo=nuget)
![NuGet Downloads (Extensions)](https://img.shields.io/nuget/dt/NewLife.Remoting.Extensions?logo=nuget)
![NuGet (Extensions)](https://img.shields.io/nuget/v/NewLife.Remoting.Extensions?logo=nuget)
![NuGet prerelease (Extensions)](https://img.shields.io/nuget/vpre/NewLife.Remoting.Extensions?label=dev%20nuget&logo=nuget)
[![NativeAOT](https://github.com/NewLifeX/NewLife.Remoting/actions/workflows/aot.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.Remoting/actions/workflows/aot.yml)
[![test](https://github.com/NewLifeX/NewLife.Remoting/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.Remoting/actions/workflows/test.yml)
<!-- metrics:start -->
[![AOT smoke](https://img.shields.io/badge/AOT%20smoke-23%2F23-brightgreen?logo=dotnet)](Doc/AOT兼容性分析.md)
[![TFM](https://img.shields.io/badge/TFM-7%20%28net45%20%E2%86%92%20net10.0%29-512BD4?logo=dotnet)](https://www.nuget.org/packages/NewLife.Remoting)
[![locales](https://img.shields.io/badge/locales-31-2ea44f?logo=dotnet)](Doc/多语言本地化接入.md)
<!-- metrics:end -->
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS%20%7C%20Android%20%7C%20iOS-blue)
![arch](https://img.shields.io/badge/arch-x64%20%7C%20arm64%20%7C%20loongarch64%20%7C%20riscv64%20%7C%20s390x-orange)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/NewLife.Remoting?logo=github)](https://github.com/NewLifeX/NewLife.Remoting/graphs/contributors)

<!-- lang:start -->
🌐 [English](README.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

NewLife.Remoting 是 NewLife 技术栈的统一远程通信基础设施，由两个 NuGet 包组成：

| 包 | 定位 | 覆盖 |
|:---|:-----|:------|
| **NewLife.Remoting** | 核心通信库 | RPC 客户端/服务端、协议编解码、连接管理、流式调用、客户端基类、指令下发管线、自动更新 |
| **NewLife.Remoting.Extensions** | 扩展库 | 服务端控制器基类、最小 API 宿主、设备管理服务、Token 鉴权、命令响应总线 |

源码：https://github.com/NewLifeX/NewLife.Remoting  
> NuGet：`NewLife.Remoting` / `NewLife.Remoting.Extensions`

> ℹ️ 本中文版与 [English README](README.md) 同批维护；若两者内容不一致，以英文版为对外口径。

## 文档索引

| 文档 | 说明 |
|------|------|
| [Doc/需求文档.md](Doc/需求文档.md) | 需求定义：愿景、核心目标、功能需求（RPC/STRM/CLNT/UPGD/SRV/CMD/AOT/L10N/EXT） |
| [Doc/架构设计.md](Doc/架构设计.md) | 架构设计：分层概览、核心模块架构、内存管理、测试策略、设计决策 |
| [Doc/SRV-服务端架构.md](Doc/SRV-服务端架构.md) | 服务端业务架构：服务层设计、核心业务流程（登录/注册/注销/心跳/升级/事件上报/指令下发）、在线会话生命周期、集群部署 |
| [Doc/功能清单.md](Doc/功能清单.md) | 功能追踪：主功能状态、进行中项与待办（发版待办/待修复项） |
| [Doc/竞品分析.md](Doc/竞品分析.md) | 竞品分析：RPC 框架对比 + IoT 指令下发对比矩阵 |
| [Doc/SRMP.MD](Doc/SRMP.MD) | SRMP 协议规范 |
| [Doc/RPC内存管理设计.md](Doc/RPC内存管理设计.md) | 零拷贝内存管理 |
| [Doc/自动更新机制.md](Doc/自动更新机制.md) | 自动更新全链路设计 |
| [Doc/重试策略.md](Doc/重试策略.md) | 重试策略：IRetryPolicy 接口与指数退避示例 |
| [Doc/AOT兼容性分析.md](Doc/AOT兼容性分析.md) | NativeAOT 支持范围、<!--aot-scenarios:start-->23<!--aot-scenarios:end--> 场景实测（冒烟 <!--aot-smoke:start-->23/23<!--aot-smoke:end-->）、下游使用指引与已知边界 |
| [Doc/多语言本地化接入.md](Doc/多语言本地化接入.md) | 运行时消息英文化、`Locales/*.yml` 词表与 `L.T` 接入指引 |
| [Doc/README多语言规范.md](Doc/README多语言规范.md) | README 11 种语言的定序规则、生成脚本与 CI 门禁 |

---

## 亮点

| 核心能力 | 说明 |
|:---------|------|
| **RPC 核心框架** | SRMP 协议、JSON/二进制编解码、请求处理流水线、客户端/服务端、Cluster 连接管理、元数据传递、重试策略、服务端全链路异步化、TraceId 调用链追踪 |
| **STRM 流式调用** | Server-Streaming：`IAsyncEnumerable<T>` 逐条推送，SRMP + SSE 双协议 |
| **CLNT 客户端基类** | 登录/心跳/升级/指令/事件一站式封装，WS/SSE 双通道自动切换 |
| **UPGD 自动更新** | 检测→下载→校验→解压→覆盖→重启全链路 |
| **SRV 服务端基础** | ApiController/BaseController/BaseDeviceController、TokenService、IDeviceService；同一端点协议提供 MVC 与最小 API 两套宿主入口（镜像实现、测试锁定一致） |
| **CMD 指令下发** | SessionManager + 事件总线广播 + WS/SSE/Ping 多通道 |
| **L10N 多语言** | 运行时消息以英文为源文，`Locales/*.yml` 内置 31 种语言词表（含简体/繁体中文），缺译文自动回退英文；不依赖 ResourceManager 与卫星程序集 |
| **AOT 支持** | net8.0+ 常驻 AOT 兼容分析（核心库 0 告警 / Extensions 0 告警）+ 自有源生成器（动作分发表/流式泵/签名类型保留），核心库 <!--aot-scenarios:start-->23<!--aot-scenarios:end--> 场景 + Extensions 最小 API 宿主 11 场景 + XCode 设备服务链路 6 场景 NativeAOT 实测通过（见 AOT 文档 §8） |

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
| RPC 吞吐 | 100,000 TPS | 455,000 msg/s |
| 单次 RPC 延迟 | ~41 μs | — |
| 框架兼容 | net45 / net461 / netstandard2.0 / netstandard2.1 / net6.0 / net8.0 / net10.0（跟随 NewLife.Core v12，net8.0+ 支持 NativeAOT） |

---
## 与其它 NewLife 组件协同

| 组件 | 协同价值 |
| ---- | -------- |
| NewLife.Core | 日志、配置、序列化、多语言、对象池、TimerX、性能追踪 |
| NewLife.Net | 高性能网络栈、WebSocket/编码器、端口复用 |
| NewLife.Redis | 分布式缓存 / 消息队列，扩展会话共享或推送 fanout |
| Stardust | 节点注册、配置中心、APM、发布中心；可托管 ApiServer 节点 |
| AntJob | 使用 ClientBase 接入任务调度与分布式计算 |
| NewLife.MQTT / Modbus / IoT | 设备层协议采集后通过 Remoting 上行 |

## 贡献与反馈

欢迎：Bug 反馈 / 性能优化 PR / 新协议编解码 / 控制器示例 / 文档补充 / 新语种翻译。
Issue 讨论请尽量提供：运行环境、目标框架、最小可复现代码、日志摘要。

---
## 生态版图

NewLife 生态按依赖分层，各项目独立版本、独立发版。下面的版图在所有 NewLife 仓库中逐字一致；每个项目链接到代码仓库与主 NuGet 包。

| 项目 | 一句话定位 | 下载量 |
| --- | --- | --- |
| **基础底座** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | 日志、配置、缓存、网络、序列化、定时调度与 APM 追踪。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | 数据中间件：单表百亿级、自动分表、读写分离，支持 10 多种数据库。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 高性能网络库：单机 177 万 pps、14.5 Gbps（TCP 1KB，2026-10 实测），400 万 Tcp 长连接。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 协议通信与 RPC 框架，面向 CS 应用与物联网设备。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | 把 .NET 应用安装为操作系统守护进程：Windows 服务与 Linux systemd。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | `dotnet new` 项目模板：控制台、Web、WebApi、服务、数据与物联网。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **数据存储与消息** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Redis 客户端：微秒级延迟、百万级吞吐，内置消息队列。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 纯托管 RocketMQ 客户端，支持 Apache RocketMQ 与阿里云消息队列。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | MQTT 客户端与服务端，客户端支持阿里云物联网。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | C# 混合数据库：嵌入/网络双模，关系型、时序、消息队列与 KV 于一体。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | MySql 驱动与数据访问辅助层，基于 NewLife 数据栈实现。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web、AI 与分布式** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 魔方快速开发平台：用户权限、SSO、OAuth 服务端与 CRUD 脚手架。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | 星尘分布式服务平台：节点管理、APM、配置中心、注册中心与发布中心。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | 蚂蚁调度：分布式任务与实时数据平台，支持分片、重试与回溯补算。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | 统一接入 46 家大模型服务商，附带开箱即用的 ChatAI Web 对话应用。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **工业与物联网** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | IoT 标准库，定义物联网通信协议契约与设备抽象。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | Modbus TCP/RTU/ASCII，基于 NewLife.IoT 实现。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | 西门子 PLC 协议，基于 NewLife.IoT 实现。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | 欧姆龙 PLC 协议，基于 NewLife.IoT 实现。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | 三菱 Melsec PLC 协议，基于 NewLife.IoT 实现。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | OPC 客户端，用于工业数据采集。 | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | BACnet 楼宇自控协议栈。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | 开源轻量级 IoT 平台，含网关与设备管理。 | — |
| **车联网** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | JT/T 808 车辆定位协议：70+ 消息体，覆盖 2011/2013/2019 三个版本。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | JT/T 809 部标平台互联协议，面向运输管理部门。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | JT/T 1078 车载视频：RTP 解析、FLV/fMP4 转封装与直播推送。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **领域组件** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | 一个包覆盖 Excel、Word、PPT、PDF 等 15+ 文档格式。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | 跨平台音频：编解码、设备 IO、DSP、容器与流式处理。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | 视频编解码、封装格式、帧处理与流媒体。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 百度/高德/腾讯/天地图统一接口，支持地理编码与坐标转换。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | IPv4 归属地：离线内存映射库 + 在线 API 双源。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **工具与产品** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | 码神工具：网络、串口、加解密、正则、Modbus 与 MQTT。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | 自托管 Git 源代码管理系统。 | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | 从低版本宿主启动时，自动安装匹配的 .NET 运行时。 | — |

> 徽章取各项目主包；主题包、扩展包与卫星包见对应 NuGet 页面。未在表中列出的 30 多个包（FTP、LoRa、Office 扩展、各类协议驱动等）见 [NewLifeX 组织](https://github.com/NewLifeX) 与 [newlifex.com](https://newlifex.com)。

---
## 新生命开发团队

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

新生命团队成立于 2002 年，二十余年专注 .NET 与物联网技术生态，主导开源项目近百个（生态版图收录 34 个主干项目），为电力、物流、医疗、工控、互联网等行业提供软硬件一体化解决方案；以“学无先后达者为师”为信条。

开源组件在 NuGet 累计下载 <!--nuget-total:start-->6.48M<!--nuget-total:end--> 次（全生态口径，截至 2026-10）。

网站：https://newlifex.com · 开源组织：https://github.com/NewLifeX

<details>
<summary>微信公众号：智能大石头（扫码关注）</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

---
## License

MIT © NewLife. 欢迎商用与二次开发。
