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
| RPC 輸送量 | 100,000 TPS | 22,660,000 TPS |
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
## 新生命專案矩陣

各專案預設支援 net10.0/net8.0/netstandard2.1/netstandard2.0/net4.62/net4.5，舊版（2024.0801）支援 net4.0/net2.0

| 專案 | 年份 | 說明 |
| :--: | :--: | ---- |
| [NewLife.Core](https://github.com/NewLifeX/X) | 2002 | 核心程式庫，日誌、設定、快取、網路、序列化、APM 效能追蹤 |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | 2005 | 大數據中介軟體，單表百億級，自動分表，讀寫分離 |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 2005 | 網路程式庫，2266 萬 tps / 百萬連線 |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 2011 | 本專案，雙協定通訊框架 |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 2010 | 快速開發平台，權限 / OAuth / SSO |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | 2008 | 服務安裝 / 守護行程 / Systemd |
| [NewLife.Zero](https://github.com/NewLifeX/NewLife.Zero) | 2020 | 專案範本集合 |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | 2017 | 高效能 Redis 用戶端/訊息佇列 |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 2018 | 純受控 RocketMQ 用戶端 |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | 2019 | 物聯網 MqttClient/MqttServer |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | 2022 | IoT 通訊標準模型庫 |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | 2022 | Modbus 協定族 |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | 2022 | 西門子 PLC 通訊 |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 2022 | 地圖聚合元件 |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | 2023 | 音訊編解碼 |
| Stardust | 2018 | 分散式服務平台（註冊/設定/APM/發佈） |
| AntJob | 2019 | 分散式大數據運算平台 |
| 等等 | - | 更多請見官網 |

---
## 團隊與版權

![XCode](https://newlifex.com/logo.png)

新生命團隊（NewLife）成立於 2002 年，80+ 開源專案，NuGet 累計下載 400+ 萬。全部專案採用 MIT 協定，可自由修改再發行（無需聲明來源）。  
網站：https://newlifex.com  
開源：https://github.com/NewLifeX  
QQ 群：1600800 / 1600838  

> `新生命團隊始於 2002 年，部分開源專案具有 20 年以上歷史，原始碼庫保留 2010 年以來全部記錄。`

---
## License

MIT © NewLife. 歡迎商用與二次開發。
