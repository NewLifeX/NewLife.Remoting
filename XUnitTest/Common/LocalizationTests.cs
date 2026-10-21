using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Clients;
using NewLife.Remoting.Extensions;
using Xunit;

namespace XUnitTest.Common;

/// <summary>组件多语言词表验证</summary>
/// <remarks>
/// 本类用例会临时改写 <see cref="Localization.Culture"/>（进程级全局状态），与其它用例串行执行，
/// 避免并行用例读到被临时改写的语言。英文原文是词表键，缺译文时回退英文。
/// 组件词表由源生成器产出的模块初始化器注册，首次访问程序集时触发；此处显式触发扩展库的模块初始化，
/// 避免用例执行顺序影响注册时机。
/// </remarks>
[Collection("GlobalState")]
public class LocalizationTests
{
    static LocalizationTests() => RuntimeHelpers.RunModuleConstructor(typeof(NewLife.Remoting.Extensions.ApiFilterAttribute).Module.ModuleHandle);

    /// <summary>NewLife.Remoting 词条</summary>
    private const String CoreKey = "Server address is not specified.";

    /// <summary>NewLife.Remoting.Extensions 词条</summary>
    private const String ExtKey = "Authentication failed";

    #region 中文
    [Fact(DisplayName = "多语言：简体中文按英文原文查到组件译文")]
    public void Translate_zh_Hans()
    {
        var saved = Localization.Culture;
        try
        {
            Localization.Culture = CultureInfo.GetCultureInfo("zh-CN");

            Assert.Equal("未指定服务端地址。", Localization.Translate(CoreKey));
            Assert.Equal("认证失败", Localization.Translate(ExtKey));
        }
        finally
        {
            Localization.Culture = saved;
        }
    }

    [Fact(DisplayName = "多语言：繁体中文由 zh-Hant 统一承接 zh-TW")]
    public void Translate_zh_Hant()
    {
        var saved = Localization.Culture;
        try
        {
            Localization.Culture = CultureInfo.GetCultureInfo("zh-TW");

            Assert.Equal("未指定伺服器位址。", Localization.Translate(CoreKey));
            Assert.Equal("驗證失敗", Localization.Translate(ExtKey));
        }
        finally
        {
            Localization.Culture = saved;
        }
    }
    #endregion

    #region 其它语言
    [Fact(DisplayName = "多语言：德语等非中文语言词表已注册")]
    public void Translate_de()
    {
        var saved = Localization.Culture;
        try
        {
            Localization.Culture = CultureInfo.GetCultureInfo("de-DE");

            Assert.Equal("Serveradresse ist nicht angegeben.", Localization.Translate(CoreKey));
            Assert.Equal("Authentifizierung fehlgeschlagen", Localization.Translate(ExtKey));
        }
        finally
        {
            Localization.Culture = saved;
        }
    }

    /// <summary>与 locales.json 保持一致的 31 种内置语言</summary>
    public static readonly String[] Locales =
    [
        "ar", "bn", "cs", "da", "de", "el", "es", "fa", "fi", "fr", "he", "hi", "hu", "id", "it", "ja",
        "ko", "ms", "nb", "nl", "pl", "pt", "ro", "ru", "sv", "th", "tr", "uk", "vi", "zh-Hans", "zh-Hant",
    ];

    /// <summary>全部内置语言的测试数据</summary>
    public static IEnumerable<Object[]> LocaleData => Locales.Select(e => new Object[] { e });

    [Theory(DisplayName = "多语言：全部内置语言的词表均在运行时注册")]
    [MemberData(nameof(LocaleData))]
    public void Translate_all_locales(String name)
    {
        var saved = Localization.Culture;
        try
        {
            Localization.Culture = CultureInfo.GetCultureInfo(name);

            Assert.NotEqual(CoreKey, Localization.Translate(CoreKey));
            Assert.NotEqual(ExtKey, Localization.Translate(ExtKey));
        }
        finally
        {
            Localization.Culture = saved;
        }
    }
    #endregion

    #region 回退
    [Theory(DisplayName = "多语言：无词表的语言回退英文原文")]
    [InlineData("en-US")]
    [InlineData("sw-KE")]
    public void Translate_fallback(String name)
    {
        var saved = Localization.Culture;
        try
        {
            Localization.Culture = CultureInfo.GetCultureInfo(name);

            Assert.Equal(CoreKey, Localization.Translate(CoreKey));
            Assert.Equal(ExtKey, Localization.Translate(ExtKey));
        }
        finally
        {
            Localization.Culture = saved;
        }
    }

    [Fact(DisplayName = "多语言：未知键回退英文原文")]
    public void Translate_unknown_key() => Assert.Equal("Not a translated message.", Localization.Translate("Not a translated message."));
    #endregion

    #region 元数据
    /// <summary>NewLife.Remoting 程序集描述（词表源文，允许其它语言回退英文）</summary>
    private const String MainDescription = "Unified RPC, HTTP, WebSocket and SRMP communication for client-server and IoT applications.";

    /// <summary>NewLife.Remoting.Extensions 程序集描述（词表源文，允许其它语言回退英文）</summary>
    private const String ExtDescription = "Device access controllers, token services and session management extensions for NewLife.Remoting.";

    [Fact(DisplayName = "多语言：程序集标题与描述为英文源文，中文有译文")]
    public void Translate_assembly_metadata()
    {
        var main = typeof(ApiClient).Assembly;
        var ext = typeof(ApiFilterAttribute).Assembly;

        Assert.Equal("NewLife Remoting Library", main.GetCustomAttribute<AssemblyTitleAttribute>()?.Title);
        Assert.Equal(MainDescription, main.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description);
        Assert.Equal("NewLife Remoting Extensions", ext.GetCustomAttribute<AssemblyTitleAttribute>()?.Title);
        Assert.Equal(ExtDescription, ext.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description);

        var saved = Localization.Culture;
        try
        {
            Localization.Culture = CultureInfo.GetCultureInfo("zh-CN");

            Assert.Equal("NewLife统一远程通信框架", Localization.Translate("NewLife Remoting Library"));
            Assert.Equal("提供RPC、HTTP、WebSocket与SRMP统一通信能力", Localization.Translate(MainDescription));
            Assert.Equal("远程通信服务扩展", Localization.Translate("NewLife Remoting Extensions"));
            Assert.Equal("提供设备接入控制器、令牌服务与会话管理扩展", Localization.Translate(ExtDescription));

            // 元数据只维护少数语言，其余语言回退英文源文
            Localization.Culture = CultureInfo.GetCultureInfo("de-DE");
            Assert.Equal("NewLife Remoting Library", Localization.Translate("NewLife Remoting Library"));
            Assert.Equal(MainDescription, Localization.Translate(MainDescription));
        }
        finally
        {
            Localization.Culture = saved;
        }
    }
    #endregion

    #region 占位符
    [Fact(DisplayName = "多语言：带占位符的模板按译文格式化")]
    public void Translate_placeholders()
    {
        var saved = Localization.Culture;
        try
        {
            Localization.Culture = CultureInfo.GetCultureInfo("zh-CN");

            var text = Localization.Translate("Cannot find service named [{0}]!", "Api/Info");
            Assert.Equal("无法找到名为[Api/Info]的服务！", text);
        }
        finally
        {
            Localization.Culture = saved;
        }
    }
    #endregion

    #region 日志
    [Fact(DisplayName = "多语言：日志拼接名称前缀后仍按模板翻译")]
    public void WriteLog_translates_template_after_name_prefix()
    {
        var saved = Localization.Culture;
        var savedLog = Localization.LogCulture;
        try
        {
            Localization.Culture = CultureInfo.GetCultureInfo("zh-CN");
            Localization.LogCulture = CultureInfo.GetCultureInfo("zh-CN");

            var log1 = new CaptureLog();
            var host = new TestHost { Name = "Test", Log = log1 };
            host.WriteLog("Slow call [{0}] took {1:n0}ms", "Api/Info", 1_234);
            Assert.Equal("[Test]慢调用[Api/Info]，耗时1,234ms", log1.Message);

            var log2 = new CaptureLog();
            var upgrade = new Upgrade { Name = "Test", Log = log2 };
            upgrade.WriteLog("Update succeeded!");
            Assert.Equal("[Test]更新成功！", log2.Message);

            var log3 = new CaptureLog();
            var client = new TestClient { Name = "Test", Log = log3 };
            client.WriteLog("Logout: {0} {1}", "Api/Info", "old");
            Assert.Equal("[Test]注销：Api/Info old", log3.Message);

            var log4 = new CaptureLog();
            var cluster = new ClientSingleCluster<String> { Name = "Test", Log = log4 };
            cluster.WriteLog("Cluster connecting: {0}", "svc");
            Assert.Equal("[Test]集群连接：svc", log4.Message);

            var log5 = new CaptureLog();
            var pool = new ClientPoolCluster<String> { Name = "Test", Log = log5 };
            pool.WriteLog("Cluster balancing: {0}", "svc");
            Assert.Equal("[Test]集群均衡：svc", log5.Message);
        }
        finally
        {
            Localization.Culture = saved;
            Localization.LogCulture = savedLog;
        }
    }

    [Fact(DisplayName = "多语言：自定义 ILog 实现同样先翻译并格式化再拼接前缀")]
    public void WriteLog_translates_for_custom_ilog()
    {
        var saved = Localization.Culture;
        var savedLog = Localization.LogCulture;
        try
        {
            Localization.Culture = CultureInfo.GetCultureInfo("zh-CN");
            Localization.LogCulture = CultureInfo.GetCultureInfo("zh-CN");

            var log = new RawLog();
            var host = new TestHost { Name = "Test", Log = log };
            host.WriteLog("Slow call [{0}] took {1:n0}ms", "Api/Info", 1_234);

            Assert.Equal("[Test]慢调用[Api/Info]，耗时1,234ms", Assert.Single(log.Messages));
        }
        finally
        {
            Localization.Culture = saved;
            Localization.LogCulture = savedLog;
        }
    }

    [Fact(DisplayName = "多语言：日志语言独立于界面语言")]
    public void WriteLog_uses_log_culture()
    {
        var saved = Localization.Culture;
        var savedLog = Localization.LogCulture;
        try
        {
            Localization.Culture = CultureInfo.GetCultureInfo("de-DE");
            Localization.LogCulture = CultureInfo.GetCultureInfo("zh-CN");

            var log = new CaptureLog();
            var host = new TestHost { Name = "Test", Log = log };
            host.WriteLog("Update succeeded!");

            Assert.Equal("[Test]更新成功！", log.Message);
            Assert.Equal("Serveradresse ist nicht angegeben.", Localization.Translate(CoreKey));
        }
        finally
        {
            Localization.Culture = saved;
            Localization.LogCulture = savedLog;
        }
    }

    private sealed class CaptureLog : Logger
    {
        public String? Message { get; private set; }

        protected override void OnWrite(LogLevel level, String format, params Object?[] args) => Message = Format(format, args);
    }

    private sealed class RawLog : ILog
    {
        public List<String> Messages { get; } = [];
        public Boolean Enable { get; set; } = true;
        public LogLevel Level { get; set; } = LogLevel.All;

        public void Write(LogLevel level, String format, params Object?[] args) => Messages.Add(args.Length == 0 ? format : $"{format}|{String.Join(",", args)}");
        public void Debug(String format, params Object?[] args) => Write(LogLevel.Debug, format, args);
        public void Info(String format, params Object?[] args) => Write(LogLevel.Info, format, args);
        public void Warn(String format, params Object?[] args) => Write(LogLevel.Warn, format, args);
        public void Error(String format, params Object?[] args) => Write(LogLevel.Error, format, args);
        public void Fatal(String format, params Object?[] args) => Write(LogLevel.Fatal, format, args);
    }

    private sealed class TestHost : ApiHost { }

    private sealed class TestClient : ClientBase { }
    #endregion

    #region 请求语言
    [Fact(DisplayName = "多语言：Accept-Language 按 q 权重选择语言")]
    public void RequestCulture_parse_quality()
    {
        Assert.Equal("zh-CN", RequestCultureScope.ParseAcceptLanguage("fr-FR;q=0.5, zh-CN;q=0.9, en;q=0.8")!.Name);
        Assert.Equal("de-DE", RequestCultureScope.ParseAcceptLanguage("de-DE")!.Name);
        Assert.Null(RequestCultureScope.ParseAcceptLanguage(""));
        Assert.Null(RequestCultureScope.ParseAcceptLanguage("invalid-!!"));
    }

    [Fact(DisplayName = "多语言：按 Accept-Language 切换请求语言并在释放后还原")]
    public void RequestCulture_scope_switch_and_restore()
    {
        var saved = Localization.Culture;
        var savedLog = Localization.LogCulture;
        var savedUI = CultureInfo.CurrentUICulture;
        try
        {
            Localization.Culture = null;
            Localization.LogCulture = null;
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

            using (RequestCultureScope.Create("fr-FR;q=0.5, zh-CN;q=0.9, en;q=0.8"))
            {
                Assert.Equal("zh-CN", CultureInfo.CurrentUICulture.Name);
                Assert.Equal("未指定服务端地址。", Localization.Translate(CoreKey));
            }

            Assert.Equal("en-US", CultureInfo.CurrentUICulture.Name);
        }
        finally
        {
            Localization.Culture = saved;
            Localization.LogCulture = savedLog;
            CultureInfo.CurrentUICulture = savedUI;
        }
    }

    [Fact(DisplayName = "多语言：全局固定 Culture 时忽略请求语言")]
    public void RequestCulture_scope_ignores_fixed_culture()
    {
        var saved = Localization.Culture;
        var savedUI = CultureInfo.CurrentUICulture;
        try
        {
            Localization.Culture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

            using (RequestCultureScope.Create("zh-CN"))
            {
                Assert.Equal("en-US", CultureInfo.CurrentUICulture.Name);
                Assert.Equal(CoreKey, Localization.Translate(CoreKey));
            }
        }
        finally
        {
            Localization.Culture = saved;
            CultureInfo.CurrentUICulture = savedUI;
        }
    }

    [Fact(DisplayName = "多语言：ApiFilter 按请求头切换语言并在执行后还原")]
    public void ApiFilter_applies_request_culture()
    {
        var saved = Localization.Culture;
        var savedUI = CultureInfo.CurrentUICulture;
        try
        {
            Localization.Culture = null;
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

            var http = new DefaultHttpContext();
            http.Request.Headers["Accept-Language"] = "de-DE";
            var filter = new ApiFilterAttribute();
            filter.OnActionExecuting(CreateExecutingContext(http));

            try
            {
                Assert.Equal("de-DE", CultureInfo.CurrentUICulture.Name);
                Assert.Equal("Serveradresse ist nicht angegeben.", Localization.Translate(CoreKey));
            }
            finally
            {
                filter.OnActionExecuted(CreateExecutedContext(http));
            }

            Assert.Equal("en-US", CultureInfo.CurrentUICulture.Name);
        }
        finally
        {
            Localization.Culture = saved;
            CultureInfo.CurrentUICulture = savedUI;
        }
    }

    [Fact(DisplayName = "多语言：宿主已配置 RequestLocalization 时 ApiFilter 不覆盖宿主语言")]
    public void ApiFilter_defers_to_host_request_culture()
    {
        var saved = Localization.Culture;
        var savedUI = CultureInfo.CurrentUICulture;
        try
        {
            Localization.Culture = null;
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");

            var http = new DefaultHttpContext();
            http.Request.Headers["Accept-Language"] = "de-DE";
            http.Features.Set<IRequestCultureFeature>(new RequestCultureFeature(new RequestCulture(CultureInfo.GetCultureInfo("fr-FR")), null));

            var filter = new ApiFilterAttribute();
            filter.OnActionExecuting(CreateExecutingContext(http));

            try
            {
                Assert.Equal("fr-FR", CultureInfo.CurrentUICulture.Name);
                Assert.Equal("Échec de l'authentification", Localization.Translate(ExtKey));
            }
            finally
            {
                filter.OnActionExecuted(CreateExecutedContext(http));
            }

            Assert.Equal("fr-FR", CultureInfo.CurrentUICulture.Name);
        }
        finally
        {
            Localization.Culture = saved;
            CultureInfo.CurrentUICulture = savedUI;
        }
    }

    private static ActionExecutingContext CreateExecutingContext(HttpContext http)
    {
        var actionContext = new ActionContext(http, new RouteData(), new ControllerActionDescriptor());
        return new ActionExecutingContext(actionContext, [], new Dictionary<String, Object?>(), new Object());
    }

    private static ActionExecutedContext CreateExecutedContext(HttpContext http)
    {
        var actionContext = new ActionContext(http, new RouteData(), new ControllerActionDescriptor());
        return new ActionExecutedContext(actionContext, [], new Object());
    }
    #endregion
}
