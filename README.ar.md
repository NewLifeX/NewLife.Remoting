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
| إنتاجية RPC | 100,000 TPS | 22,660,000 TPS |
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
## مصفوفة مشاريع NewLife

تدعم جميع المشاريع افتراضيًا net10.0/net8.0/netstandard2.1/netstandard2.0/net4.62/net4.5، وتدعم سلسلة 2024.0801 أيضًا net4.0/net2.0.

| المشروع | السنة | الوصف |
| :--: | :--: | ---- |
| [NewLife.Core](https://github.com/NewLifeX/X) | 2002 | المكتبة الأساسية: التسجيل والإعدادات والتخزين المؤقت والشبكات والتسلسل وAPM |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | 2005 | وسيط بيانات: عشرات المليارات من الصفوف في جدول واحد، تقسيم تلقائي، فصل القراءة/الكتابة |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 2005 | مكتبة شبكات: 22.66 مليون tps / ملايين الاتصالات |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 2011 | هذا المشروع: إطار اتصال ببروتوكولين |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 2010 | منصة تطوير سريع: الأذونات / OAuth / SSO |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | 2008 | تثبيت الخدمات / البرامج الخفية / Systemd |
| [NewLife.Zero](https://github.com/NewLifeX/NewLife.Zero) | 2020 | قوالب المشاريع |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | 2017 | عميل Redis / قائمة رسائل عالي الأداء |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 2018 | عميل RocketMQ مُدار بالكامل |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | 2019 | MqttClient/MqttServer لإنترنت الأشياء |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | 2022 | نماذج قياسية لاتصالات إنترنت الأشياء |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | 2022 | عائلة بروتوكولات Modbus |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | 2022 | الاتصال بوحدات Siemens PLC |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 2022 | مكوّنات تجميع الخرائط |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | 2023 | ترميز الصوت |
| Stardust | 2018 | منصة خدمات موزّعة (التسجيل/الإعدادات/APM/النشر) |
| AntJob | 2019 | منصة حوسبة بيانات ضخمة موزّعة |
| أخرى | - | راجع الموقع الرسمي |

---
## الفريق وحقوق النشر

![XCode](https://newlifex.com/logo.png)

تأسست NewLife عام 2002، وتضم أكثر من 80 مشروعًا مفتوح المصدر وأكثر من 4 ملايين تنزيل على NuGet. جميع المشاريع برخصة MIT ويمكن تعديلها وإعادة توزيعها بحرية (دون الحاجة إلى نسب المصدر).  
الموقع: https://newlifex.com  
GitHub: https://github.com/NewLifeX  
مجموعات QQ: 1600800 / 1600838  

> `بدأت NewLife عام 2002؛ بعض المشاريع يتجاوز عمرها 20 عامًا، وتحتفظ المستودعات بجميع التغييرات منذ 2010.`

---
## الرخصة

MIT © NewLife. نرحّب بالاستخدام التجاري والتطوير الثانوي.
