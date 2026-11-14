using System;
using System.Globalization;
using System.Linq;
using NewLife.Remoting.Services;
using Xunit;

namespace XUnitTest.Services;

/// <summary>设备历史动作与备注测试</summary>
public class DeviceActionsTests
{
    [Fact(DisplayName = "动作常量均为语言无关的英文唯一键")]
    public void ActionConstantsAreLanguageNeutral()
    {
        var fields = typeof(DeviceActions).GetFields()
            .Where(e => e.IsLiteral && e.FieldType == typeof(String))
            .ToArray();
        Assert.NotEmpty(fields);

        var values = fields.Select(e => (String)e.GetRawConstantValue()!).ToArray();
        Assert.All(values, v => Assert.False(v.Any(c => c >= 0x4E00 && c <= 0x9FFF), $"动作名不应包含中文：{v}"));
        Assert.Equal(values.Length, values.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact(DisplayName = "下线备注固定英文与时间格式")]
    public void FormatOfflineRemark()
    {
        var login = new DateTime(2026, 11, 14, 14, 2, 12);
        var active = new DateTime(2026, 11, 14, 15, 52, 35);

        var text = DeviceActions.FormatOfflineRemark("ApplicationExit", "DESKTOP-SUSG516", "2EE79C9F", login, active);

        Assert.Equal("ApplicationExit [DESKTOP-SUSG516/2EE79C9F] login at 2026-11-14 14:02:12, last active at 2026-11-14 15:52:35", text);
        Assert.DoesNotContain("]]", text);
        Assert.DoesNotContain("登录于", text);
    }

    [Fact(DisplayName = "下线备注不受当前区域设置影响")]
    public void FormatOfflineRemarkIgnoresCulture()
    {
        var oldCulture = CultureInfo.CurrentCulture;
        try
        {
            // 泰语默认佛历，验证时间固定使用公历格式
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("th-TH");

            var login = new DateTime(2026, 11, 14, 14, 2, 12);
            var text = DeviceActions.FormatOfflineRemark("ApplicationExit", "DESKTOP", null, login, DateTime.MinValue);

            Assert.Equal("ApplicationExit [DESKTOP] login at 2026-11-14 14:02:12", text);
        }
        finally
        {
            CultureInfo.CurrentCulture = oldCulture;
        }
    }

    [Fact(DisplayName = "下线备注缺省值")]
    public void FormatOfflineRemarkDefaults()
    {
        var text = DeviceActions.FormatOfflineRemark(null, null, null, DateTime.MinValue, DateTime.MinValue);

        Assert.Equal("Offline [-]", text);
    }
}
