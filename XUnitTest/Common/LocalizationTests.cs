using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Clients;
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
            cluster.WriteLog("[{0}]Cluster connecting: {1}", "Test", "svc");
            Assert.Equal("[Test]集群连接：svc", log4.Message);
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

    private sealed class TestHost : ApiHost { }

    private sealed class TestClient : ClientBase { }
    #endregion
}
