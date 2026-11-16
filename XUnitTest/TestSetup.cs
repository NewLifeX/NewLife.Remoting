using System;
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
        // 集成测试大量依赖 Api/All 与完整 Api/Info，进程级开启诊断；生产默认仍为 false
        NewLife.Remoting.ApiServer.DiagnosticsDefault = true;

        // 区域不可用时（icu 缺失且运行在 globalization-invariant 模式）不能让模块初始化器抛异常：
        // 那会让整个测试程序集在发现阶段失败，表现为全量用例无声消失。此时保持进程区域不变，
        // 依赖固定区域文案的断言在那种环境下本就不该成立，交由对应用例自行失败。
        var culture = TryGetCulture("en-US");
        if (culture == null) return;

        Localization.Culture = culture;
        Localization.LogCulture = culture;
    }

    private static CultureInfo? TryGetCulture(String name)
    {
        try
        {
            return CultureInfo.GetCultureInfo(name);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}
