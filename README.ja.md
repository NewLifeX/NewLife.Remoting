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
[![locales](https://img.shields.io/badge/locales-31-2ea44f?logo=dotnet)](Doc/多语言本地化接入.md)
<!-- metrics:end -->
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS%20%7C%20Android%20%7C%20iOS-blue)
![arch](https://img.shields.io/badge/arch-x64%20%7C%20arm64%20%7C%20loongarch64%20%7C%20riscv64%20%7C%20s390x-orange)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/NewLife.Remoting?logo=github)](https://github.com/NewLifeX/NewLife.Remoting/graphs/contributors)

<!-- lang:start -->
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

NewLife.Remoting は NewLife スタックの統合リモーティング基盤で、2 つの NuGet パッケージで構成されます。

| パッケージ | 役割 | 範囲 |
|:---|:-----|:------|
| **NewLife.Remoting** | コア通信ライブラリ | RPC クライアント/サーバー、プロトコルコーデック、接続管理、ストリーミング呼び出し、クライアント基底クラス、コマンド配信パイプライン、自動更新 |
| **NewLife.Remoting.Extensions** | 拡張ライブラリ | サーバーコントローラー基底クラス、Minimal API ホスト、デバイス管理サービス、トークン認証、コマンド応答バス |

ソース: https://github.com/NewLifeX/NewLife.Remoting  
> NuGet: `NewLife.Remoting` / `NewLife.Remoting.Extensions`

> ⚠️ 本翻訳は更新が遅れる場合があります。正式な内容は[英語 README](README.md)を参照してください。

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
| **L10N 多言語** | 実行時メッセージは英語を原文とし、`Locales/*.yml` に 31 言語を内蔵（簡体字・繁体字中国語を含む）、未訳は英語へフォールバック。ResourceManager とサテライトアセンブリ不要 |
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
| RPC スループット | 100,000 TPS | 455,000 msg/s |
| RPC あたりの遅延 | 約 41 μs | — |
| 対応フレームワーク | net45 / net461 / netstandard2.0 / netstandard2.1 / net6.0 / net8.0 / net10.0（NewLife.Core v12 に追随、net8.0+ で NativeAOT） |

## v4 へのアップグレード

v4 は NewLife.Core v12 に追随し、ネットワーク層を変更します。アップグレード前にご確認ください：

- **デュアルポート**：メインポートは SRMP 専用です。HTTP を有効にするには `HttpPort` を明示的に設定してください（既定では無効）。
- **WebSocket 上の SRMP を廃止**：`ws://`/`wss://` アドレスは `NotSupportedException` をスローします。SRMP には `tcp://`/`udp://`、HTTP 呼び出しには `http(s)://` を使用してください。
- **独自コーデック型を削除**：`WsClient`、`HttpCodec`、`WebSocketClientCodec`、`WebSocketServerCodec`、`HttpMessage` のフレームメソッドは削除されました。HTTP は Core の `HttpServer` が処理し、独自フレーミングは `IMessageCodec` を実装してください。
- **英語のメッセージキー**：実行時メッセージは英語原文をキーにします。ローカライズ済み文字列ではなく `ApiCode` またはエラーコードで判定してください。
- **ターゲットフレームワーク**：net5.0/net7.0/net9.0 のアセットは提供されなくなり、NuGet は最も近いサポート対象フレームワークへ自動的にフォールバックします。

完全な一覧は [ChangeLog.md](ChangeLog.md) を参照してください。

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
## エコシステムマップ

NewLife は、独立したバージョン管理を行うオープンソースプロジェクトの階層型エコシステムです。以下のマップはすべての NewLife リポジトリで逐語的に同一の内容を共有しており、各プロジェクトはコードリポジトリと主要な NuGet パッケージにリンクしています。

| プロジェクト | 概要 | ダウンロード数 |
| --- | --- | --- |
| **基盤** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | ログ、構成、キャッシュ、ネットワーク、シリアライズ、スケジューリング、APM トレーシング。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | データミドルウェア：単一テーブルで数百億件級、自動シャーディング、読み書き分離、10 種類以上のデータベースに対応。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 高性能ネットワークライブラリ：単一マシンで 177 万 pps、14.5 Gbps（TCP 1KB、2026-10 実測）、400 万 Tcp 長接続。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | CS アプリケーションと IoT デバイス向けのプロトコル通信と RPC フレームワーク。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | .NET アプリケーションを OS デーモンとしてインストール：Windows サービスと Linux systemd。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | コンソール、Web、API、サービス、データ、IoT 向けの `dotnet new` プロジェクトテンプレート。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **データとメッセージング** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | マイクロ秒級の遅延、数百万 ops/s のスループット、メッセージキュー内蔵の Redis クライアント。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | Apache RocketMQ と Alibaba Cloud Message Queue に対応した純マネージド RocketMQ クライアント。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | MQTT クライアントとサーバー。Alibaba Cloud IoT への接続を含みます。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | C# 製ハイブリッドデータベース：組み込み/ネットワークの両モード、リレーショナル、時系列、メッセージキュー、KV を 1 つに。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | NewLife データスタック上に構築された MySql ドライバーとヘルパー層。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web、AI、分散** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 高速管理プラットフォームフレームワーク：権限、SSO、OAuth サーバー、CRUD スキャフォールディング。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | 分散サービスプラットフォーム：ノード管理、APM、構成センター、登録センター、リリースセンター。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | シャーディング、リトライ、バックフィルに対応した分散タスクとリアルタイムデータのスケジューリングプラットフォーム。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | 46 社の LLM プロバイダーに統一接続するクライアントと、すぐに使える Web チャットアプリ ChatAI。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **産業と IoT** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | プロトコル契約とデバイス抽象を定義する IoT 標準ライブラリ。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | NewLife.IoT 上に実装された Modbus TCP/RTU/ASCII。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | NewLife.IoT 上に実装されたシーメンス PLC プロトコル。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | NewLife.IoT 上に実装されたオムロン PLC プロトコル。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | NewLife.IoT 上に実装された三菱 Melsec PLC プロトコル。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | 産業データアクセス向けの OPC クライアント。 | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | BACnet ビル管理プロトコルスタック。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | ゲートウェイとデバイス管理を備えた軽量なオープンソース IoT プラットフォーム。 | — |
| **車載テレマティクス** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | JT/T 808 車両位置情報プロトコル：70 以上のメッセージボディ、2011/2013/2019 に対応。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | 運輸管理部門向けの JT/T 809 プラットフォーム間プロトコル。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | JT/T 1078 車載ビデオ：RTP 解析、FLV/fMP4 リマックス、ライブ配信。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **ドメインコンポーネント** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | Excel、Word、PPT、PDF など 15 以上のドキュメント形式を 1 パッケージで。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | クロスプラットフォーム音声：コーデック、デバイス IO、DSP、コンテナ、ストリーミング。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | ビデオコーデック、コンテナ形式、フレーム処理、ストリーミング。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 百度地図、高徳地図、テンセント地図、天地図を統一インターフェイス化。ジオコーディングと座標変換に対応。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | IPv4 位置情報：オフラインのメモリマップデータベースとオンライン API のフォールバック。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **ツールと製品** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | 開発者向けツールボックス：ネットワーク、シリアルポート、暗号化、正規表現、Modbus、MQTT。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | セルフホスト型 Git ソースコード管理システム。 | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | 古いホストから起動した際に対応する .NET ランタイムを自動インストールします。 | — |

> バッジは各プロジェクトの主要パッケージを示します。テーマ、拡張、サテライトパッケージは各 NuGet ページを参照してください。ここに記載していない 30 以上のパッケージ（FTP、LoRa、Office 拡張、各種プロトコルドライバーなど）は [NewLifeX 組織](https://github.com/NewLifeX) と [newlifex.com](https://newlifex.com) で公開しています。

---
## 新生命開発チーム

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

新生命チームは 2002 年に設立され、20 年以上にわたり .NET と IoT の技術エコシステムに注力し、100 近いオープンソースプロジェクトを主導し（うち 34 が主要プロジェクト）、電力、物流、医療、産業制御、インターネットなどの業界にソフトウェアとハードウェアを一体化したソリューションを提供しています。「学問に先後なし、達する者が師である」を信条としています。

オープンソースコンポーネントは NuGet で累計 <!--nuget-total:start-->6.48M<!--nuget-total:end--> 回ダウンロードされています（エコシステム全体、2026-10 時点）。

ウェブサイト：https://newlifex.com · オープンソース組織：https://github.com/NewLifeX

<details>
<summary>WeChat 公式アカウント：智能大石头（スキャンしてフォロー）</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

---
## ライセンス

MIT © NewLife。商用利用と二次開発を歓迎します。
