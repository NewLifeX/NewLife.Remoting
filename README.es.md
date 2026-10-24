# NewLife.Remoting — Marco unificado de comunicación remota (RPC + HTTP + WebSocket + SRMP)

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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

NewLife.Remoting es la infraestructura unificada de comunicación remota del stack NewLife y se distribuye en dos paquetes NuGet:

| Paquete | Rol | Cobertura |
|:---|:-----|:------|
| **NewLife.Remoting** | Biblioteca de comunicación principal | Cliente/servidor RPC, códecs de protocolo, gestión de conexiones, llamadas en streaming, clases base de cliente, canalización de comandos, actualización automática |
| **NewLife.Remoting.Extensions** | Biblioteca de extensiones | Clases base de controladores de servidor, host de API mínima, servicios de gestión de dispositivos, autenticación por token, bus de respuestas de comandos |

Código fuente: https://github.com/NewLifeX/NewLife.Remoting  
> NuGet: `NewLife.Remoting` / `NewLife.Remoting.Extensions`

> ⚠️ Esta traducción puede quedar desactualizada; la versión autorizada es el [README en inglés](README.md).

## Índice de documentación

| Documento | Descripción |
|------|------|
| [Doc/需求文档.md](Doc/需求文档.md) | Requisitos: visión, objetivos, requisitos funcionales (RPC/STRM/CLNT/UPGD/SRV/CMD/AOT/L10N/EXT) |
| [Doc/架构设计.md](Doc/架构设计.md) | Arquitectura: capas, módulos principales, gestión de memoria, estrategia de pruebas, decisiones de diseño |
| [Doc/SRV-服务端架构.md](Doc/SRV-服务端架构.md) | Arquitectura del servidor: capa de servicio, flujos principales (login/registro/logout/heartbeat/actualización/eventos/comandos), ciclo de vida de sesiones y despliegue en clúster |
| [Doc/功能清单.md](Doc/功能清单.md) | Seguimiento de funcionalidades: estado, elementos en curso y pendientes |
| [Doc/竞品分析.md](Doc/竞品分析.md) | Análisis competitivo: comparación de frameworks RPC y de envío de comandos IoT |
| [Doc/SRMP.MD](Doc/SRMP.MD) | Especificación del protocolo SRMP |
| [Doc/RPC内存管理设计.md](Doc/RPC内存管理设计.md) | Gestión de memoria sin copia |
| [Doc/自动更新机制.md](Doc/自动更新机制.md) | Diseño completo de la actualización automática |
| [Doc/重试策略.md](Doc/重试策略.md) | Política de reintentos: `IRetryPolicy` y retroceso exponencial |
| [Doc/AOT兼容性分析.md](Doc/AOT兼容性分析.md) | Alcance de NativeAOT, <!--aot-scenarios:start-->23<!--aot-scenarios:end--> escenarios verificados (smoke <!--aot-smoke:start-->23/23<!--aot-smoke:end-->), guía y límites conocidos |
| [Doc/多语言本地化接入.md](Doc/多语言本地化接入.md) | Mensajes en inglés, tablas `Locales/*.yml` y guía de `L.T` |
| [Doc/README多语言规范.md](Doc/README多语言规范.md) | Reglas de orden, scripts y control de CI para los 11 idiomas del README |

---

## Aspectos destacados

| Capacidad | Descripción |
|:---------|------|
| **Núcleo RPC** | Protocolo SRMP, códecs JSON/binario, canalización de peticiones, cliente/servidor, gestión de clúster, metadatos, reintentos, servidor totalmente asíncrono, trazabilidad con TraceId |
| **STRM streaming** | Server-Streaming: `IAsyncEnumerable<T>` enviado elemento a elemento por SRMP y SSE |
| **CLNT cliente base** | Login/heartbeat/actualización/comandos/eventos en un solo lugar, con conmutación automática WS/SSE |
| **UPGD actualización** | Detectar → descargar → verificar → extraer → sobrescribir → reiniciar |
| **SRV servidor base** | ApiController/BaseController/BaseDeviceController, TokenService, IDeviceService; el mismo protocolo se expone como MVC y como API mínima (implementaciones espejo validadas por pruebas) |
| **CMD comandos** | SessionManager + difusión por bus de eventos + canales WS/SSE/Ping |
| **L10N localización** | Los mensajes usan inglés como texto origen; `Locales/*.yml` aportan 31 idiomas integrados (incluido chino simplificado y tradicional), con retroceso al inglés; sin ResourceManager ni ensamblados satélite |
| **Soporte AOT** | Análisis AOT permanente en net8.0+ (0 advertencias en la biblioteca principal y Extensions) + generador propio (tabla de despacho, bombas de streaming, preservación de firmas); NativeAOT verificado en <!--aot-scenarios:start-->23<!--aot-scenarios:end--> escenarios, 11 del host de API mínima y 6 de la cadena de dispositivos XCode (ver documento AOT §8) |

## Inicio rápido

**Cinco líneas en el servidor y cinco en el cliente para tener RPC funcionando:**

```csharp
// === Servidor ===
var server = new ApiServer(12345);
server.Register<MyController>();
server.Start();

public class MyController : ApiController
{
    public String Ping(String name) => $"Hello {name}, {DateTime.Now:HH:mm:ss}";
    // Streaming: devolver IAsyncEnumerable<T> envía los elementos uno a uno
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

// Llamada normal
var rs = await client.InvokeAsync<String>("My/Ping", new { name = "dev" });

// Llamada en streaming: recibe cada envío del servidor
await foreach (var log in client.InvokeStreamAsync<String>("My/StreamLogs", new { count = 10 }))
    Console.WriteLine(log);

// Metadatos (TraceId se inyecta automáticamente)
client.Headers["TenantId"] = "tenant-01";
```

## Rendimiento

| Métrica | Valor típico | Pico en laboratorio |
|:----|:------:|:--------:|
| Conexiones TCP largas | 10.000 | 4.000.000 |
| Rendimiento RPC | 100.000 TPS | 455.000 msg/s |
| Latencia por RPC | ~41 μs | — |
| Frameworks | net45 / net461 / netstandard2.0 / netstandard2.1 / net6.0 / net8.0 / net10.0 (alineado con NewLife.Core v12; NativeAOT desde net8.0) |

## Actualización a v4

v4 se alinea con NewLife.Core v12 y cambia la capa de red. Antes de actualizar:

- **Dos puertos**: el puerto principal solo sirve SRMP; configure `HttpPort` explícitamente para habilitar HTTP (desactivado por defecto).
- **Sin SRMP sobre WebSocket**: las direcciones `ws://`/`wss://` ahora lanzan `NotSupportedException`; use `tcp://`/`udp://` para SRMP y `http(s)://` para llamadas HTTP.
- **Tipos de códec eliminados**: `WsClient`, `HttpCodec`, `WebSocketClientCodec`, `WebSocketServerCodec` y los métodos de trama de `HttpMessage` ya no existen; HTTP lo gestiona el `HttpServer` de Core y el framing personalizado debe implementar `IMessageCodec`.
- **Claves de mensajes en inglés**: los mensajes en tiempo de ejecución usan el texto inglés como clave; compare `ApiCode` o códigos de error en lugar del texto localizado.
- **Frameworks de destino**: ya no se publican los activos net5.0/net7.0/net9.0; NuGet recurre automáticamente al framework compatible más cercano.

Consulte [ChangeLog.md](ChangeLog.md) para la lista completa.

---
## Integración con otros componentes NewLife

| Componente | Aporte |
| ---- | -------- |
| NewLife.Core | Registro, configuración, serialización, localización, pool de objetos, TimerX, trazas |
| NewLife.Net | Pila de red de alto rendimiento, WebSocket/códecs, reutilización de puertos |
| NewLife.Redis | Caché distribuida / cola de mensajes para sesiones compartidas o fan-out |
| Stardust | Registro de nodos, centro de configuración, APM, centro de despliegue |
| AntJob | Planificación de tareas y computación distribuida mediante ClientBase |
| NewLife.MQTT / Modbus / IoT | Captura de protocolos de dispositivo y envío ascendente por Remoting |

## Contribuciones y comentarios

Se aceptan: informes de errores, PR de rendimiento, nuevos códecs, ejemplos de controladores, documentación y nuevas traducciones.
Al abrir un issue incluya: entorno, framework de destino, reproducción mínima y extracto de los registros.

---
## Mapa del ecosistema

NewLife es un ecosistema por capas de proyectos de código abierto con versionado independiente. El mapa siguiente se comparte literalmente en todos los repositorios de NewLife; cada proyecto enlaza a su repositorio y a su paquete principal de NuGet.

| Proyecto | Qué hace | Descargas |
| --- | --- | --- |
| **Fundación** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | Registro de logs, configuración, caché, red, serialización, programación de tareas y seguimiento APM. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | Middleware de datos: tablas únicas a escala de decenas de miles de millones, particionado automático, separación lectura/escritura y más de 10 bases de datos. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | Biblioteca de red de alto rendimiento: 1,77 M pps / 14,5 Gbps en una máquina (TCP 1KB, 2026-10), 4 M conexiones TCP persistentes. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | Framework de comunicación por protocolo y RPC para aplicaciones cliente/servidor y dispositivos IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | Instala aplicaciones .NET como demonios del sistema operativo: servicios de Windows y systemd de Linux. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | Plantillas de proyecto `dotnet new` para consola, web, API, servicio, datos e IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **Datos y mensajería** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Cliente Redis con latencia de microsegundos, millones de operaciones por segundo y colas de mensajes integradas. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | Cliente RocketMQ totalmente administrado para Apache RocketMQ y Alibaba Cloud Message Queue. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | Cliente y servidor MQTT, incluida la conectividad con Alibaba Cloud IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | Base de datos híbrida en C#: embebida o en red, relacional, series temporales, cola de mensajes y KV en una sola. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | Controlador y capa de ayuda de MySql construidos sobre la pila de datos de NewLife. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web, IA y distribuido** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | Framework rápido para plataformas de administración: permisos, SSO, servidor OAuth y andamiaje CRUD. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | Plataforma distribuida de servicios: gestión de nodos, APM, centro de configuración, registro y centro de despliegues. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | Plataforma de tareas distribuidas y programación de datos en tiempo real con particionado, reintentos y relleno histórico. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | Cliente unificado para 46 proveedores de LLM más ChatAI, una aplicación web de chat lista para usar. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **Industrial e IoT** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | Biblioteca estándar de IoT que define contratos de protocolo y abstracciones de dispositivos. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | Implementación de Modbus TCP/RTU/ASCII sobre NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | Protocolo para PLC Siemens sobre NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | Protocolo para PLC Omron sobre NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | Protocolo para PLC Mitsubishi Melsec sobre NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | Cliente OPC para el acceso a datos industriales. | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | Pila de protocolo BACnet para la automatización de edificios. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | Plataforma IoT de código abierto y ligera con gestión de pasarelas y dispositivos. | — |
| **Telemática vehicular** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | Protocolo de posicionamiento vehicular JT/T 808: más de 70 cuerpos de mensaje, 2011/2013/2019. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | Protocolo plataforma a plataforma JT/T 809 para autoridades de transporte. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | Vídeo a bordo JT/T 1078: análisis RTP, remultiplexado FLV/fMP4 y transmisión en directo. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **Componentes de dominio** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | Un solo paquete para Excel, Word, PPT, PDF y más de 15 formatos de documento. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | Audio multiplataforma: códecs, E/S de dispositivos, DSP, contenedores y streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | Códecs de vídeo, formatos de contenedor, procesamiento de fotogramas y streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | Interfaz unificada sobre Baidu, AMap, Tencent y Tianditu con geocodificación y conversión de coordenadas. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | Geolocalización IPv4 con una base de datos offline mapeada en memoria y una API en línea de respaldo. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **Herramientas y productos** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | Caja de herramientas del desarrollador: red, puerto serie, criptografía, expresiones regulares, Modbus y MQTT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | Sistema de gestión de código fuente Git autoalojado. | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | Instala automáticamente el runtime de .NET correspondiente cuando se ejecuta desde un host más antiguo. | — |

> Las insignias muestran el paquete principal de cada proyecto; los paquetes de temas, extensiones y satélites figuran en sus páginas de NuGet.Los más de 30 paquetes no listados aquí (FTP, LoRa, extensiones de Office, controladores de protocolo, etc.) están disponibles en la [organización NewLifeX](https://github.com/NewLifeX) y en [newlifex.com](https://newlifex.com).

---
## Equipo

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

NewLife se fundó en 2002 y lleva más de dos décadas dedicado al ecosistema .NET e IoT, mantiene cerca de 100 proyectos de código abierto (34 de ellos principales) y ofrece soluciones integradas de software y hardware para los sectores de energía, logística, salud, control industrial e Internet. Nos guía el lema «en el aprendizaje no hay antigüedad: quien domina es maestro».

Los componentes de código abierto acumulan <!--nuget-total:start-->6.48M<!--nuget-total:end--> descargas en NuGet (todo el ecosistema, a fecha de 2026-10).

Sitio web: https://newlifex.com · Organización de código abierto: https://github.com/NewLifeX

<details>
<summary>Cuenta oficial de WeChat: 智能大石头 (escanea para seguir)</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

---
## Licencia

MIT © NewLife. Se agradece el uso comercial y el desarrollo derivado.
