<!-- generated from README.md by sync-readme-package.ps1; do not edit -->

# NewLife.Remoting — Unified Remoting Framework (RPC + HTTP + WebSocket + SRMP)

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
[![AOT smoke](https://img.shields.io/badge/AOT%20smoke-23%2F23-brightgreen?logo=dotnet)](https://github.com/NewLifeX/NewLife.Remoting/blob/master/Doc/AOT兼容性分析.md)
[![TFM](https://img.shields.io/badge/TFM-7%20%28net45%20%E2%86%92%20net10.0%29-512BD4?logo=dotnet)](https://www.nuget.org/packages/NewLife.Remoting)
[![locales](https://img.shields.io/badge/locales-31-2ea44f?logo=dotnet)](https://github.com/NewLifeX/NewLife.Remoting/blob/master/Doc/多语言本地化接入.md)
<!-- metrics:end -->
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS%20%7C%20Android%20%7C%20iOS-blue)
![arch](https://img.shields.io/badge/arch-x64%20%7C%20arm64%20%7C%20loongarch64%20%7C%20riscv64%20%7C%20s390x-orange)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/NewLife.Remoting?logo=github)](https://github.com/NewLifeX/NewLife.Remoting/graphs/contributors)

<!-- lang:start -->
🌐 [简体中文](https://github.com/NewLifeX/NewLife.Remoting/blob/master/README.zh-CN.md) | [繁體中文](https://github.com/NewLifeX/NewLife.Remoting/blob/master/README.zh-TW.md) | [Español](https://github.com/NewLifeX/NewLife.Remoting/blob/master/README.es.md) | [Deutsch](https://github.com/NewLifeX/NewLife.Remoting/blob/master/README.de.md) | [日本語](https://github.com/NewLifeX/NewLife.Remoting/blob/master/README.ja.md) | [Français](https://github.com/NewLifeX/NewLife.Remoting/blob/master/README.fr.md) | [Português](https://github.com/NewLifeX/NewLife.Remoting/blob/master/README.pt.md) | [Русский](https://github.com/NewLifeX/NewLife.Remoting/blob/master/README.ru.md) | [한국어](https://github.com/NewLifeX/NewLife.Remoting/blob/master/README.ko.md) | [العربية](https://github.com/NewLifeX/NewLife.Remoting/blob/master/README.ar.md)
<!-- lang:end -->

NewLife.Remoting is the unified remoting infrastructure of the NewLife stack, shipped as two NuGet packages:

| Package | Role | Coverage |
|:---|:-----|:------|
| **NewLife.Remoting** | Core communication library | RPC client/server, protocol codecs, connection management, streaming calls, client base classes, command dispatch pipeline, auto-update |
| **NewLife.Remoting.Extensions** | Extension library | Server controller base classes, minimal API host, device management services, token authentication, command response bus |

Source: https://github.com/NewLifeX/NewLife.Remoting  
> NuGet: `NewLife.Remoting` / `NewLife.Remoting.Extensions`

## Documentation Index

| Document | Description |
|------|------|
| [Doc/需求文档.md](https://github.com/NewLifeX/NewLife.Remoting/blob/master/Doc/需求文档.md) | Requirements: vision, core goals, functional requirements (RPC/STRM/CLNT/UPGD/SRV/CMD/AOT/L10N/EXT) |
| [Doc/架构设计.md](https://github.com/NewLifeX/NewLife.Remoting/blob/master/Doc/架构设计.md) | Architecture: layering, core module design, memory management, test strategy, design decisions |
| [Doc/SRV-服务端架构.md](https://github.com/NewLifeX/NewLife.Remoting/blob/master/Doc/SRV-服务端架构.md) | Server business architecture: service layer, core flows (login/register/logout/heartbeat/upgrade/event upload/command push), online session lifecycle, cluster deployment |
| [Doc/功能清单.md](https://github.com/NewLifeX/NewLife.Remoting/blob/master/Doc/功能清单.md) | Feature tracking: status, in-progress items and backlog (release TODOs / pending fixes) |
| [Doc/竞品分析.md](https://github.com/NewLifeX/NewLife.Remoting/blob/master/Doc/竞品分析.md) | Competitive analysis: RPC framework comparison + IoT command push matrix |
| [Doc/SRMP.MD](https://github.com/NewLifeX/NewLife.Remoting/blob/master/Doc/SRMP.MD) | SRMP protocol specification |
| [Doc/RPC内存管理设计.md](https://github.com/NewLifeX/NewLife.Remoting/blob/master/Doc/RPC内存管理设计.md) | Zero-copy memory management |
| [Doc/自动更新机制.md](https://github.com/NewLifeX/NewLife.Remoting/blob/master/Doc/自动更新机制.md) | End-to-end auto-update design |
| [Doc/重试策略.md](https://github.com/NewLifeX/NewLife.Remoting/blob/master/Doc/重试策略.md) | Retry policy: `IRetryPolicy` and exponential backoff samples |
| [Doc/AOT兼容性分析.md](https://github.com/NewLifeX/NewLife.Remoting/blob/master/Doc/AOT兼容性分析.md) | NativeAOT support scope, <!--aot-scenarios:start-->23<!--aot-scenarios:end--> verified scenarios (smoke <!--aot-smoke:start-->23/23<!--aot-smoke:end-->), downstream guidance and known limits |
| [Doc/多语言本地化接入.md](https://github.com/NewLifeX/NewLife.Remoting/blob/master/Doc/多语言本地化接入.md) | English runtime messages, `Locales/*.yml` tables and `L.T` integration guide |
| [Doc/README多语言规范.md](https://github.com/NewLifeX/NewLife.Remoting/blob/master/Doc/README多语言规范.md) | Ordering rules, generator scripts and CI gate for the 11 README languages |

---

## Highlights

| Capability | Description |
|:---------|------|
| **RPC core** | SRMP protocol, JSON/binary codecs, request pipeline, client/server, cluster connection management, metadata propagation, retry policy, fully asynchronous server path, TraceId call-chain tracing |
| **STRM streaming** | Server streaming: `IAsyncEnumerable<T>` pushed item by item over SRMP and SSE |
| **CLNT client base** | Login/heartbeat/upgrade/command/event in one place, automatic WS/SSE channel switching |
| **UPGD auto-update** | Detect → download → verify → extract → overwrite → restart |
| **SRV server base** | ApiController/BaseController/BaseDeviceController, TokenService, IDeviceService; the same endpoint protocol has both MVC and minimal API hosts (mirrored implementations locked by tests) |
| **CMD command push** | SessionManager + event bus broadcast + WS/SSE/Ping channels |
| **L10N localization** | Runtime messages use English as the source text; `Locales/*.yml` ship 31 built-in languages (including Simplified and Traditional Chinese), missing entries fall back to English; no ResourceManager or satellite assemblies |
| **AOT support** | Always-on AOT analysis for net8.0+ (0 warnings in the core library and Extensions) + in-house source generator (action dispatch table, stream pumps, signature type preservation); NativeAOT verified for <!--aot-scenarios:start-->23<!--aot-scenarios:end--> core scenarios, 11 minimal-API host scenarios and 6 XCode device-service scenarios (see AOT doc §8) |

## Quick Start

**Five lines on the server, five on the client, and RPC works:**

```csharp
// === Server ===
var server = new ApiServer(12345);
server.Register<MyController>();
server.Start();

public class MyController : ApiController
{
    public String Ping(String name) => $"Hello {name}, {DateTime.Now:HH:mm:ss}";
    // Streaming: returning IAsyncEnumerable<T> pushes items one by one
    public async IAsyncEnumerable<String> StreamLogs(Int32 count)
    {
        for (var i = 0; i < count; i++)
        {
            await Task.Delay(100);
            yield return $"Log #{i}: {DateTime.Now:HH:mm:ss}";
        }
    }
}

// === Client ===
var client = new ApiClient("tcp://127.0.0.1:12345");
client.Open();

// Regular call
var rs = await client.InvokeAsync<String>("My/Ping", new { name = "dev" });

// Streaming call — receive server pushes one by one
await foreach (var log in client.InvokeStreamAsync<String>("My/StreamLogs", new { count = 10 }))
    Console.WriteLine(log);

// Metadata propagation (TraceId is injected automatically)
client.Headers["TenantId"] = "tenant-01";
```

## Performance

| Metric | Typical | Peak in lab |
|:----|:------:|:--------:|
| TCP long connections | 10,000 | 4,000,000 |
| RPC throughput | 100,000 TPS | 455,000 msg/s |
| Single RPC latency | ~41 μs | — |
| Framework support | net45 / net461 / netstandard2.0 / netstandard2.1 / net6.0 / net8.0 / net10.0 (aligned with NewLife.Core v12; NativeAOT on net8.0+) |

---
## Working with Other NewLife Components

| Component | Value |
| ---- | -------- |
| NewLife.Core | Logging, configuration, serialization, localization, object pooling, TimerX, performance tracing |
| NewLife.Net | High-performance network stack, WebSocket/codecs, port reuse |
| NewLife.Redis | Distributed cache / message queue for shared sessions or push fan-out |
| Stardust | Node registration, config center, APM, release center; can host ApiServer nodes |
| AntJob | Job scheduling and distributed computing via ClientBase |
| NewLife.MQTT / Modbus / IoT | Collect device protocols and report upstream through Remoting |

## Contributing and Feedback

Contributions welcome: bug reports, performance PRs, new protocol codecs, controller samples, documentation and new translations.
When opening an issue, please include: runtime environment, target framework, minimal repro and log excerpt.

---
## Ecosystem Map

NewLife is a layered ecosystem of independently versioned open-source projects. The map below is shared verbatim by every NewLife repository; each project links to its repository and its primary NuGet package.

| Project | What it does | Downloads |
| --- | --- | --- |
| **Foundation** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | Logging, configuration, caching, networking, serialization, scheduling and APM tracing. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | Data middleware: single tables at the tens-of-billions scale, automatic sharding, read-write splitting, 10+ databases. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | High-performance network library: 1.77M pps / 14.5 Gbps on a single machine (TCP 1KB, 2026-10), 4M long-lived TCP connections. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | Protocol communication and RPC framework for client/server applications and IoT devices. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | Installs .NET applications as OS daemons: Windows services and Linux systemd. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | `dotnet new` project templates for console, web, API, service, data and IoT projects. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **Data & Messaging** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Redis client with microsecond latency, millions of ops/s and built-in message queues. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | Pure managed RocketMQ client for Apache RocketMQ and Alibaba Cloud Message Queue. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | MQTT client and server, including Alibaba Cloud IoT connectivity. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | Hybrid database in C#: embedded or networked, relational, time-series, message queue and KV in one. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | MySql driver and helper layer built on the NewLife data stack. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web, AI & Distributed** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | Rapid admin-platform framework: permissions, SSO, OAuth server and CRUD scaffolding. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | Distributed service platform: node management, APM, configuration center, registry and release center. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | Distributed task and real-time data scheduling platform with sharding, retries and backfill. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | Unified client for 46 LLM providers plus ChatAI, a ready-to-run web chat application. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **Industrial & IoT** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | IoT standard library defining protocol contracts and device abstractions. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | Modbus TCP/RTU/ASCII implementation on top of NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | Siemens PLC protocol on top of NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | Omron PLC protocol on top of NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | Mitsubishi Melsec PLC protocol on top of NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | OPC client for industrial data access. | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | BACnet building-automation protocol stack. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | Lightweight open-source IoT platform with gateway and device management. | — |
| **Vehicle Telematics** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | JT/T 808 vehicle positioning protocol: 70+ message bodies, 2011/2013/2019. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | JT/T 809 platform-to-platform protocol for transport authorities. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | JT/T 1078 in-vehicle video: RTP parsing, FLV/fMP4 remuxing and live streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **Domain Components** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | One package for Excel, Word, PPT, PDF and 15+ document formats. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | Cross-platform audio: codecs, device I/O, DSP, containers and streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | Video codecs, container formats, frame processing and streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | Unified interface over Baidu, AMap, Tencent and Tianditu with geocoding and coordinate conversion. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | IPv4 geolocation with an offline memory-mapped database and an online API fallback. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **Tools & Products** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | Developer toolbox: network, serial port, crypto, regex, Modbus and MQTT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | Self-hosted Git source code management system. | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | Installs the matching .NET runtime automatically when launched from an older host. | — |

> Badges show the primary package of each project; theme, extension and satellite packages are listed on their NuGet pages.The 30+ packages not listed here (FTP, LoRa, Office extensions, protocol drivers and more) are available under the [NewLifeX organization](https://github.com/NewLifeX) and on [newlifex.com](https://newlifex.com).

---
## Team

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

Founded in 2002, the NewLife team has spent more than two decades on the .NET and IoT ecosystem, maintains nearly 100 open-source projects (34 of them core to this map), and delivers integrated software and hardware solutions for power, logistics, healthcare, industrial control and internet industries. We live by the motto "learning has no seniority — mastery earns respect".

Open-source components have been downloaded <!--nuget-total:start-->6.48M<!--nuget-total:end--> times from NuGet (ecosystem-wide, as of 2026-10).

Website: https://newlifex.com · Open source organization: https://github.com/NewLifeX

<details>
<summary>WeChat Official Account: 智能大石头 (scan to follow)</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

---
## License

MIT © NewLife. Commercial use and secondary development are welcome.
