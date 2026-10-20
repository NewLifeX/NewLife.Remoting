# NewLife.Remoting — إطار موحّد للاتصال عن بُعد (RPC + HTTP + WebSocket + SRMP)

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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md)
<!-- lang:end -->

NewLife.Remoting هو البنية التحتية الموحّدة للاتصال عن بُعد في منظومة NewLife، ويُقدَّم في حزمتي NuGet:

| الحزمة | الدور | التغطية |
|:---|:-----|:------|
| **NewLife.Remoting** | مكتبة الاتصال الأساسية | عميل/خادم RPC، ترميز البروتوكولات، إدارة الاتصالات، الاستدعاءات المتدفقة، الأصناف الأساسية للعميل، مسار الأوامر، التحديث التلقائي |
| **NewLife.Remoting.Extensions** | مكتبة الامتدادات | الأصناف الأساسية لوحدات تحكم الخادم، مضيف Minimal API، خدمات إدارة الأجهزة، المصادقة بالرموز، ناقل استجابات الأوامر |

الشيفرة المصدرية: https://github.com/NewLifeX/NewLife.Remoting  
> NuGet: `NewLife.Remoting` / `NewLife.Remoting.Extensions`

> ⚠️ قد تتأخر هذه الترجمة عن أحدث نسخة؛ المحتوى المعتمد هو [README الإنجليزي](README.md).

## فهرس الوثائق

| الوثيقة | الوصف |
|------|------|
| [Doc/需求文档.md](Doc/需求文档.md) | المتطلبات: الرؤية والأهداف والمتطلبات الوظيفية (RPC/STRM/CLNT/UPGD/SRV/CMD/AOT/L10N/EXT) |
| [Doc/架构设计.md](Doc/架构设计.md) | البنية: الطبقات والوحدات الأساسية وإدارة الذاكرة واستراتيجية الاختبار وقرارات التصميم |
| [Doc/SRV-服务端架构.md](Doc/SRV-服务端架构.md) | بنية الخادم: طبقة الخدمة والمسارات الأساسية (تسجيل الدخول/التسجيل/الخروج/نبضة الحياة/التحديث/الأحداث/الأوامر) ودورة حياة الجلسات والنشر العنقودي |
| [Doc/功能清单.md](Doc/功能清单.md) | تتبّع الميزات: الحالة والعناصر الجارية وقائمة المهام |
| [Doc/竞品分析.md](Doc/竞品分析.md) | تحليل المنافسين: مقارنة أطر RPC ودفع أوامر إنترنت الأشياء |
| [Doc/SRMP.MD](Doc/SRMP.MD) | مواصفة بروتوكول SRMP |
| [Doc/RPC内存管理设计.md](Doc/RPC内存管理设计.md) | إدارة الذاكرة بدون نسخ |
| [Doc/自动更新机制.md](Doc/自动更新机制.md) | التصميم الكامل للتحديث التلقائي |
| [Doc/重试策略.md](Doc/重试策略.md) | سياسة إعادة المحاولة: `IRetryPolicy` والتراجع الأُسّي |
| [Doc/AOT兼容性分析.md](Doc/AOT兼容性分析.md) | نطاق NativeAOT، <!--aot-scenarios:start-->23<!--aot-scenarios:end--> سيناريو مُختبَرًا (دخان <!--aot-smoke:start-->23/23<!--aot-smoke:end-->)، وإرشادات الاستخدام والحدود المعروفة |
| [Doc/多语言本地化接入.md](Doc/多语言本地化接入.md) | رسائل وقت التشغيل بالإنجليزية وجداول `Locales/*.yml` ودليل `L.T` |
| [Doc/README多语言规范.md](Doc/README多语言规范.md) | قواعد الترتيب وسكربتات التوليد وبوابة CI للغات README الإحدى عشرة |

---

## أبرز الميزات

| القدرة | الوصف |
|:---------|------|
| **نواة RPC** | بروتوكول SRMP، ترميز JSON/ثنائي، مسار الطلبات، العميل/الخادم، إدارة العنقود، نقل البيانات الوصفية، سياسة إعادة المحاولة، خادم غير متزامن بالكامل، تتبّع TraceId |
| **STRM البثّ** | بثّ من الخادم: إرسال `IAsyncEnumerable<T>` عنصرًا بعنصر عبر SRMP وSSE |
| **CLNT أساس العميل** | تسجيل الدخول/نبضة الحياة/التحديث/الأوامر/الأحداث في مكان واحد مع تبديل تلقائي بين WS وSSE |
| **UPGD التحديث** | كشف → تنزيل → تحقّق → فك ضغط → استبدال → إعادة تشغيل |
| **SRV أساس الخادم** | ApiController/BaseController/BaseDeviceController وTokenService وIDeviceService؛ تُقدَّم نقطة النهاية نفسها عبر MVC وMinimal API (تطبيقات متطابقة يثبّتها الاختبار) |
| **CMD الأوامر** | SessionManager + بثّ ناقل الأحداث + قنوات WS/SSE/Ping |
| **L10N التعدد اللغوي** | رسائل وقت التشغيل بالنص الإنجليزي كمصدر، وتوفّر `Locales/*.yml` 31 لغة مضمّنة (بما فيها الصينية المبسطة والتقليدية) مع الرجوع إلى الإنجليزية عند غياب الترجمة؛ دون ResourceManager أو تجميعات تابعة |
| **دعم AOT** | تحليل AOT دائم ابتداءً من net8.0 (صفر تحذيرات في المكتبة الأساسية وExtensions) + مولّد شيفرة خاص (جدول التوزيع، مضخّات البثّ، الحفاظ على التوقيعات)؛ تم التحقق بـ NativeAOT على <!--aot-scenarios:start-->23<!--aot-scenarios:end--> سيناريو و11 سيناريو لمضيف Minimal API و6 سيناريوهات لمسار أجهزة XCode (انظر وثيقة AOT §8) |

## البدء السريع

**خمسة أسطر في الخادم وخمسة في العميل ليبدأ RPC بالعمل:**

```csharp
// === الخادم ===
var server = new ApiServer(12345);
server.Register<MyController>();
server.Start();

public class MyController : ApiController
{
    public String Ping(String name) => $"Hello {name}, {DateTime.Now:HH:mm:ss}";
    // البثّ: إرجاع IAsyncEnumerable<T> يرسل العناصر واحدًا تلو الآخر
    public async IAsyncEnumerable<String> StreamLogs(Int32 count)
    {
        for (var i = 0; i < count; i++)
        {
            await Task.Delay(100);
            yield return $"Log #{i}: {DateTime.Now:HH:mm:ss}";
        }
    }
}

// === العميل ===
var client = new ApiClient("tcp://127.0.0.1:12345");
client.Open();

// استدعاء عادي
var rs = await client.InvokeAsync<String>("My/Ping", new { name = "dev" });

// استدعاء متدفق — استقبال ما يرسله الخادم عنصرًا بعنصر
await foreach (var log in client.InvokeStreamAsync<String>("My/StreamLogs", new { count = 10 }))
    Console.WriteLine(log);

// البيانات الوصفية (يُحقن TraceId تلقائيًا)
client.Headers["TenantId"] = "tenant-01";
```

## مؤشرات الأداء

| المؤشر | القيمة النموذجية | الذروة في المختبر |
|:----|:------:|:--------:|
| اتصالات TCP طويلة الأمد | 10,000 | 4,000,000 |
| إنتاجية RPC | 100,000 TPS | 455,000 msg/s |
| زمن استدعاء RPC واحد | ~41 ميكروثانية | — |
| الأطر المدعومة | net45 / net461 / netstandard2.0 / netstandard2.1 / net6.0 / net8.0 / net10.0 (متوافق مع NewLife.Core v12، وNativeAOT من net8.0) |

---
## التعاون مع مكوّنات NewLife الأخرى

| المكوّن | القيمة |
| ---- | -------- |
| NewLife.Core | التسجيل والإعدادات والتسلسل والتعدد اللغوي وتجمّع الكائنات وTimerX وتتبّع الأداء |
| NewLife.Net | مكدّس شبكات عالي الأداء وWebSocket/الترميز وإعادة استخدام المنافذ |
| NewLife.Redis | ذاكرة مؤقتة موزّعة / قائمة رسائل لمشاركة الجلسات أو توزيع الدفع |
| Stardust | تسجيل العقد ومركز الإعدادات وAPM ومركز النشر |
| AntJob | جدولة المهام والحوسبة الموزّعة عبر ClientBase |
| NewLife.MQTT / Modbus / IoT | جمع بروتوكولات الأجهزة ثم إرسالها صعودًا عبر Remoting |

## المساهمة والملاحظات

نرحّب بـ: تقارير الأخطاء / طلبات سحب لتحسين الأداء / ترميز بروتوكولات جديدة / أمثلة وحدات تحكم / إثراء الوثائق / ترجمات لغات جديدة.
يرجى في البلاغ تضمين: بيئة التشغيل والإطار الهدف وأصغر شيفرة لإعادة الإنتاج ومقتطف من السجل.

---
## خريطة المنظومة

NewLife منظومة متعدّدة الطبقات من مشاريع مفتوحة المصدر تُصدَر بإصدارات مستقلة. الخريطة أدناه مشتركة حرفيًا بين جميع مستودعات NewLife؛ وكل مشروع يرتبط بمستودعه وبحزمته الرئيسية على NuGet.

| المشروع | الوظيفة | التنزيلات |
| --- | --- | --- |
| **الأساس** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | التسجيل والتهيئة والتخزين المؤقت والشبكات والتسلسل والجدولة وتتبّع أداء APM. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | وسيط بيانات: جداول واحدة بمستوى عشرات المليارات من الصفوف، تقسيم تلقائي للجداول، فصل القراءة عن الكتابة، وأكثر من 10 قواعد بيانات. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | مكتبة شبكات عالية الأداء: 1.77 مليون pps / 14.5 Gbps على جهاز واحد (TCP 1KB، 2026-10)، و4 ملايين اتصال TCP طويل الأمد. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | إطار اتصال بروتوكولي وRPC لتطبيقات العميل/الخادم وأجهزة إنترنت الأشياء. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | يثبّت تطبيقات .NET كخدمات حارسة لنظام التشغيل: خدمات Windows وsystemd في Linux. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | قوالب مشاريع `dotnet new` للطرفية وweb وAPI والخدمات والبيانات وإنترنت الأشياء. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **البيانات والمراسلة** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | عميل Redis بزمن استجابة بالميكروثانية، وملايين العمليات في الثانية، وقوائم رسائل مدمجة. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | عميل RocketMQ مُدار بالكامل لـ Apache RocketMQ وخدمة رسائل Alibaba Cloud. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | عميل وخادم MQTT، بما في ذلك الاتصال بـ Alibaba Cloud IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | قاعدة بيانات هجينة بلغة C#: مدمجة أو شبكية، وعلائقية، وسلاسل زمنية، وقائمة رسائل، وKV في واحد. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | برنامج تشغيل MySql وطبقة مساعدة مبنية على مكدّس بيانات NewLife. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web والذكاء الاصطناعي والأنظمة الموزّعة** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | إطار منصة إدارة سريعة: الصلاحيات، والدخول الموحّد SSO، وخادم OAuth، وتوليد واجهات CRUD. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | منصة خدمات موزّعة: إدارة العقد، وAPM، ومركز الإعدادات، ومركز التسجيل، ومركز النشر. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | منصة موزّعة للمهام وجدولة البيانات الفورية، مع التقسيم وإعادة المحاولة وإعادة التعبئة. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | عميل موحّد لـ 46 مزوّد LLM بالإضافة إلى ChatAI، تطبيق ويب جاهز للمحادثة. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **الصناعة وإنترنت الأشياء** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | مكتبة معايير إنترنت الأشياء التي تعرّف عقود البروتوكولات وتجريدات الأجهزة. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | تنفيذ Modbus TCP/RTU/ASCII فوق NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | بروتوكول Siemens PLC فوق NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | بروتوكول Omron PLC فوق NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | بروتوكول Mitsubishi Melsec PLC فوق NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | عميل OPC للوصول إلى البيانات الصناعية. | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | مكدّس بروتوكول BACnet لأتمتة المباني. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | منصة إنترنت أشياء مفتوحة المصدر وخفيفة، مع إدارة البوابات والأجهزة. | — |
| **تليماتيكا المركبات** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | بروتوكول تحديد مواقع المركبات JT/T 808: أكثر من 70 جسم رسالة، 2011/2013/2019. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | بروتوكول JT/T 809 بين المنصات لجهات إدارة النقل. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | فيديو داخل المركبة JT/T 1078: تحليل RTP وإعادة التجميع إلى FLV/fMP4 والبث المباشر. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **مكوّنات المجال** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | حزمة واحدة لـ Excel وWord وPPT وPDF وأكثر من 15 صيغة مستندات. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | صوت عبر المنصات: مرمّزات، وإدخال/إخراج الأجهزة، وDSP، وحاويات، وبث. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | مرمّزات الفيديو وصيغ الحاويات ومعالجة الإطارات والبث. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | واجهة موحّدة فوق Baidu وAMap وTencent وTianditu مع الترميز الجغرافي وتحويل الإحداثيات. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | تحديد الموقع الجغرافي لعناوين IPv4 بقاعدة بيانات غير متصلة مُعيّنة في الذاكرة، وواجهة API عبر الإنترنت كبديل. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **الأدوات والمنتجات** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | صندوق أدوات المطوّر: الشبكات والمنفذ التسلسلي والتشفير والتعبيرات النمطية وModbus وMQTT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | نظام استضافة ذاتية لإدارة الشيفرة المصدرية Git. | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | يثبّت بيئة تشغيل .NET المطابقة تلقائيًا عند التشغيل من مضيف أقدم. | — |

> تعرض الشارات الحزمة الرئيسية لكل مشروع؛ أما حزم السمات والإضافات والحزم التابعة فمذكورة في صفحاتها على NuGet.الحزم التي تتجاوز 30 حزمة وغير المذكورة هنا (FTP وLoRa وإضافات Office وبرامج تشغيل البروتوكولات وغيرها) متاحة في [منظمة NewLifeX](https://github.com/NewLifeX) وعلى [newlifex.com](https://newlifex.com).

---
## الفريق

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

تأسّست NewLife في عام 2002، وأمضت أكثر من عقدين في منظومة .NET وإنترنت الأشياء، وتدير ما يقارب 100 مشروع مفتوح المصدر (34 منها مشاريع رئيسية)، وتقدّم حلولًا متكاملة للبرمجيات والأجهزة لقطاعات الكهرباء والخدمات اللوجستية والرعاية الصحية والتحكّم الصناعي والإنترنت. ونلتزم بمبدأ "لا أسبقية في التعلّم — الإتقان يكسب الاحترام".

نُزّلت المكوّنات مفتوحة المصدر <!--nuget-total:start-->6.48M<!--nuget-total:end--> مرة من NuGet (على مستوى المنظومة بأكملها، حتى 2026-10).

الموقع: https://newlifex.com · المنظمة مفتوحة المصدر: https://github.com/NewLifeX

<details>
<summary>حساب WeChat الرسمي: 智能大石头 (امسح للاشتراك)</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

---
## الرخصة

MIT © NewLife. نرحّب بالاستخدام التجاري والتطوير الثانوي.
