# NewLife.Remoting — 통합 원격 통신 프레임워크 (RPC + HTTP + WebSocket + SRMP)

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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [العربية](README.ar.md)
<!-- lang:end -->

NewLife.Remoting은 NewLife 스택의 통합 원격 통신 기반으로, 두 개의 NuGet 패키지로 제공됩니다.

| 패키지 | 역할 | 범위 |
|:---|:-----|:------|
| **NewLife.Remoting** | 핵심 통신 라이브러리 | RPC 클라이언트/서버, 프로토콜 코덱, 연결 관리, 스트리밍 호출, 클라이언트 기본 클래스, 명령 파이프라인, 자동 업데이트 |
| **NewLife.Remoting.Extensions** | 확장 라이브러리 | 서버 컨트롤러 기본 클래스, Minimal API 호스트, 장치 관리 서비스, 토큰 인증, 명령 응답 버스 |

소스: https://github.com/NewLifeX/NewLife.Remoting  
> NuGet: `NewLife.Remoting` / `NewLife.Remoting.Extensions`

> ⚠️ 이 번역은 업데이트가 늦어질 수 있습니다. 정식 내용은 [영어 README](README.md)를 기준으로 합니다.

## 문서 색인

| 문서 | 설명 |
|------|------|
| [Doc/需求文档.md](Doc/需求文档.md) | 요구사항: 비전, 목표, 기능 요구사항 (RPC/STRM/CLNT/UPGD/SRV/CMD/AOT/L10N/EXT) |
| [Doc/架构设计.md](Doc/架构设计.md) | 아키텍처: 계층, 핵심 모듈, 메모리 관리, 테스트 전략, 설계 결정 |
| [Doc/SRV-服务端架构.md](Doc/SRV-服务端架构.md) | 서버 아키텍처: 서비스 계층, 핵심 흐름(로그인/등록/로그아웃/하트비트/업그레이드/이벤트/명령), 세션 수명 주기, 클러스터 배포 |
| [Doc/功能清单.md](Doc/功能清单.md) | 기능 추적: 상태, 진행 중 항목, 백로그 |
| [Doc/竞品分析.md](Doc/竞品分析.md) | 경쟁 분석: RPC 프레임워크 비교 및 IoT 명령 전송 비교 |
| [Doc/SRMP.MD](Doc/SRMP.MD) | SRMP 프로토콜 규격 |
| [Doc/RPC内存管理设计.md](Doc/RPC内存管理设计.md) | 제로 카피 메모리 관리 |
| [Doc/自动更新机制.md](Doc/自动更新机制.md) | 자동 업데이트 전체 설계 |
| [Doc/重试策略.md](Doc/重试策略.md) | 재시도 정책: `IRetryPolicy`와 지수 백오프 |
| [Doc/AOT兼容性分析.md](Doc/AOT兼容性分析.md) | NativeAOT 지원 범위, <!--aot-scenarios:start-->23<!--aot-scenarios:end--> 시나리오 실측(스모크 <!--aot-smoke:start-->23/23<!--aot-smoke:end-->), 활용 지침과 알려진 한계 |
| [Doc/多语言本地化接入.md](Doc/多语言本地化接入.md) | 런타임 메시지 영문화, `Locales/*.yml` 표와 `L.T` 적용 안내 |
| [Doc/README多语言规范.md](Doc/README多语言规范.md) | README 11개 언어의 정렬 규칙, 생성 스크립트, CI 게이트 |

---

## 주요 특징

| 기능 | 설명 |
|:---------|------|
| **RPC 핵심** | SRMP 프로토콜, JSON/바이너리 코덱, 요청 파이프라인, 클라이언트/서버, 클러스터 연결 관리, 메타데이터 전달, 재시도, 서버 전 구간 비동기화, TraceId 추적 |
| **STRM 스트리밍** | 서버 스트리밍: `IAsyncEnumerable<T>`를 SRMP와 SSE로 항목별 전송 |
| **CLNT 클라이언트 기본** | 로그인/하트비트/업그레이드/명령/이벤트를 한곳에서, WS/SSE 자동 전환 |
| **UPGD 자동 업데이트** | 감지 → 다운로드 → 검증 → 압축 해제 → 덮어쓰기 → 재시작 |
| **SRV 서버 기본** | ApiController/BaseController/BaseDeviceController, TokenService, IDeviceService; 동일 엔드포인트를 MVC와 Minimal API 두 방식으로 제공(미러 구현을 테스트로 고정) |
| **CMD 명령 전송** | SessionManager + 이벤트 버스 브로드캐스트 + WS/SSE/Ping 채널 |
| **L10N 다국어** | 런타임 메시지는 영어를 원문으로 사용하고 `Locales/*.yml`이 31개 언어를 내장(중국어 간체/번체 포함)하며 미번역 시 영어로 폴백. ResourceManager와 위성 어셈블리 불필요 |
| **AOT 지원** | net8.0+에서 AOT 분석 상시 사용(핵심 라이브러리 0 경고 / Extensions 0 경고) + 자체 소스 생성기(액션 디스패치 표, 스트림 펌프, 시그니처 유지). 핵심 <!--aot-scenarios:start-->23<!--aot-scenarios:end--> 시나리오, Extensions Minimal API 11 시나리오, XCode 장치 경로 6 시나리오를 NativeAOT로 실측(AOT 문서 §8) |

## 빠른 시작

**서버 5줄, 클라이언트 5줄이면 RPC가 동작합니다.**

```csharp
// === 서버 ===
var server = new ApiServer(12345);
server.Register<MyController>();
server.Start();

public class MyController : ApiController
{
    public String Ping(String name) => $"Hello {name}, {DateTime.Now:HH:mm:ss}";
    // 스트리밍: IAsyncEnumerable<T>를 반환하면 항목별로 전송됩니다
    public async IAsyncEnumerable<String> StreamLogs(Int32 count)
    {
        for (var i = 0; i < count; i++)
        {
            await Task.Delay(100);
            yield return $"Log #{i}: {DateTime.Now:HH:mm:ss}";
        }
    }
}

// === 클라이언트 ===
var client = new ApiClient("tcp://127.0.0.1:12345");
client.Open();

// 일반 호출
var rs = await client.InvokeAsync<String>("My/Ping", new { name = "dev" });

// 스트리밍 호출 — 서버 전송 항목을 순서대로 수신
await foreach (var log in client.InvokeStreamAsync<String>("My/StreamLogs", new { count = 10 }))
    Console.WriteLine(log);

// 메타데이터 전달(TraceId 자동 주입)
client.Headers["TenantId"] = "tenant-01";
```

## 성능 지표

| 지표 | 일반 값 | 실험 최고치 |
|:----|:------:|:--------:|
| TCP 장기 연결 수 | 10,000 | 4,000,000 |
| RPC 처리량 | 100,000 TPS | 455,000 msg/s |
| RPC 1회 지연 | 약 41 μs | — |
| 프레임워크 지원 | net45 / net461 / netstandard2.0 / netstandard2.1 / net6.0 / net8.0 / net10.0 (NewLife.Core v12 기준, net8.0+ NativeAOT) |

---
## 다른 NewLife 구성 요소와의 협업

| 구성 요소 | 가치 |
| ---- | -------- |
| NewLife.Core | 로깅, 구성, 직렬화, 다국어, 개체 풀, TimerX, 성능 추적 |
| NewLife.Net | 고성능 네트워크 스택, WebSocket/코덱, 포트 재사용 |
| NewLife.Redis | 분산 캐시 / 메시지 큐로 세션 공유와 푸시 팬아웃 확장 |
| Stardust | 노드 등록, 구성 센터, APM, 배포 센터 |
| AntJob | ClientBase로 작업 스케줄링과 분산 컴퓨팅 |
| NewLife.MQTT / Modbus / IoT | 장치 프로토콜 수집 후 Remoting으로 상향 전송 |

## 기여와 피드백

환영합니다: 버그 제보 / 성능 개선 PR / 새 프로토콜 코덱 / 컨트롤러 예제 / 문서 보강 / 새 언어 번역.
이슈에는 실행 환경, 대상 프레임워크, 최소 재현 코드, 로그 발췌를 포함해 주세요.

---
## 생태계 지도

NewLife는 독립적으로 버전이 관리되는 오픈소스 프로젝트의 계층형 생태계입니다. 아래 지도는 모든 NewLife 리포지토리에서 그대로 동일하게 공유되며, 각 프로젝트는 코드 리포지토리와 기본 NuGet 패키지로 연결됩니다.

| 프로젝트 | 한 줄 소개 | 다운로드 |
| --- | --- | --- |
| **기반** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | 로깅, 구성, 캐시, 네트워크, 직렬화, 스케줄링, APM 추적. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | 데이터 미들웨어: 단일 테이블 수백억 건, 자동 분할 테이블, 읽기/쓰기 분리, 10여 종 데이터베이스 지원. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 고성능 네트워크 라이브러리: 단일 노드 177만 pps, 14.5 Gbps(TCP 1KB, 2026-10 실측), 400만 Tcp 장기 연결. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | CS 애플리케이션과 IoT 장치를 위한 프로토콜 통신 및 RPC 프레임워크. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | .NET 애플리케이션을 운영체제 데몬으로 설치: Windows 서비스와 Linux systemd. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | 콘솔, Web, API, 서비스, 데이터, IoT용 `dotnet new` 프로젝트 템플릿. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **데이터 및 메시징** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Redis 클라이언트: 마이크로초 수준 지연, 백만 수준 처리량, 메시지 큐 내장. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | Apache RocketMQ와 알리바바 클라우드 메시지 큐를 지원하는 순수 관리형 RocketMQ 클라이언트. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | MQTT 클라이언트와 서버, 알리바바 클라우드 IoT 연결 포함. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | C# 하이브리드 데이터베이스: 임베디드/네트워크 두 모드, 관계형, 시계열, 메시지 큐, KV를 하나로. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | NewLife 데이터 스택 위에 구축한 MySql 드라이버와 도우미 계층. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web, AI 및 분산** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 빠른 관리 플랫폼 프레임워크: 권한, SSO, OAuth 서버, CRUD 스캐폴딩. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | 분산 서비스 플랫폼: 노드 관리, APM, 구성 센터, 등록 센터, 배포 센터. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | 샤딩, 재시도, 백필을 지원하는 분산 작업 및 실시간 데이터 스케줄링 플랫폼. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | 46개 LLM 공급자를 통합 연결하는 클라이언트와 바로 사용할 수 있는 ChatAI 웹 채팅 애플리케이션. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **산업 및 IoT** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | 프로토콜 계약과 장치 추상을 정의하는 IoT 표준 라이브러리. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | NewLife.IoT 기반으로 구현한 Modbus TCP/RTU/ASCII. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | NewLife.IoT 기반으로 구현한 지멘스 PLC 프로토콜. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | NewLife.IoT 기반으로 구현한 오므론 PLC 프로토콜. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | NewLife.IoT 기반으로 구현한 미쓰비시 Melsec PLC 프로토콜. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | 산업 데이터 수집용 OPC 클라이언트. | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | BACnet 빌딩 자동화 프로토콜 스택. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | 게이트웨이와 장치 관리를 갖춘 경량 오픈소스 IoT 플랫폼. | — |
| **차량 텔레매틱스** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | JT/T 808 차량 위치 프로토콜: 70개 이상 메시지 본문, 2011/2013/2019 지원. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | 운송 관리 부서를 위한 JT/T 809 플랫폼 간 프로토콜. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | JT/T 1078 차량용 비디오: RTP 파싱, FLV/fMP4 리먹싱, 라이브 스트리밍. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **도메인 컴포넌트** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | Excel, Word, PPT, PDF 등 15개 이상 문서 형식을 하나의 패키지로. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | 크로스 플랫폼 오디오: 코덱, 장치 IO, DSP, 컨테이너, 스트리밍. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | 비디오 코덱, 컨테이너 형식, 프레임 처리, 스트리밍. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 바이두/고덕/텐센트/톈디투 지도 통합 인터페이스, 지오코딩과 좌표 변환 지원. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | IPv4 위치 정보: 오프라인 메모리 매핑 데이터베이스와 온라인 API 이중 소스. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **도구 및 제품** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | 개발자 도구 모음: 네트워크, 시리얼 포트, 암호화, 정규식, Modbus, MQTT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | 셀프 호스팅 Git 소스 코드 관리 시스템. | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | 구버전 호스트에서 시작할 때 일치하는 .NET 런타임을 자동으로 설치합니다. | — |

> 배지는 각 프로젝트의 기본 패키지를 나타냅니다. 테마, 확장, 위성 패키지는 해당 NuGet 페이지에서 확인하세요.여기에 나열하지 않은 30여 개 패키지(FTP, LoRa, Office 확장, 각종 프로토콜 드라이버 등)는 [NewLifeX 조직](https://github.com/NewLifeX)과 [newlifex.com](https://newlifex.com)에서 확인할 수 있습니다.

---
## 뉴라이프 개발팀

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

뉴라이프 팀(NewLife)은 2002년에 설립되어 20여 년간 .NET과 IoT 기술 생태계에 집중하며 100여 개의 오픈소스 프로젝트를 주도하고(이 중 34개가 핵심 프로젝트), 전력, 물류, 의료, 산업 제어, 인터넷 등 산업에 소프트웨어와 하드웨어를 아우르는 통합 솔루션을 제공해 왔습니다. "학문에는 선후가 없으며, 이룬 사람이 스승이다"라는 신조를 따릅니다.

오픈소스 컴포넌트는 NuGet에서 누적 <!--nuget-total:start-->6.48M<!--nuget-total:end-->회 다운로드되었습니다(생태계 전체 기준, 2026-10).

웹사이트: https://newlifex.com · 오픈소스 조직: https://github.com/NewLifeX

<details>
<summary>위챗 공식 계정: 智能大石头(스캔하여 팔로우)</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

---
## 라이선스

MIT © NewLife. 상업적 사용과 2차 개발을 환영합니다.
