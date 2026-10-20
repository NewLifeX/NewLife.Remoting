# NewLife.Remoting — Framework de communication distante unifié (RPC + HTTP + WebSocket + SRMP)

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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

NewLife.Remoting est l'infrastructure de communication distante unifiée de la pile NewLife, distribuée en deux packages NuGet :

| Package | Rôle | Périmètre |
|:---|:-----|:------|
| **NewLife.Remoting** | Bibliothèque de communication principale | Client/serveur RPC, codecs de protocole, gestion des connexions, appels en streaming, classes de base client, pipeline de commandes, mise à jour automatique |
| **NewLife.Remoting.Extensions** | Bibliothèque d'extensions | Classes de base de contrôleurs serveur, hôte Minimal API, services de gestion d'appareils, authentification par jeton, bus de réponses aux commandes |

Code source : https://github.com/NewLifeX/NewLife.Remoting  
> NuGet : `NewLife.Remoting` / `NewLife.Remoting.Extensions`

> ⚠️ Cette traduction peut être en retard ; la version de référence est le [README anglais](README.md).

## Index de la documentation

| Document | Description |
|------|------|
| [Doc/需求文档.md](Doc/需求文档.md) | Exigences : vision, objectifs, exigences fonctionnelles (RPC/STRM/CLNT/UPGD/SRV/CMD/AOT/L10N/EXT) |
| [Doc/架构设计.md](Doc/架构设计.md) | Architecture : couches, modules clés, gestion mémoire, stratégie de test, décisions de conception |
| [Doc/SRV-服务端架构.md](Doc/SRV-服务端架构.md) | Architecture serveur : couche service, flux principaux (connexion/enregistrement/déconnexion/heartbeat/mise à jour/événements/commandes), cycle de vie des sessions, déploiement en cluster |
| [Doc/功能清单.md](Doc/功能清单.md) | Suivi des fonctionnalités : état, éléments en cours et backlog |
| [Doc/竞品分析.md](Doc/竞品分析.md) | Analyse concurrentielle : frameworks RPC et diffusion de commandes IoT |
| [Doc/SRMP.MD](Doc/SRMP.MD) | Spécification du protocole SRMP |
| [Doc/RPC内存管理设计.md](Doc/RPC内存管理设计.md) | Gestion mémoire sans copie |
| [Doc/自动更新机制.md](Doc/自动更新机制.md) | Conception complète de la mise à jour automatique |
| [Doc/重试策略.md](Doc/重试策略.md) | Stratégie de nouvelle tentative : `IRetryPolicy` et backoff exponentiel |
| [Doc/AOT兼容性分析.md](Doc/AOT兼容性分析.md) | Périmètre NativeAOT, <!--aot-scenarios:start-->23<!--aot-scenarios:end--> scénarios vérifiés (smoke <!--aot-smoke:start-->23/23<!--aot-smoke:end-->), guide et limites connues |
| [Doc/多语言本地化接入.md](Doc/多语言本地化接入.md) | Messages d'exécution en anglais, tables `Locales/*.yml` et guide `L.T` |
| [Doc/README多语言规范.md](Doc/README多语言规范.md) | Règles d'ordre, scripts et contrôle CI pour les 11 langues du README |

---

## Points forts

| Capacité | Description |
|:---------|------|
| **Cœur RPC** | Protocole SRMP, codecs JSON/binaire, pipeline de requêtes, client/serveur, gestion de cluster, métadonnées, politique de nouvelle tentative, serveur entièrement asynchrone, traçage TraceId |
| **STRM streaming** | Server-Streaming : `IAsyncEnumerable<T>` poussé élément par élément via SRMP et SSE |
| **CLNT client de base** | Connexion/heartbeat/mise à jour/commandes/événements au même endroit, bascule automatique WS/SSE |
| **UPGD mise à jour** | Détecter → télécharger → vérifier → extraire → remplacer → redémarrer |
| **SRV base serveur** | ApiController/BaseController/BaseDeviceController, TokenService, IDeviceService ; le même protocole d'endpoint est exposé en MVC et en Minimal API (implémentations miroir verrouillées par tests) |
| **CMD commandes** | SessionManager + diffusion par bus d'événements + canaux WS/SSE/Ping |
| **L10N localisation** | Les messages d'exécution ont l'anglais pour texte source ; `Locales/*.yml` fournissent 31 langues intégrées (dont le chinois simplifié et traditionnel), avec repli sur l'anglais ; sans ResourceManager ni assemblys satellites |
| **Support AOT** | Analyse AOT permanente à partir de net8.0 (0 avertissement sur la bibliothèque principale et Extensions) + générateur de source interne (table de dispatch, pompes de streaming, conservation des signatures) ; NativeAOT validé sur <!--aot-scenarios:start-->23<!--aot-scenarios:end--> scénarios, 11 scénarios Minimal API et 6 scénarios d'appareils XCode (voir doc AOT §8) |

## Démarrage rapide

**Cinq lignes côté serveur, cinq côté client, et le RPC fonctionne :**

```csharp
// === Serveur ===
var server = new ApiServer(12345);
server.Register<MyController>();
server.Start();

public class MyController : ApiController
{
    public String Ping(String name) => $"Hello {name}, {DateTime.Now:HH:mm:ss}";
    // Streaming : retourner IAsyncEnumerable<T> pousse les éléments un par un
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

// Appel classique
var rs = await client.InvokeAsync<String>("My/Ping", new { name = "dev" });

// Appel en streaming — réception des pushes serveur
await foreach (var log in client.InvokeStreamAsync<String>("My/StreamLogs", new { count = 10 }))
    Console.WriteLine(log);

// Métadonnées (TraceId injecté automatiquement)
client.Headers["TenantId"] = "tenant-01";
```

## Performances

| Indicateur | Typique | Pic en laboratoire |
|:----|:------:|:--------:|
| Connexions TCP longues | 10 000 | 4 000 000 |
| Débit RPC | 100 000 TPS | 455 000 msg/s |
| Latence par RPC | ~41 μs | — |
| Frameworks | net45 / net461 / netstandard2.0 / netstandard2.1 / net6.0 / net8.0 / net10.0 (aligné sur NewLife.Core v12 ; NativeAOT à partir de net8.0) |

---
## Coopération avec les autres composants NewLife

| Composant | Apport |
| ---- | -------- |
| NewLife.Core | Journalisation, configuration, sérialisation, localisation, pool d'objets, TimerX, traçage |
| NewLife.Net | Pile réseau haute performance, WebSocket/codecs, réutilisation de ports |
| NewLife.Redis | Cache distribué / file de messages pour sessions partagées ou fan-out |
| Stardust | Enregistrement de nœuds, centre de configuration, APM, centre de publication |
| AntJob | Ordonnancement de tâches et calcul distribué via ClientBase |
| NewLife.MQTT / Modbus / IoT | Collecte des protocoles d'appareils puis remontée via Remoting |

## Contributions et retours

Bienvenus : rapports de bugs, PR de performance, nouveaux codecs, exemples de contrôleurs, documentation et nouvelles traductions.
Dans une issue, précisez : environnement, framework cible, reproduction minimale et extrait de journaux.

---
## Carte de l'écosystème

NewLife est un écosystème en couches de projets open source versionnés indépendamment. La carte ci-dessous est reprise à l'identique par tous les dépôts NewLife ; chaque projet renvoie à son dépôt et à son paquet NuGet principal.

| Projet | Rôle | Téléchargements |
| --- | --- | --- |
| **Fondation** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | Journalisation, configuration, cache, réseau, sérialisation, ordonnancement et traçage APM. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | Intergiciel de données : tables uniques à l'échelle de dizaines de milliards, partitionnement automatique, séparation lecture/écriture et plus de 10 bases de données. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | Bibliothèque réseau hautes performances : 1,77 M pps / 14,5 Gbps sur une machine (TCP 1KB, 2026-10), 4 M connexions TCP persistantes. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | Communication protocolaire et framework RPC pour applications client/serveur et appareils IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | Installe des applications .NET comme démons système : services Windows et systemd Linux. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | Modèles de projet `dotnet new` pour console, web, API, service, données et IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **Données et messagerie** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Client Redis avec une latence en microsecondes, des millions d'opérations/s et des files de messages intégrées. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | Client RocketMQ entièrement managé pour Apache RocketMQ et Alibaba Cloud Message Queue. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | Client et serveur MQTT, y compris la connectivité Alibaba Cloud IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | Base de données hybride en C# : embarquée ou en réseau, relationnelle, séries temporelles, file de messages et KV réunis. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | Pilote et couche d'assistance MySql construits sur la pile de données NewLife. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web, IA et distribué** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | Framework rapide de plateforme d'administration : permissions, SSO, serveur OAuth et échafaudage CRUD. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | Plateforme de services distribués : gestion des nœuds, APM, centre de configuration, registre et centre de publication. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | Plateforme de tâches distribuées et d'ordonnancement de données en temps réel avec partitionnement, reprises et backfill. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | Client unifié pour 46 fournisseurs de LLM, plus ChatAI, une application web de chat prête à l'emploi. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **Industriel et IoT** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | Bibliothèque standard IoT définissant les contrats de protocole et les abstractions d'appareils. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | Implémentation Modbus TCP/RTU/ASCII au-dessus de NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | Protocole pour automate Siemens au-dessus de NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | Protocole pour automate Omron au-dessus de NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | Protocole pour automate Mitsubishi Melsec au-dessus de NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | Client OPC pour l'accès aux données industrielles. | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | Pile de protocoles BACnet pour l'automatisation des bâtiments. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | Plateforme IoT open source légère avec gestion des passerelles et des appareils. | — |
| **Télématique embarquée** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | Protocole de positionnement de véhicules JT/T 808 : plus de 70 corps de message, 2011/2013/2019. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | Protocole plateforme à plateforme JT/T 809 pour les autorités de transport. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | Vidéo embarquée JT/T 1078 : analyse RTP, remuxage FLV/fMP4 et diffusion en direct. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **Composants métier** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | Un seul paquet pour Excel, Word, PPT, PDF et plus de 15 formats de documents. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | Audio multiplateforme : codecs, E/S d'appareils, DSP, conteneurs et streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | Codecs vidéo, formats de conteneurs, traitement des images et streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | Interface unifiée pour Baidu, AMap, Tencent et Tianditu avec géocodage et conversion de coordonnées. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | Géolocalisation IPv4 avec une base de données hors ligne mappée en mémoire et une API en ligne en secours. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **Outils et produits** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | Boîte à outils du développeur : réseau, port série, cryptographie, regex, Modbus et MQTT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | Système de gestion de code source Git auto-hébergé. | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | Installe automatiquement le runtime .NET correspondant lorsqu'il est lancé depuis un hôte plus ancien. | — |

> Les badges indiquent le paquet principal de chaque projet ; les paquets de thèmes, d'extensions et satellites sont répertoriés sur leurs pages NuGet.Les plus de 30 paquets non listés ici (FTP, LoRa, extensions Office, pilotes de protocoles, etc.) sont disponibles dans l'[organisation NewLifeX](https://github.com/NewLifeX) et sur [newlifex.com](https://newlifex.com).

---
## Équipe

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

NewLife a été fondée en 2002 et œuvre depuis plus de deux décennies dans l'écosystème .NET et IoT, maintient près de 100 projets open source (dont 34 principaux) et fournit des solutions matérielles et logicielles intégrées aux secteurs de l'énergie, de la logistique, de la santé, du contrôle industriel et d'Internet. Notre devise : « l'apprentissage ne connaît pas l'ancienneté — celui qui maîtrise devient maître ».

Les composants open source ont été téléchargés <!--nuget-total:start-->6.48M<!--nuget-total:end--> fois depuis NuGet (à l'échelle de l'écosystème, en date de 2026-10).

Site web : https://newlifex.com · Organisation open source : https://github.com/NewLifeX

<details>
<summary>Compte officiel WeChat : 智能大石头 (scannez pour suivre)</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

---
## Licence

MIT © NewLife. L'usage commercial et le développement dérivé sont les bienvenus.
