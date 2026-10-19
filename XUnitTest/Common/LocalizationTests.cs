using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using NewLife;
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
}
