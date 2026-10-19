# NewLife.Remoting — 統合リモーティングフレームワーク (RPC + HTTP + WebSocket + SRMP)

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
[![locales](https://img.shields.io/badge/locales-2-2ea44f?logo=dotnet)](Doc/多语言本地化接入.md)
<!-- metrics:end -->
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-blue)
![arch](https://img.shields.io/badge/arch-x64%20%7C%20arm64%20%7C%20loongarch64-orange)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/NewLife.Remoting?logo=github)](https://github.com/NewLifeX/NewLife.Remoting/graphs/contributors)

<!-- lang:start -->
🌐 [中文](Readme.MD) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

NewLife.Remoting は NewLife スタックの統合リモーティング基盤で、2 つの NuGet パッケージで構成されます。

| パッケージ | 役割 | 範囲 |
|:---|:-----|:------|
| **NewLife.Remoting** | コア通信ライブラリ | RPC クライアント/サーバー、プロトコルコーデック、接続管理、ストリーミング呼び出し、クライアント基底クラス、コマンド配信パイプライン、自動更新 |
| **NewLife.Remoting.Extensions** | 拡張ライブラリ | サーバーコントローラー基底クラス、Minimal API ホスト、デバイス管理サービス、トークン認証、コマンド応答バス |

ソース: https://github.com/NewLifeX/NewLife.Remoting  
> NuGet: `NewLife.Remoting` / `NewLife.Remoting.Extensions`

> ⚠️ 本翻訳は[中国語版](Readme.MD)より遅れる場合があります。中国語版が正式です。

## ドキュメント索引

| ドキュメント | 説明 |
|------|------|
| [Doc/需求文档.md](Doc/需求文档.md) | 要件定義：ビジョン、目標、機能要件（RPC/STRM/CLNT/UPGD/SRV/CMD/AOT/L10N/EXT） |
| [Doc/架构设计.md](Doc/架构设计.md) | アーキテクチャ：レイヤー、主要モジュール、メモリ管理、テスト戦略、設計判断 |
| [Doc/SRV-服务端架构.md](Doc/SRV-服务端架构.md) | サーバー業務アーキテクチャ：サービス層、主要フロー（ログイン/登録/ログアウト/ハートビート/更新/イベント/コマンド）、オンラインセッション、クラスタ運用 |
| [Doc/功能清单.md](Doc/功能清单.md) | 機能追跡：状態、進行中項目、バックログ |
| [Doc/竞品分析.md](Doc/竞品分析.md) | 競合分析：RPC フレームワーク比較と IoT コマンド配信の比較 |
| [Doc/SRMP.MD](Doc/SRMP.MD) | SRMP プロトコル仕様 |
| [Doc/RPC内存管理设计.md](Doc/RPC内存管理设计.md) | ゼロコピーのメモリ管理 |
| [Doc/自动更新机制.md](Doc/自动更新机制.md) | 自動更新の全体設計 |
| [Doc/重试策略.md](Doc/重试策略.md) | 再試行ポリシー：`IRetryPolicy` と指数バックオフ |
| [Doc/AOT兼容性分析.md](Doc/AOT兼容性分析.md) | NativeAOT の対応範囲、<!--aot-scenarios:start-->23<!--aot-scenarios:end--> シナリオ実測（スモーク <!--aot-smoke:start-->23/23<!--aot-smoke:end-->）、利用指針と既知の境界 |
| [Doc/多语言本地化接入.md](Doc/多语言本地化接入.md) | 実行時メッセージの英語化、`Locales/*.yml` と `L.T` の導入ガイド |
| [Doc/README多语言规范.md](Doc/README多语言规范.md) | README 11 言語の順序規則、生成スクリプト、CI ゲート |

---

## 特長

| 機能 | 説明 |
|:---------|------|
| **RPC コア** | SRMP プロトコル、JSON/バイナリコーデック、要求パイプライン、クライアント/サーバー、クラスタ接続管理、メタデータ伝播、再試行、サーバー全経路の非同期化、TraceId 追跡 |
| **STRM ストリーミング** | サーバーストリーミング：`IAsyncEnumerable<T>` を SRMP と SSE で逐次配信 |
| **CLNT クライアント基底** | ログイン/ハートビート/更新/コマンド/イベントを一括提供、WS/SSE を自動切替 |
| **UPGD 自動更新** | 検出 → ダウンロード → 検証 → 展開 → 上書き → 再起動 |
| **SRV サーバー基底** | ApiController/BaseController/BaseDeviceController、TokenService、IDeviceService。同一エンドポイントを MVC と Minimal API の両方で提供（ミラー実装をテストで固定） |
| **CMD コマンド配信** | SessionManager + イベントバスブロードキャスト + WS/SSE/Ping チャネル |
| **L10N 多言語** | 実行時メッセージは英語を原文とし、`Locales/*.yml` が簡体字・繁体字中国語を提供、未訳は英語へフォールバック。ResourceManager とサテライトアセンブリ不要 |
| **AOT 対応** | net8.0+ で AOT 解析を常時有効（コアライブラリ 0 警告 / Extensions 0 警告）+ 独自ソースジェネレーター（アクション分離表、ストリームポンプ、シグネチャ型保持）。コア <!--aot-scenarios:start-->23<!--aot-scenarios:end--> シナリオ、Extensions Minimal API 11 シナリオ、XCode デバイス経路 6 シナリオを NativeAOT で実測（AOT ドキュメント §8） |

## クイックスタート

**サーバー 5 行、クライアント 5 行で RPC が動作します。**

```csharp
// === サーバー ===
var server = new ApiServer(12345);
server.Register<MyController>();
server.Start();

public class MyController : ApiController
{
    public String Ping(String name) => $"Hello {name}, {DateTime.Now:HH:mm:ss}";
    // ストリーミング：IAsyncEnumerable<T> を返すと逐次配信される
    public async IAsyncEnumerable<String> StreamLogs(Int32 count)
    {
        for (var i = 0; i < count; i++)
        {
            await Task.Delay(100);
            yield return $"Log #{i}: {DateTime.Now:HH:mm:ss}";
        }
    }
}

// === クライアント ===
var client = new ApiClient("tcp://127.0.0.1:12345");
client.Open();

// 通常呼び出し
var rs = await client.InvokeAsync<String>("My/Ping", new { name = "dev" });

// ストリーミング呼び出し — サーバーの配信を逐次受信
await foreach (var log in client.InvokeStreamAsync<String>("My/StreamLogs", new { count = 10 }))
    Console.WriteLine(log);

// メタデータ伝播（TraceId は自動注入）
client.Headers["TenantId"] = "tenant-01";
```

## 性能指標

| 指標 | 標準値 | 実験ピーク |
|:----|:------:|:--------:|
| TCP 常時接続数 | 10,000 | 4,000,000 |
| RPC スループット | 100,000 TPS | 22,660,000 TPS |
| RPC あたりの遅延 | 約 41 μs | — |
| 対応フレームワーク | net45 / net461 / netstandard2.0 / netstandard2.1 / net6.0 / net8.0 / net10.0（NewLife.Core v12 に追随、net8.0+ で NativeAOT） |

---
## 他の NewLife コンポーネントとの連携

| コンポーネント | 連携価値 |
| ---- | -------- |
| NewLife.Core | ログ、設定、シリアル化、多言語、オブジェクトプール、TimerX、性能追跡 |
| NewLife.Net | 高性能ネットワークスタック、WebSocket/コーデック、ポート再利用 |
| NewLife.Redis | 分散キャッシュ / メッセージキュー、セッション共有やプッシュ fanout |
| Stardust | ノード登録、設定センター、APM、リリースセンター |
| AntJob | ClientBase 経由でジョブスケジューリングと分散処理 |
| NewLife.MQTT / Modbus / IoT | デバイス層プロトコルを収集し Remoting で上り送信 |

## コントリビュートとフィードバック

歓迎：バグ報告 / 性能改善 PR / 新しいプロトコルコーデック / コントローラー例 / ドキュメント追記 / 新言語の翻訳。
Issue には実行環境、ターゲットフレームワーク、最小再現コード、ログ抜粋を添えてください。

---
## 新生命プロジェクトマトリクス

各プロジェクトは既定で net10.0/net8.0/netstandard2.1/netstandard2.0/net4.62/net4.5 に対応し、2024.0801 系は net4.0/net2.0 にも対応します。

| プロジェクト | 年 | 説明 |
| :--: | :--: | ---- |
| [NewLife.Core](https://github.com/NewLifeX/X) | 2002 | コアライブラリ：ログ、設定、キャッシュ、ネットワーク、シリアル化、APM |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | 2005 | データミドルウェア：単表数百億行、自動分割、読み書き分離 |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 2005 | ネットワークライブラリ：2,266 万 tps / 数百万接続 |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 2011 | 本プロジェクト：デュアルプロトコルのリモーティング |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 2010 | 高速開発プラットフォーム：権限 / OAuth / SSO |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | 2008 | サービスインストール / デーモン / Systemd |
| [NewLife.Zero](https://github.com/NewLifeX/NewLife.Zero) | 2020 | プロジェクトテンプレート集 |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | 2017 | 高性能 Redis クライアント / メッセージキュー |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 2018 | フルマネージド RocketMQ クライアント |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | 2019 | IoT 向け MqttClient/MqttServer |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | 2022 | IoT 通信標準モデル |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | 2022 | Modbus プロトコル群 |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | 2022 | Siemens PLC 通信 |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 2022 | 地図集約コンポーネント |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | 2023 | 音声コーデック |
| Stardust | 2018 | 分散サービス基盤（登録/設定/APM/リリース） |
| AntJob | 2019 | 分散ビッグデータ計算基盤 |
| ほか | - | 公式サイトを参照 |

---
## チームと著作権

![XCode](https://newlifex.com/logo.png)

NewLife は 2002 年設立、80 以上のオープンソースプロジェクト、NuGet 累計ダウンロード 400 万件超。すべて MIT ライセンスで、自由に改変・再配布できます（出典表示不要）。  
ウェブサイト: https://newlifex.com  
GitHub: https://github.com/NewLifeX  
QQ グループ: 1600800 / 1600838  

> `NewLife は 2002 年に始まり、20 年以上の歴史を持つプロジェクトもあり、リポジトリは 2010 年以降の全履歴を保持しています。`

---
## ライセンス

MIT © NewLife。商用利用と二次開発を歓迎します。
