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
[![AOT smoke](https://img.shields.io/badge/AOT%20smoke-23%2F23-brightgreen?logo=dotnet)](Doc/AOT兼容性分析.md)
[![TFM](https://img.shields.io/badge/TFM-7%20%28net45%20%E2%86%92%20net10.0%29-512BD4?logo=dotnet)](https://www.nuget.org/packages/NewLife.Remoting)
[![locales](https://img.shields.io/badge/locales-31-2ea44f?logo=dotnet)](Doc/多语言本地化接入.md)
<!-- metrics:end -->
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS%20%7C%20Android%20%7C%20iOS-blue)
![arch](https://img.shields.io/badge/arch-x64%20%7C%20arm64%20%7C%20loongarch64%20%7C%20riscv64%20%7C%20s390x-orange)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/NewLife.Remoting?logo=github)](https://github.com/NewLifeX/NewLife.Remoting/graphs/contributors)

<!-- lang:start -->
🌐 [中文](Readme.MD) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

NewLife.Remoting is the unified remoting infrastructure of the NewLife stack, shipped as two NuGet packages:

| Package | Role | Coverage |
|:---|:-----|:------|
| **NewLife.Remoting** | Core communication library | RPC client/server, protocol codecs, connection management, streaming calls, client base classes, command dispatch pipeline, auto-update |
| **NewLife.Remoting.Extensions** | Extension library | Server controller base classes, minimal API host, device management services, token authentication, command response bus |

Source: https://github.com/NewLifeX/NewLife.Remoting  
> NuGet: `NewLife.Remoting` / `NewLife.Remoting.Extensions`

> ⚠️ This translation may lag behind the [Chinese README](Readme.MD), which is the authoritative source.

## Documentation Index

| Document | Description |
|------|------|
| [Doc/需求文档.md](Doc/需求文档.md) | Requirements: vision, core goals, functional requirements (RPC/STRM/CLNT/UPGD/SRV/CMD/AOT/L10N/EXT) |
| [Doc/架构设计.md](Doc/架构设计.md) | Architecture: layering, core module design, memory management, test strategy, design decisions |
| [Doc/SRV-服务端架构.md](Doc/SRV-服务端架构.md) | Server business architecture: service layer, core flows (login/register/logout/heartbeat/upgrade/event upload/command push), online session lifecycle, cluster deployment |
| [Doc/功能清单.md](Doc/功能清单.md) | Feature tracking: status, in-progress items and backlog (release TODOs / pending fixes) |
| [Doc/竞品分析.md](Doc/竞品分析.md) | Competitive analysis: RPC framework comparison + IoT command push matrix |
| [Doc/SRMP.MD](Doc/SRMP.MD) | SRMP protocol specification |
| [Doc/RPC内存管理设计.md](Doc/RPC内存管理设计.md) | Zero-copy memory management |
| [Doc/自动更新机制.md](Doc/自动更新机制.md) | End-to-end auto-update design |
| [Doc/重试策略.md](Doc/重试策略.md) | Retry policy: `IRetryPolicy` and exponential backoff samples |
| [Doc/AOT兼容性分析.md](Doc/AOT兼容性分析.md) | NativeAOT support scope, <!--aot-scenarios:start-->23<!--aot-scenarios:end--> verified scenarios (smoke <!--aot-smoke:start-->23/23<!--aot-smoke:end-->), downstream guidance and known limits |
| [Doc/多语言本地化接入.md](Doc/多语言本地化接入.md) | English runtime messages, `Locales/*.yml` tables and `L.T` integration guide |
| [Doc/README多语言规范.md](Doc/README多语言规范.md) | Ordering rules, generator scripts and CI gate for the 11 README languages |

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
| RPC throughput | 100,000 TPS | 22,660,000 TPS |
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
## NewLife Project Matrix

All projects support net10.0/net8.0/netstandard2.1/netstandard2.0/net4.62/net4.5 by default; the 2024.0801 release line also supports net4.0/net2.0.

| Project | Year | Description |
| :--: | :--: | ---- |
| [NewLife.Core](https://github.com/NewLifeX/X) | 2002 | Core library: logging, configuration, caching, networking, serialization, APM tracing |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | 2005 | Data middleware: tens of billions of rows per table, sharding, read/write splitting |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 2005 | Network library: 22.66M tps / millions of connections |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 2011 | This project: dual-protocol remoting framework |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 2010 | Rapid development platform: permissions / OAuth / SSO |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | 2008 | Service installation / daemon / Systemd |
| [NewLife.Zero](https://github.com/NewLifeX/NewLife.Zero) | 2020 | Project scaffolding templates |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | 2017 | High-performance Redis client / message queue |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 2018 | Fully managed RocketMQ client |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | 2019 | IoT MqttClient/MqttServer |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | 2022 | IoT communication standard models |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | 2022 | Modbus protocol family |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | 2022 | Siemens PLC communication |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 2022 | Map aggregation components |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | 2023 | Audio codecs |
| Stardust | 2018 | Distributed service platform (registry/config/APM/release) |
| AntJob | 2019 | Distributed big-data computing platform |
| More | - | See the official website |

---
## Team and Copyright

![XCode](https://newlifex.com/logo.png)

NewLife was founded in 2002 with 80+ open-source projects and more than 4 million cumulative NuGet downloads. All projects are MIT licensed and may be modified and redistributed freely (no attribution required).  
Website: https://newlifex.com  
GitHub: https://github.com/NewLifeX  
QQ groups: 1600800 / 1600838  

> `NewLife started in 2002; some projects are over 20 years old and the source repositories keep every commit since 2010.`

---
## License

MIT © NewLife. Commercial use and secondary development are welcome.
