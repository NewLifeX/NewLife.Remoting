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
🌐 [中文](Readme.MD) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

NewLife.Remoting est l'infrastructure de communication distante unifiée de la pile NewLife, distribuée en deux packages NuGet :

| Package | Rôle | Périmètre |
|:---|:-----|:------|
| **NewLife.Remoting** | Bibliothèque de communication principale | Client/serveur RPC, codecs de protocole, gestion des connexions, appels en streaming, classes de base client, pipeline de commandes, mise à jour automatique |
| **NewLife.Remoting.Extensions** | Bibliothèque d'extensions | Classes de base de contrôleurs serveur, hôte Minimal API, services de gestion d'appareils, authentification par jeton, bus de réponses aux commandes |

Code source : https://github.com/NewLifeX/NewLife.Remoting  
> NuGet : `NewLife.Remoting` / `NewLife.Remoting.Extensions`

> ⚠️ Cette traduction peut être en retard sur le [README chinois](Readme.MD), qui fait référence.

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
| Débit RPC | 100 000 TPS | 22 660 000 TPS |
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
## Matrice des projets NewLife

Tous les projets prennent en charge par défaut net10.0/net8.0/netstandard2.1/netstandard2.0/net4.62/net4.5 ; la branche 2024.0801 prend aussi en charge net4.0/net2.0.

| Projet | Année | Description |
| :--: | :--: | ---- |
| [NewLife.Core](https://github.com/NewLifeX/X) | 2002 | Bibliothèque principale : journalisation, configuration, cache, réseau, sérialisation, APM |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | 2005 | Middleware de données : dizaines de milliards de lignes, partitionnement, séparation lecture/écriture |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 2005 | Bibliothèque réseau : 22,66 M tps / millions de connexions |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 2011 | Ce projet : framework de communication bi-protocole |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 2010 | Plateforme de développement rapide : permissions / OAuth / SSO |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | 2008 | Installation de service / démon / Systemd |
| [NewLife.Zero](https://github.com/NewLifeX/NewLife.Zero) | 2020 | Modèles de projet |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | 2017 | Client Redis / file de messages haute performance |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 2018 | Client RocketMQ entièrement managé |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | 2019 | MqttClient/MqttServer pour l'IoT |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | 2022 | Modèles standards de communication IoT |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | 2022 | Famille de protocoles Modbus |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | 2022 | Communication avec les automates Siemens |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 2022 | Composants d'agrégation cartographique |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | 2023 | Codecs audio |
| Stardust | 2018 | Plateforme de services distribués (registre/config/APM/publication) |
| AntJob | 2019 | Plateforme distribuée de calcul big data |
| Autres | - | Voir le site officiel |

---
## Équipe et droits d'auteur

![XCode](https://newlifex.com/logo.png)

NewLife a été créé en 2002 : plus de 80 projets open source et plus de 4 millions de téléchargements NuGet cumulés. Tous les projets sont sous licence MIT et peuvent être modifiés et redistribués librement (aucune attribution requise).  
Site : https://newlifex.com  
GitHub : https://github.com/NewLifeX  
Groupes QQ : 1600800 / 1600838  

> `NewLife a démarré en 2002 ; certains projets ont plus de 20 ans et les dépôts conservent tous les commits depuis 2010.`

---
## Licence

MIT © NewLife. L'usage commercial et le développement dérivé sont les bienvenus.
