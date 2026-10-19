# NewLife.Remoting — Einheitliches Remoting-Framework (RPC + HTTP + WebSocket + SRMP)

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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

NewLife.Remoting ist die einheitliche Remoting-Infrastruktur des NewLife-Stacks und besteht aus zwei NuGet-Paketen:

| Paket | Rolle | Umfang |
|:---|:-----|:------|
| **NewLife.Remoting** | Kernbibliothek | RPC-Client/Server, Protokoll-Codecs, Verbindungsverwaltung, Streaming-Aufrufe, Client-Basisklassen, Kommando-Pipeline, Auto-Update |
| **NewLife.Remoting.Extensions** | Erweiterungsbibliothek | Controller-Basisklassen, Minimal-API-Host, Geräteverwaltung, Token-Authentifizierung, Command-Response-Bus |

Quellcode: https://github.com/NewLifeX/NewLife.Remoting  
> NuGet: `NewLife.Remoting` / `NewLife.Remoting.Extensions`

> ⚠️ Diese Übersetzung kann veraltet sein; maßgeblich ist das [englische README](README.md).

## Dokumentationsindex

| Dokument | Beschreibung |
|------|------|
| [Doc/需求文档.md](Doc/需求文档.md) | Anforderungen: Vision, Ziele, funktionale Anforderungen (RPC/STRM/CLNT/UPGD/SRV/CMD/AOT/L10N/EXT) |
| [Doc/架构设计.md](Doc/架构设计.md) | Architektur: Schichten, Kernmodule, Speicherverwaltung, Teststrategie, Designentscheidungen |
| [Doc/SRV-服务端架构.md](Doc/SRV-服务端架构.md) | Server-Architektur: Service-Schicht, Kernabläufe (Login/Registrierung/Logout/Heartbeat/Update/Ereignisse/Kommandos), Session-Lebenszyklus, Cluster-Betrieb |
| [Doc/功能清单.md](Doc/功能清单.md) | Funktionsverfolgung: Status, laufende Punkte und Backlog |
| [Doc/竞品分析.md](Doc/竞品分析.md) | Wettbewerbsanalyse: RPC-Frameworks und IoT-Kommandozustellung |
| [Doc/SRMP.MD](Doc/SRMP.MD) | SRMP-Protokollspezifikation |
| [Doc/RPC内存管理设计.md](Doc/RPC内存管理设计.md) | Zero-Copy-Speicherverwaltung |
| [Doc/自动更新机制.md](Doc/自动更新机制.md) | Vollständiges Auto-Update-Design |
| [Doc/重试策略.md](Doc/重试策略.md) | Wiederholungsstrategie: `IRetryPolicy` und exponentielles Backoff |
| [Doc/AOT兼容性分析.md](Doc/AOT兼容性分析.md) | NativeAOT-Umfang, <!--aot-scenarios:start-->23<!--aot-scenarios:end--> geprüfte Szenarien (Smoke <!--aot-smoke:start-->23/23<!--aot-smoke:end-->), Anleitung und bekannte Grenzen |
| [Doc/多语言本地化接入.md](Doc/多语言本地化接入.md) | Englische Laufzeitmeldungen, `Locales/*.yml`-Tabellen und `L.T`-Anleitung |
| [Doc/README多语言规范.md](Doc/README多语言规范.md) | Sortierregeln, Generator-Skripte und CI-Gate für die 11 README-Sprachen |

---

## Highlights

| Fähigkeit | Beschreibung |
|:---------|------|
| **RPC-Kern** | SRMP-Protokoll, JSON-/Binär-Codecs, Request-Pipeline, Client/Server, Cluster-Verwaltung, Metadaten, Retry-Policy, durchgehend asynchroner Server, TraceId-Tracing |
| **STRM-Streaming** | Server-Streaming: `IAsyncEnumerable<T>` elementweise über SRMP und SSE |
| **CLNT-Clientbasis** | Login/Heartbeat/Update/Kommandos/Ereignisse an einer Stelle, automatischer WS/SSE-Wechsel |
| **UPGD-Auto-Update** | Erkennen → Herunterladen → Prüfen → Entpacken → Überschreiben → Neustart |
| **SRV-Serverbasis** | ApiController/BaseController/BaseDeviceController, TokenService, IDeviceService; dasselbe Endpunktprotokoll als MVC und als Minimal-API (spiegelgleiche Implementierungen, durch Tests abgesichert) |
| **CMD-Kommandos** | SessionManager + Event-Bus-Broadcast + WS/SSE/Ping-Kanäle |
| **L10N-Lokalisierung** | Laufzeitmeldungen mit Englisch als Quelltext; `Locales/*.yml` liefern 31 integrierte Sprachen (inklusive vereinfachtem und traditionellem Chinesisch), fehlende Einträge fallen auf Englisch zurück; kein ResourceManager, keine Satellitenassemblys |
| **AOT-Unterstützung** | Permanente AOT-Analyse ab net8.0 (0 Warnungen in Kernbibliothek und Extensions) + eigener Source-Generator (Dispatch-Tabelle, Stream-Pumps, Signaturerhalt); NativeAOT geprüft für <!--aot-scenarios:start-->23<!--aot-scenarios:end--> Szenarien, 11 Minimal-API-Szenarien und 6 XCode-Geräteszenarien (siehe AOT-Dokument §8) |

## Schnellstart

**Fünf Zeilen Server, fünf Zeilen Client – RPC läuft:**

```csharp
// === Server ===
var server = new ApiServer(12345);
server.Register<MyController>();
server.Start();

public class MyController : ApiController
{
    public String Ping(String name) => $"Hello {name}, {DateTime.Now:HH:mm:ss}";
    // Streaming: IAsyncEnumerable<T> wird elementweise gesendet
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

// Normaler Aufruf
var rs = await client.InvokeAsync<String>("My/Ping", new { name = "dev" });

// Streaming-Aufruf – Empfang der Server-Pushes
await foreach (var log in client.InvokeStreamAsync<String>("My/StreamLogs", new { count = 10 }))
    Console.WriteLine(log);

// Metadaten (TraceId wird automatisch gesetzt)
client.Headers["TenantId"] = "tenant-01";
```

## Leistungsdaten

| Kennzahl | Typisch | Labor-Spitze |
|:----|:------:|:--------:|
| TCP-Langzeitverbindungen | 10.000 | 4.000.000 |
| RPC-Durchsatz | 100.000 TPS | 22.660.000 TPS |
| Latenz pro RPC | ~41 μs | — |
| Zielframeworks | net45 / net461 / netstandard2.0 / netstandard2.1 / net6.0 / net8.0 / net10.0 (abgestimmt auf NewLife.Core v12; NativeAOT ab net8.0) |

---
## Zusammenspiel mit anderen NewLife-Komponenten

| Komponente | Nutzen |
| ---- | -------- |
| NewLife.Core | Logging, Konfiguration, Serialisierung, Lokalisierung, Objektpool, TimerX, Tracing |
| NewLife.Net | Hochperformanter Netzwerk-Stack, WebSocket/Codecs, Port-Wiederverwendung |
| NewLife.Redis | Verteilter Cache / Message-Queue für geteilte Sessions oder Push-Fanout |
| Stardust | Knotenregistrierung, Config-Center, APM, Release-Center |
| AntJob | Job-Scheduling und verteiltes Rechnen über ClientBase |
| NewLife.MQTT / Modbus / IoT | Geräteprotokolle erfassen und über Remoting senden |

## Beiträge und Feedback

Willkommen: Bug-Reports, Performance-PRs, neue Protokoll-Codecs, Controller-Beispiele, Dokumentation und neue Übersetzungen.
Bitte im Issue angeben: Laufzeitumgebung, Zielframework, minimale Reproduktion und Logauszug.

---
## NewLife-Projektmatrix

Alle Projekte unterstützen standardmäßig net10.0/net8.0/netstandard2.1/netstandard2.0/net4.62/net4.5; die Linie 2024.0801 zusätzlich net4.0/net2.0.

| Projekt | Jahr | Beschreibung |
| :--: | :--: | ---- |
| [NewLife.Core](https://github.com/NewLifeX/X) | 2002 | Kernbibliothek: Logging, Konfiguration, Cache, Netzwerk, Serialisierung, APM |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | 2005 | Daten-Middleware: zig Milliarden Zeilen pro Tabelle, Sharding, Lese-/Schreibtrennung |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 2005 | Netzwerkbibliothek: 22,66 Mio. tps / Millionen Verbindungen |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 2011 | Dieses Projekt: Remoting mit zwei Protokollen |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 2010 | Rapid-Development-Plattform: Rechte / OAuth / SSO |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | 2008 | Dienstinstallation / Daemon / Systemd |
| [NewLife.Zero](https://github.com/NewLifeX/NewLife.Zero) | 2020 | Projektvorlagen |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | 2017 | Hochperformanter Redis-Client / Message-Queue |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 2018 | Vollständig verwalteter RocketMQ-Client |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | 2019 | IoT MqttClient/MqttServer |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | 2022 | Standardmodelle für IoT-Kommunikation |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | 2022 | Modbus-Protokollfamilie |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | 2022 | Siemens-PLC-Kommunikation |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 2022 | Karten-Aggregationskomponenten |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | 2023 | Audio-Codecs |
| Stardust | 2018 | Verteilte Serviceplattform (Registry/Config/APM/Release) |
| AntJob | 2019 | Verteilte Big-Data-Plattform |
| Weitere | - | Siehe offizielle Website |

---
## Team und Urheberrecht

![XCode](https://newlifex.com/logo.png)

NewLife wurde 2002 gegründet, umfasst über 80 Open-Source-Projekte und mehr als 4 Millionen NuGet-Downloads. Alle Projekte stehen unter MIT-Lizenz und dürfen frei verändert und weitergegeben werden (keine Namensnennung nötig).  
Website: https://newlifex.com  
GitHub: https://github.com/NewLifeX  
QQ-Gruppen: 1600800 / 1600838  

> `NewLife startete 2002; einige Projekte sind über 20 Jahre alt, die Repositories bewahren alle Commits seit 2010.`

---
## Lizenz

MIT © NewLife. Kommerzielle Nutzung und Weiterentwicklung sind willkommen.
