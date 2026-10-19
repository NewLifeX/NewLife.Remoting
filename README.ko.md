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
🌐 [中文](Readme.MD) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [العربية](README.ar.md)
<!-- lang:end -->

NewLife.Remoting은 NewLife 스택의 통합 원격 통신 기반으로, 두 개의 NuGet 패키지로 제공됩니다.

| 패키지 | 역할 | 범위 |
|:---|:-----|:------|
| **NewLife.Remoting** | 핵심 통신 라이브러리 | RPC 클라이언트/서버, 프로토콜 코덱, 연결 관리, 스트리밍 호출, 클라이언트 기본 클래스, 명령 파이프라인, 자동 업데이트 |
| **NewLife.Remoting.Extensions** | 확장 라이브러리 | 서버 컨트롤러 기본 클래스, Minimal API 호스트, 장치 관리 서비스, 토큰 인증, 명령 응답 버스 |

소스: https://github.com/NewLifeX/NewLife.Remoting  
> NuGet: `NewLife.Remoting` / `NewLife.Remoting.Extensions`

> ⚠️ 이 번역은 [중국어 README](Readme.MD)보다 늦을 수 있으며, 중국어판이 기준입니다.

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
| RPC 처리량 | 100,000 TPS | 22,660,000 TPS |
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
## 뉴라이프 프로젝트 매트릭스

모든 프로젝트는 기본적으로 net10.0/net8.0/netstandard2.1/netstandard2.0/net4.62/net4.5를 지원하며, 2024.0801 계열은 net4.0/net2.0도 지원합니다.

| 프로젝트 | 연도 | 설명 |
| :--: | :--: | ---- |
| [NewLife.Core](https://github.com/NewLifeX/X) | 2002 | 핵심 라이브러리: 로깅, 구성, 캐시, 네트워크, 직렬화, APM |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | 2005 | 데이터 미들웨어: 단일 테이블 수백억 행, 자동 분할, 읽기/쓰기 분리 |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 2005 | 네트워크 라이브러리: 2,266만 tps / 수백만 연결 |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 2011 | 이 프로젝트: 이중 프로토콜 통신 프레임워크 |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 2010 | 빠른 개발 플랫폼: 권한 / OAuth / SSO |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | 2008 | 서비스 설치 / 데몬 / Systemd |
| [NewLife.Zero](https://github.com/NewLifeX/NewLife.Zero) | 2020 | 프로젝트 템플릿 모음 |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | 2017 | 고성능 Redis 클라이언트 / 메시지 큐 |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 2018 | 완전 관리형 RocketMQ 클라이언트 |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | 2019 | IoT용 MqttClient/MqttServer |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | 2022 | IoT 통신 표준 모델 |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | 2022 | Modbus 프로토콜 제품군 |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | 2022 | 지멘스 PLC 통신 |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 2022 | 지도 집계 구성 요소 |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | 2023 | 오디오 코덱 |
| Stardust | 2018 | 분산 서비스 플랫폼(등록/구성/APM/배포) |
| AntJob | 2019 | 분산 빅데이터 컴퓨팅 플랫폼 |
| 기타 | - | 공식 사이트 참조 |

---
## 팀과 저작권

![XCode](https://newlifex.com/logo.png)

NewLife는 2002년에 설립되었고 80개 이상의 오픈 소스 프로젝트와 400만 건 이상의 NuGet 누적 다운로드를 보유합니다. 모든 프로젝트는 MIT 라이선스이며 자유롭게 수정·재배포할 수 있습니다(출처 표기 불필요).  
웹사이트: https://newlifex.com  
GitHub: https://github.com/NewLifeX  
QQ 그룹: 1600800 / 1600838  

> `NewLife는 2002년에 시작되었으며 일부 프로젝트는 20년 이상의 역사를 가지고, 저장소는 2010년 이후 모든 커밋을 보관합니다.`

---
## 라이선스

MIT © NewLife. 상업적 사용과 2차 개발을 환영합니다.
