# NewLife.Remoting — 統一遠端通訊框架 (RPC + HTTP + WebSocket + SRMP)

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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

NewLife.Remoting 是 NewLife 技術棧的統一遠端通訊基礎設施，由兩個 NuGet 套件組成：

| 套件 | 定位 | 涵蓋範圍 |
|:---|:-----|:------|
| **NewLife.Remoting** | 核心通訊程式庫 | RPC 用戶端/伺服器、協定編解碼、連線管理、串流呼叫、用戶端基底類別、指令下發管線、自動更新 |
| **NewLife.Remoting.Extensions** | 擴充程式庫 | 伺服器控制器基底類別、最小 API 裝載、裝置管理服務、Token 驗證、命令回應匯流排 |

原始碼：https://github.com/NewLifeX/NewLife.Remoting  
> NuGet：`NewLife.Remoting` / `NewLife.Remoting.Extensions`

> ⚠️ 本翻譯可能落後於[英文 README](README.md)，內容以英文版為準。

## 文件索引

| 文件 | 說明 |
|------|------|
| [Doc/需求文档.md](Doc/需求文档.md) | 需求定義：願景、核心目標、功能需求（RPC/STRM/CLNT/UPGD/SRV/CMD/AOT/L10N/EXT） |
| [Doc/架构设计.md](Doc/架构设计.md) | 架構設計：分層概觀、核心模組架構、記憶體管理、測試策略、設計決策 |
| [Doc/SRV-服务端架构.md](Doc/SRV-服务端架构.md) | 伺服器業務架構：服務層設計、核心流程（登入/註冊/登出/心跳/升級/事件上報/指令下發）、線上工作階段生命週期、叢集部署 |
| [Doc/功能清单.md](Doc/功能清单.md) | 功能追蹤：主功能狀態、進行中項目與待辦（發版待辦/待修復項） |
| [Doc/竞品分析.md](Doc/竞品分析.md) | 競品分析：RPC 框架比較 + IoT 指令下發比較矩陣 |
| [Doc/SRMP.MD](Doc/SRMP.MD) | SRMP 協定規範 |
| [Doc/RPC内存管理设计.md](Doc/RPC内存管理设计.md) | 零複製記憶體管理 |
| [Doc/自动更新机制.md](Doc/自动更新机制.md) | 自動更新全鏈路設計 |
| [Doc/重试策略.md](Doc/重试策略.md) | 重試策略：`IRetryPolicy` 介面與指數退避範例 |
| [Doc/AOT兼容性分析.md](Doc/AOT兼容性分析.md) | NativeAOT 支援範圍、<!--aot-scenarios:start-->23<!--aot-scenarios:end--> 場景實測（煙霧 <!--aot-smoke:start-->23/23<!--aot-smoke:end-->）、下游使用指引與已知邊界 |
| [Doc/多语言本地化接入.md](Doc/多语言本地化接入.md) | 執行階段訊息英文化、`Locales/*.yml` 詞表與 `L.T` 接入指引 |
| [Doc/README多语言规范.md](Doc/README多语言规范.md) | README 11 種語言的排序規則、產生腳本與 CI 門檻 |

---

## 亮點

| 核心能力 | 說明 |
|:---------|------|
| **RPC 核心框架** | SRMP 協定、JSON/二進位編解碼、請求處理管線、用戶端/伺服器、Cluster 連線管理、中繼資料傳遞、重試策略、伺服器全鏈路非同步化、TraceId 呼叫鏈追蹤 |
| **STRM 串流呼叫** | Server-Streaming：`IAsyncEnumerable<T>` 逐筆推送，SRMP + SSE 雙協定 |
| **CLNT 用戶端基底** | 登入/心跳/升級/指令/事件一站式封裝，WS/SSE 雙通道自動切換 |
| **UPGD 自動更新** | 偵測→下載→驗證→解壓縮→覆寫→重新啟動全鏈路 |
| **SRV 伺服器基礎** | ApiController/BaseController/BaseDeviceController、TokenService、IDeviceService；同一端點協定提供 MVC 與最小 API 兩套裝載入口（鏡像實作、測試鎖定一致） |
| **CMD 指令下發** | SessionManager + 事件匯流排廣播 + WS/SSE/Ping 多通道 |
| **L10N 多語言** | 執行階段訊息以英文為原文，`Locales/*.yml` 詞表內建 31 種語言（含簡體/繁體中文），缺譯文自動回退英文；不依賴 ResourceManager 與附屬組件 |
| **AOT 支援** | net8.0+ 常駐 AOT 相容分析（核心程式庫 0 警告 / Extensions 0 警告）+ 自有來源產生器（動作分派表/串流幫浦/簽章型別保留），核心程式庫 <!--aot-scenarios:start-->23<!--aot-scenarios:end--> 場景 + Extensions 最小 API 裝載 11 場景 + XCode 裝置服務鏈路 6 場景 NativeAOT 實測通過（見 AOT 文件 §8） |

## 快速開始

**5 行伺服器，5 行用戶端，立即跑通 RPC 呼叫：**

```csharp
// === 伺服器 ===
var server = new ApiServer(12345);
server.Register<MyController>();
server.Start();

public class MyController : ApiController
{
    public String Ping(String name) => $"Hello {name}, {DateTime.Now:HH:mm:ss}";
    // 串流推送：回傳 IAsyncEnumerable<T> 自動逐筆下發
    public async IAsyncEnumerable<String> StreamLogs(Int32 count)
    {
        for (var i = 0; i < count; i++)
        {
            await Task.Delay(100);
            yield return $"Log #{i}: {DateTime.Now:HH:mm:ss}";
        }
    }
}

// === 用戶端 ===
var client = new ApiClient("tcp://127.0.0.1:12345");
client.Open();

// 一般呼叫
var rs = await client.InvokeAsync<String>("My/Ping", new { name = "dev" });

// 串流呼叫 — 逐筆接收伺服器推送
await foreach (var log in client.InvokeStreamAsync<String>("My/StreamLogs", new { count = 10 }))
    Console.WriteLine(log);

// 中繼資料傳遞（自動注入 TraceId）
client.Headers["TenantId"] = "tenant-01";
```
## 效能指標

| 指標 | 典型值 | 實驗峰值 |
|:----|:------:|:--------:|
| TCP 長連線數 | 10,000 | 4,000,000 |
| RPC 輸送量 | 100,000 TPS | 455,000 msg/s |
| 單次 RPC 延遲 | ~41 μs | — |
| 框架相容 | net45 / net461 / netstandard2.0 / netstandard2.1 / net6.0 / net8.0 / net10.0（跟隨 NewLife.Core v12，net8.0+ 支援 NativeAOT） |

---
## 與其他 NewLife 元件協同

| 元件 | 協同價值 |
| ---- | -------- |
| NewLife.Core | 日誌、設定、序列化、多語言、物件池、TimerX、效能追蹤 |
| NewLife.Net | 高效能網路堆疊、WebSocket/編碼器、埠重用 |
| NewLife.Redis | 分散式快取 / 訊息佇列，擴充工作階段共享或推送 fanout |
| Stardust | 節點註冊、設定中心、APM、發佈中心；可託管 ApiServer 節點 |
| AntJob | 使用 ClientBase 接入任務排程與分散式運算 |
| NewLife.MQTT / Modbus / IoT | 裝置層協定採集後經 Remoting 上行 |

## 貢獻與回饋

歡迎：Bug 回報 / 效能最佳化 PR / 新協定編解碼 / 控制器範例 / 文件補充 / 新語系翻譯。
Issue 討論請盡量提供：執行環境、目標框架、最小可重現程式碼、日誌摘要。

---
## 生態版圖

NewLife 生態系按相依分層，各專案獨立版本、獨立發行。下面的版圖在所有 NewLife 儲存庫中逐字一致；每個專案連結到程式碼儲存庫與主 NuGet 套件。

| 專案 | 一句話定位 | 下載量 |
| --- | --- | --- |
| **基礎底座** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | 日誌、設定、快取、網路、序列化、排程與 APM 追蹤。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | 資料中介層：單表百億級、自動分表、讀寫分離，支援 10 多種資料庫。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 高效能網路庫：單機 177 萬 pps、14.5 Gbps（TCP 1KB，2026-10 實測），400 萬 Tcp 長連線。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 協定通訊與 RPC 框架，面向 CS 應用程式與物聯網裝置。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | 將 .NET 應用程式安裝為作業系統常駐服務：Windows 服務與 Linux systemd。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | `dotnet new` 專案範本：主控台、Web、API、服務、資料與物聯網。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **資料儲存與訊息** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Redis 用戶端：微秒級延遲、百萬級吞吐，內建訊息佇列。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 純受控 RocketMQ 用戶端，支援 Apache RocketMQ 與阿里雲訊息佇列。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | MQTT 用戶端與服務端，含阿里雲物聯網接入。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | C# 混合式資料庫：內嵌或網路雙模，關係型、時序、訊息佇列與 KV 於一體。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | 建構於 NewLife 資料堆疊之上的 MySql 驅動與輔助層。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web、AI 與分散式** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 快速後台開發框架：權限、SSO、OAuth 服務端與 CRUD 樣板。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | 分散式服務平台：節點管理、APM、設定中心、註冊中心與發佈中心。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | 分散式任務與即時資料排程平台，支援分片、重試與回溯補算。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | 統一接入 46 家大模型服務商，附帶開箱即用的 ChatAI Web 對話應用。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **工業與物聯網** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | 物聯網標準庫，定義通訊協定契約與裝置抽象。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | 建構於 NewLife.IoT 之上的 Modbus TCP/RTU/ASCII 實作。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | 建構於 NewLife.IoT 之上的西門子 PLC 協定。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | 建構於 NewLife.IoT 之上的歐姆龍 PLC 協定。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | 建構於 NewLife.IoT 之上的三菱 Melsec PLC 協定。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | 工業資料存取的 OPC 用戶端。 | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | BACnet 樓宇自動化協定堆疊。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | 開源輕量級物聯網平台，含閘道與裝置管理。 | — |
| **車聯網** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | JT/T 808 車輛定位協定：70+ 訊息體，涵蓋 2011/2013/2019 三個版本。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | JT/T 809 部標平台互聯協定，面向運輸管理部門。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | JT/T 1078 車載視訊：RTP 解析、FLV/fMP4 轉封裝與直播推送。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **領域元件** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | 一個套件涵蓋 Excel、Word、PPT、PDF 等 15+ 文件格式。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | 跨平台音訊：編解碼、裝置 IO、DSP、容器與串流處理。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | 視訊編解碼、封裝格式、影格處理與串流。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 百度／高德／騰訊／天地圖統一介面，支援地理編碼與座標轉換。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | IPv4 歸屬地：離線記憶體映射資料庫 + 線上 API 雙來源。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **工具與產品** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | 開發者工具箱：網路、序列埠、加解密、正規表示式、Modbus 與 MQTT。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | 自架 Git 原始碼管理系統。 | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | 從較舊的宿主啟動時，自動安裝相符的 .NET 執行階段。 | — |

> 徽章取各專案主套件；主題套件、擴充套件與衛星套件見對應 NuGet 頁面。未在表中列出的 30 多個套件（FTP、LoRa、Office 擴充、各類協定驅動等）見 [NewLifeX 組織](https://github.com/NewLifeX) 與 [newlifex.com](https://newlifex.com)。

---
## 新生命開發團隊

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

新生命團隊成立於 2002 年，二十餘年專注 .NET 與物聯網技術生態，主導開源專案近百個（生態版圖收錄 34 個主幹專案），為電力、物流、醫療、工控、網際網路等行業提供軟硬體一體化解決方案；以「學無先後達者為師」為信條。

開源元件在 NuGet 累計下載 <!--nuget-total:start-->6.48M<!--nuget-total:end--> 次（全生態口徑，截至 2026-10）。

網站：https://newlifex.com · 開源組織：https://github.com/NewLifeX

<details>
<summary>微信公眾號：智能大石头（掃碼關注）</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

---
## License

MIT © NewLife. 歡迎商用與二次開發。
