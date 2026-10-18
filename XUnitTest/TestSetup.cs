using System.Globalization;
using System.Runtime.CompilerServices;
using NewLife;

namespace XUnitTest;

/// <summary>测试进程级初始化</summary>
/// <remarks>
/// 固定英文区域，避免机器区域设置影响异常与日志断言：
/// 组件运行期消息的英文原文是词表键，断言直接针对原文；中文/繁体译文由 <see cref="LocalizationTests"/> 单独验证。
/// </remarks>
internal static class TestSetup
{
    [ModuleInitializer]
    internal static void Init()
    {
        Localization.Culture = CultureInfo.GetCultureInfo("en-US");
        Localization.LogCulture = CultureInfo.GetCultureInfo("en-US");
    }
}
