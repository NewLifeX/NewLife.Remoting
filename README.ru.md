# NewLife.Remoting — Единый фреймворк удалённого взаимодействия (RPC + HTTP + WebSocket + SRMP)

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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

NewLife.Remoting — единая инфраструктура удалённого взаимодействия стека NewLife, поставляемая двумя пакетами NuGet:

| Пакет | Назначение | Охват |
|:---|:-----|:------|
| **NewLife.Remoting** | Основная библиотека связи | RPC-клиент/сервер, кодеки протоколов, управление соединениями, потоковые вызовы, базовые классы клиента, конвейер команд, автообновление |
| **NewLife.Remoting.Extensions** | Библиотека расширений | Базовые классы контроллеров сервера, хост Minimal API, службы устройств, аутентификация по токену, шина ответов на команды |

Исходный код: https://github.com/NewLifeX/NewLife.Remoting  
> NuGet: `NewLife.Remoting` / `NewLife.Remoting.Extensions`

> ⚠️ Этот перевод может отставать от оригинала; актуальная версия — [английский README](README.md).

## Индекс документации

| Документ | Описание |
|------|------|
| [Doc/需求文档.md](Doc/需求文档.md) | Требования: видение, цели, функциональные требования (RPC/STRM/CLNT/UPGD/SRV/CMD/AOT/L10N/EXT) |
| [Doc/架构设计.md](Doc/架构设计.md) | Архитектура: слои, ключевые модули, управление памятью, стратегия тестирования, проектные решения |
| [Doc/SRV-服务端架构.md](Doc/SRV-服务端架构.md) | Архитектура сервера: сервисный слой, основные сценарии (вход/регистрация/выход/heartbeat/обновление/события/команды), жизненный цикл сессий, кластер |
| [Doc/功能清单.md](Doc/功能清单.md) | Учёт функций: статус, текущие задачи и список исправлений |
| [Doc/竞品分析.md](Doc/竞品分析.md) | Анализ конкурентов: сравнение RPC-фреймворков и доставки команд IoT |
| [Doc/SRMP.MD](Doc/SRMP.MD) | Спецификация протокола SRMP |
| [Doc/RPC内存管理设计.md](Doc/RPC内存管理设计.md) | Управление памятью без копирования |
| [Doc/自动更新机制.md](Doc/自动更新机制.md) | Полная схема автообновления |
| [Doc/重试策略.md](Doc/重试策略.md) | Политика повторов: `IRetryPolicy` и экспоненциальная задержка |
| [Doc/AOT兼容性分析.md](Doc/AOT兼容性分析.md) | Область поддержки NativeAOT, <!--aot-scenarios:start-->23<!--aot-scenarios:end--> проверенных сценариев (smoke <!--aot-smoke:start-->23/23<!--aot-smoke:end-->), рекомендации и известные ограничения |
| [Doc/多语言本地化接入.md](Doc/多语言本地化接入.md) | Английские сообщения времени выполнения, таблицы `Locales/*.yml` и руководство по `L.T` |
| [Doc/README多语言规范.md](Doc/README多语言规范.md) | Правила порядка, скрипты и CI-контроль для 11 языков README |

---

## Ключевые возможности

| Возможность | Описание |
|:---------|------|
| **Ядро RPC** | Протокол SRMP, JSON/бинарные кодеки, конвейер запросов, клиент/сервер, управление кластером, метаданные, повторы, полностью асинхронный сервер, трассировка по TraceId |
| **STRM-потоки** | Server-Streaming: `IAsyncEnumerable<T>` передаётся поэлементно через SRMP и SSE |
| **CLNT-клиент** | Вход/heartbeat/обновление/команды/события в одном месте, автоматическое переключение WS/SSE |
| **UPGD-обновление** | Поиск → загрузка → проверка → распаковка → замена → перезапуск |
| **SRV-сервер** | ApiController/BaseController/BaseDeviceController, TokenService, IDeviceService; один и тот же протокол доступен через MVC и Minimal API (зеркальные реализации, закреплённые тестами) |
| **CMD-команды** | SessionManager + широковещательная шина событий + каналы WS/SSE/Ping |
| **L10N-локализация** | Сообщения времени выполнения используют английский как исходный текст; `Locales/*.yml` содержат встроенные таблицы для 31 языка (включая упрощённый и традиционный китайский), при отсутствии перевода — английский; без ResourceManager и сателлитных сборок |
| **Поддержка AOT** | Постоянный анализ AOT для net8.0+ (0 предупреждений в основной библиотеке и Extensions) + собственный генератор кода (таблица диспетчеризации, потоковые насосы, сохранение сигнатур); NativeAOT проверен на <!--aot-scenarios:start-->23<!--aot-scenarios:end--> сценариях, 11 сценариях Minimal API и 6 сценариях устройств XCode (см. документ AOT §8) |

## Быстрый старт

**Пять строк на сервере, пять на клиенте — RPC работает:**

```csharp
// === Сервер ===
var server = new ApiServer(12345);
server.Register<MyController>();
server.Start();

public class MyController : ApiController
{
    public String Ping(String name) => $"Hello {name}, {DateTime.Now:HH:mm:ss}";
    // Потоки: возврат IAsyncEnumerable<T> передаёт элементы по одному
    public async IAsyncEnumerable<String> StreamLogs(Int32 count)
    {
        for (var i = 0; i < count; i++)
        {
            await Task.Delay(100);
            yield return $"Log #{i}: {DateTime.Now:HH:mm:ss}";
        }
    }
}

// === Клиент ===
var client = new ApiClient("tcp://127.0.0.1:12345");
client.Open();

// Обычный вызов
var rs = await client.InvokeAsync<String>("My/Ping", new { name = "dev" });

// Потоковый вызов — приём отправок сервера
await foreach (var log in client.InvokeStreamAsync<String>("My/StreamLogs", new { count = 10 }))
    Console.WriteLine(log);

// Метаданные (TraceId подставляется автоматически)
client.Headers["TenantId"] = "tenant-01";
```

## Производительность

| Показатель | Типично | Пик в лаборатории |
|:----|:------:|:--------:|
| Долгих TCP-соединений | 10 000 | 4 000 000 |
| Пропускная способность RPC | 100 000 TPS | 22 660 000 TPS |
| Задержка одного RPC | ~41 мкс | — |
| Поддерживаемые платформы | net45 / net461 / netstandard2.0 / netstandard2.1 / net6.0 / net8.0 / net10.0 (в соответствии с NewLife.Core v12; NativeAOT с net8.0) |

---
## Совместная работа с другими компонентами NewLife

| Компонент | Ценность |
| ---- | -------- |
| NewLife.Core | Логирование, конфигурация, сериализация, локализация, пул объектов, TimerX, трассировка |
| NewLife.Net | Высокопроизводительный сетевой стек, WebSocket/кодеки, повторное использование портов |
| NewLife.Redis | Распределённый кэш / очередь сообщений для общих сессий и fan-out |
| Stardust | Регистрация узлов, центр конфигурации, APM, центр публикации |
| AntJob | Планирование задач и распределённые вычисления через ClientBase |
| NewLife.MQTT / Modbus / IoT | Сбор данных устройств и передача через Remoting |

## Участие и обратная связь

Приветствуются: сообщения об ошибках, PR по производительности, новые кодеки, примеры контроллеров, документация и новые переводы.
В issue указывайте: окружение, целевую платформу, минимальный пример и фрагмент журнала.

---
## Матрица проектов NewLife

Все проекты по умолчанию поддерживают net10.0/net8.0/netstandard2.1/netstandard2.0/net4.62/net4.5; ветка 2024.0801 также поддерживает net4.0/net2.0.

| Проект | Год | Описание |
| :--: | :--: | ---- |
| [NewLife.Core](https://github.com/NewLifeX/X) | 2002 | Основная библиотека: логирование, конфигурация, кэш, сеть, сериализация, APM |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | 2005 | Промежуточное ПО данных: десятки миллиардов строк в таблице, шардирование, разделение чтения и записи |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 2005 | Сетевая библиотека: 22,66 млн tps / миллионы соединений |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 2011 | Этот проект: двухпротокольный фреймворк |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 2010 | Платформа быстрой разработки: права / OAuth / SSO |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | 2008 | Установка служб / демон / Systemd |
| [NewLife.Zero](https://github.com/NewLifeX/NewLife.Zero) | 2020 | Шаблоны проектов |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | 2017 | Высокопроизводительный клиент Redis / очередь сообщений |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 2018 | Полностью управляемый клиент RocketMQ |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | 2019 | MqttClient/MqttServer для IoT |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | 2022 | Стандартные модели связи IoT |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | 2022 | Семейство протоколов Modbus |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | 2022 | Связь с ПЛК Siemens |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 2022 | Компоненты агрегации карт |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | 2023 | Аудиокодеки |
| Stardust | 2018 | Платформа распределённых сервисов (реестр/конфигурация/APM/публикация) |
| AntJob | 2019 | Распределённая платформа больших данных |
| Другие | - | См. официальный сайт |

---
## Команда и авторские права

![XCode](https://newlifex.com/logo.png)

NewLife основана в 2002 году: более 80 открытых проектов и более 4 миллионов загрузок NuGet. Все проекты распространяются по лицензии MIT, их можно свободно изменять и распространять (без указания авторства).  
Сайт: https://newlifex.com  
GitHub: https://github.com/NewLifeX  
QQ-группы: 1600800 / 1600838  

> `NewLife началась в 2002 году; некоторым проектам более 20 лет, а репозитории хранят все коммиты с 2010 года.`

---
## Лицензия

MIT © NewLife. Коммерческое использование и доработка приветствуются.
