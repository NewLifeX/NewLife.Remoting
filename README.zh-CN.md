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
| RPC 吞吐 | 100,000 TPS | 22,660,000 TPS |
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
## 新生命项目矩阵

各项目默认支持 net10.0/net8.0/netstandard2.1/netstandard2.0/net4.62/net4.5，旧版（2024.0801）支持 net4.0/net2.0  

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
