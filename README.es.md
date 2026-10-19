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
🌐 [中文](Readme.MD) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

NewLife.Remoting es la infraestructura unificada de comunicación remota del stack NewLife y se distribuye en dos paquetes NuGet:

| Paquete | Rol | Cobertura |
|:---|:-----|:------|
| **NewLife.Remoting** | Biblioteca de comunicación principal | Cliente/servidor RPC, códecs de protocolo, gestión de conexiones, llamadas en streaming, clases base de cliente, canalización de comandos, actualización automática |
| **NewLife.Remoting.Extensions** | Biblioteca de extensiones | Clases base de controladores de servidor, host de API mínima, servicios de gestión de dispositivos, autenticación por token, bus de respuestas de comandos |

Código fuente: https://github.com/NewLifeX/NewLife.Remoting  
> NuGet: `NewLife.Remoting` / `NewLife.Remoting.Extensions`

> ⚠️ Esta traducción puede estar desactualizada respecto al [README en chino](Readme.MD), que es la fuente autorizada.

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
| Rendimiento RPC | 100.000 TPS | 22.660.000 TPS |
| Latencia por RPC | ~41 μs | — |
| Frameworks | net45 / net461 / netstandard2.0 / netstandard2.1 / net6.0 / net8.0 / net10.0 (alineado con NewLife.Core v12; NativeAOT desde net8.0) |

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
## Matriz de proyectos NewLife

Todos los proyectos soportan por defecto net10.0/net8.0/netstandard2.1/netstandard2.0/net4.62/net4.5; la línea 2024.0801 también soporta net4.0/net2.0.

| Proyecto | Año | Descripción |
| :--: | :--: | ---- |
| [NewLife.Core](https://github.com/NewLifeX/X) | 2002 | Biblioteca principal: registro, configuración, caché, red, serialización, APM |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | 2005 | Middleware de datos: decenas de miles de millones de filas, particionado, separación lectura/escritura |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 2005 | Biblioteca de red: 22,66 M tps / millones de conexiones |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 2011 | Este proyecto: marco de comunicación de doble protocolo |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 2010 | Plataforma de desarrollo rápido: permisos / OAuth / SSO |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | 2008 | Instalación de servicios / daemon / Systemd |
| [NewLife.Zero](https://github.com/NewLifeX/NewLife.Zero) | 2020 | Plantillas de proyecto |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | 2017 | Cliente Redis / cola de mensajes de alto rendimiento |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 2018 | Cliente RocketMQ totalmente gestionado |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | 2019 | MqttClient/MqttServer para IoT |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | 2022 | Modelos estándar de comunicación IoT |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | 2022 | Familia de protocolos Modbus |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | 2022 | Comunicación con PLC Siemens |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 2022 | Componentes de agregación de mapas |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | 2023 | Códecs de audio |
| Stardust | 2018 | Plataforma de servicios distribuidos (registro/config/APM/despliegue) |
| AntJob | 2019 | Plataforma distribuida de cálculo de big data |
| Más | - | Consulte el sitio oficial |

---
## Equipo y derechos de autor

![XCode](https://newlifex.com/logo.png)

NewLife nació en 2002, cuenta con más de 80 proyectos de código abierto y más de 4 millones de descargas acumuladas en NuGet. Todos los proyectos usan licencia MIT y pueden modificarse y redistribuirse libremente (sin atribución).  
Sitio web: https://newlifex.com  
GitHub: https://github.com/NewLifeX  
Grupos QQ: 1600800 / 1600838  

> `NewLife comenzó en 2002; algunos proyectos superan los 20 años y los repositorios conservan todos los commits desde 2010.`

---
## Licencia

MIT © NewLife. Se agradece el uso comercial y el desarrollo derivado.
