# NewLife.Remoting — Estrutura unificada de comunicação remota (RPC + HTTP + WebSocket + SRMP)

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
🌐 [中文](Readme.MD) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

NewLife.Remoting é a infraestrutura unificada de comunicação remota da pilha NewLife, distribuída em dois pacotes NuGet:

| Pacote | Papel | Cobertura |
|:---|:-----|:------|
| **NewLife.Remoting** | Biblioteca de comunicação principal | Cliente/servidor RPC, codecs de protocolo, gerenciamento de conexões, chamadas em streaming, classes base de cliente, pipeline de comandos, atualização automática |
| **NewLife.Remoting.Extensions** | Biblioteca de extensões | Classes base de controllers de servidor, host Minimal API, serviços de dispositivos, autenticação por token, barramento de respostas de comandos |

Código-fonte: https://github.com/NewLifeX/NewLife.Remoting  
> NuGet: `NewLife.Remoting` / `NewLife.Remoting.Extensions`

> ⚠️ Esta tradução pode estar desatualizada em relação ao [README em chinês](Readme.MD), que é a fonte autoritativa.

## Índice de documentação

| Documento | Descrição |
|------|------|
| [Doc/需求文档.md](Doc/需求文档.md) | Requisitos: visão, objetivos, requisitos funcionais (RPC/STRM/CLNT/UPGD/SRV/CMD/AOT/L10N/EXT) |
| [Doc/架构设计.md](Doc/架构设计.md) | Arquitetura: camadas, módulos principais, gerenciamento de memória, estratégia de testes, decisões de projeto |
| [Doc/SRV-服务端架构.md](Doc/SRV-服务端架构.md) | Arquitetura do servidor: camada de serviço, fluxos principais (login/registro/logout/heartbeat/atualização/eventos/comandos), ciclo de vida das sessões e cluster |
| [Doc/功能清单.md](Doc/功能清单.md) | Acompanhamento de funcionalidades: status, itens em andamento e backlog |
| [Doc/竞品分析.md](Doc/竞品分析.md) | Análise competitiva: frameworks RPC e envio de comandos IoT |
| [Doc/SRMP.MD](Doc/SRMP.MD) | Especificação do protocolo SRMP |
| [Doc/RPC内存管理设计.md](Doc/RPC内存管理设计.md) | Gerenciamento de memória sem cópia |
| [Doc/自动更新机制.md](Doc/自动更新机制.md) | Projeto completo da atualização automática |
| [Doc/重试策略.md](Doc/重试策略.md) | Política de retentativa: `IRetryPolicy` e backoff exponencial |
| [Doc/AOT兼容性分析.md](Doc/AOT兼容性分析.md) | Escopo do NativeAOT, <!--aot-scenarios:start-->23<!--aot-scenarios:end--> cenários verificados (smoke <!--aot-smoke:start-->23/23<!--aot-smoke:end-->), orientação e limites conhecidos |
| [Doc/多语言本地化接入.md](Doc/多语言本地化接入.md) | Mensagens em inglês, tabelas `Locales/*.yml` e guia de `L.T` |
| [Doc/README多语言规范.md](Doc/README多语言规范.md) | Regras de ordenação, scripts e gate de CI para os 11 idiomas do README |

---

## Destaques

| Capacidade | Descrição |
|:---------|------|
| **Núcleo RPC** | Protocolo SRMP, codecs JSON/binário, pipeline de requisições, cliente/servidor, gerenciamento de cluster, metadados, retentativas, servidor totalmente assíncrono, rastreamento por TraceId |
| **STRM streaming** | Server-Streaming: `IAsyncEnumerable<T>` enviado item a item por SRMP e SSE |
| **CLNT cliente base** | Login/heartbeat/atualização/comandos/eventos em um só lugar, alternância automática WS/SSE |
| **UPGD atualização** | Detectar → baixar → verificar → extrair → sobrescrever → reiniciar |
| **SRV base do servidor** | ApiController/BaseController/BaseDeviceController, TokenService, IDeviceService; o mesmo endpoint é exposto em MVC e em Minimal API (implementações espelhadas validadas por testes) |
| **CMD comandos** | SessionManager + broadcast por barramento de eventos + canais WS/SSE/Ping |
| **L10N localização** | As mensagens usam inglês como texto de origem; `Locales/*.yml` fornecem 31 idiomas integrados (incluindo chinês simplificado e tradicional), com fallback para inglês; sem ResourceManager nem assemblies satélite |
| **Suporte AOT** | Análise AOT permanente a partir do net8.0 (0 avisos na biblioteca principal e no Extensions) + gerador de código próprio (tabela de despacho, bombas de streaming, preservação de assinaturas); NativeAOT validado em <!--aot-scenarios:start-->23<!--aot-scenarios:end--> cenários, 11 do host Minimal API e 6 da cadeia de dispositivos XCode (ver doc AOT §8) |

## Início rápido

**Cinco linhas no servidor, cinco no cliente e o RPC funciona:**

```csharp
// === Servidor ===
var server = new ApiServer(12345);
server.Register<MyController>();
server.Start();

public class MyController : ApiController
{
    public String Ping(String name) => $"Hello {name}, {DateTime.Now:HH:mm:ss}";
    // Streaming: retornar IAsyncEnumerable<T> envia item a item
    public async IAsyncEnumerable<String> StreamLogs(Int32 count)
    {
        for (var i = 0; i < count; i++)
        {
            await Task.Delay(100);
            yield return $"Log #{i}: {DateTime.Now:HH:mm:ss}";
        }
    }
}

// === Cliente ===
var client = new ApiClient("tcp://127.0.0.1:12345");
client.Open();

// Chamada normal
var rs = await client.InvokeAsync<String>("My/Ping", new { name = "dev" });

// Chamada em streaming — recebe cada envio do servidor
await foreach (var log in client.InvokeStreamAsync<String>("My/StreamLogs", new { count = 10 }))
    Console.WriteLine(log);

// Metadados (TraceId é injetado automaticamente)
client.Headers["TenantId"] = "tenant-01";
```

## Desempenho

| Métrica | Típico | Pico em laboratório |
|:----|:------:|:--------:|
| Conexões TCP longas | 10.000 | 4.000.000 |
| Throughput RPC | 100.000 TPS | 22.660.000 TPS |
| Latência por RPC | ~41 μs | — |
| Frameworks | net45 / net461 / netstandard2.0 / netstandard2.1 / net6.0 / net8.0 / net10.0 (alinhado ao NewLife.Core v12; NativeAOT a partir do net8.0) |

---
## Cooperação com outros componentes NewLife

| Componente | Valor |
| ---- | -------- |
| NewLife.Core | Log, configuração, serialização, localização, pool de objetos, TimerX, rastreamento |
| NewLife.Net | Pilha de rede de alto desempenho, WebSocket/codecs, reutilização de portas |
| NewLife.Redis | Cache distribuído / fila de mensagens para sessões compartilhadas ou fan-out |
| Stardust | Registro de nós, central de configuração, APM, central de releases |
| AntJob | Agendamento de tarefas e computação distribuída via ClientBase |
| NewLife.MQTT / Modbus / IoT | Coleta de protocolos de dispositivos e envio ascendente via Remoting |

## Contribuições e feedback

Bem-vindos: relatos de bugs, PRs de desempenho, novos codecs, exemplos de controllers, documentação e novas traduções.
Em issues, informe: ambiente, framework de destino, reprodução mínima e trecho de log.

---
## Matriz de projetos NewLife

Todos os projetos suportam por padrão net10.0/net8.0/netstandard2.1/netstandard2.0/net4.62/net4.5; a linha 2024.0801 também suporta net4.0/net2.0.

| Projeto | Ano | Descrição |
| :--: | :--: | ---- |
| [NewLife.Core](https://github.com/NewLifeX/X) | 2002 | Biblioteca principal: log, configuração, cache, rede, serialização, APM |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | 2005 | Middleware de dados: dezenas de bilhões de linhas por tabela, sharding, separação leitura/escrita |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 2005 | Biblioteca de rede: 22,66 M tps / milhões de conexões |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 2011 | Este projeto: framework de comunicação com dois protocolos |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 2010 | Plataforma de desenvolvimento rápido: permissões / OAuth / SSO |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | 2008 | Instalação de serviços / daemon / Systemd |
| [NewLife.Zero](https://github.com/NewLifeX/NewLife.Zero) | 2020 | Modelos de projeto |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | 2017 | Cliente Redis / fila de mensagens de alto desempenho |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 2018 | Cliente RocketMQ totalmente gerenciado |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | 2019 | MqttClient/MqttServer para IoT |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | 2022 | Modelos padrão de comunicação IoT |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | 2022 | Família de protocolos Modbus |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | 2022 | Comunicação com CLPs Siemens |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 2022 | Componentes de agregação de mapas |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | 2023 | Codecs de áudio |
| Stardust | 2018 | Plataforma de serviços distribuídos (registro/config/APM/release) |
| AntJob | 2019 | Plataforma distribuída de big data |
| Outros | - | Consulte o site oficial |

---
## Equipe e direitos autorais

![XCode](https://newlifex.com/logo.png)

A NewLife foi fundada em 2002, com mais de 80 projetos open source e mais de 4 milhões de downloads acumulados no NuGet. Todos os projetos usam licença MIT e podem ser modificados e redistribuídos livremente (sem atribuição).  
Site: https://newlifex.com  
GitHub: https://github.com/NewLifeX  
Grupos QQ: 1600800 / 1600838  

> `A NewLife começou em 2002; alguns projetos têm mais de 20 anos e os repositórios preservam todos os commits desde 2010.`

---
## Licença

MIT © NewLife. Uso comercial e desenvolvimento derivado são bem-vindos.
