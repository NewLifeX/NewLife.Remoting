using IoT.Data;
using Microsoft.AspNetCore.Mvc;
using NewLife.Cube;
using NewLife.Web;
using XCode.Membership;

namespace IoTZero.Areas.IoT.Controllers;

/// <summary>设备分组。物联网平台支持建立设备分组，分组中可包含不同产品下的设备。通过设备组来进行跨产品管理设备。</summary>
[Menu(50, true, Icon = "fa-table")]
[IoTArea]
public class DeviceGroupController : EntityController<DeviceGroup>
{
    static DeviceGroupController()
    {
        LogOnChange = true;

        ListFields.RemoveField("UpdateUserId", "UpdateIP");
        ListFields.RemoveCreateField().RemoveRemarkField();
    }

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<DeviceGroup> Search(Pager p)
    {
        var name = p["name"];
        var parentid = p["parentid"].ToInt(-1);

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return DeviceGroup.Search(name, parentid, start, end, p["Q"], p);
    }

    [EntityAuthorize(PermissionFlags.Update)]
    public ActionResult Refresh()
    {
        DeviceGroup.Refresh();

        return JsonRefresh("成功！", 1);
    }
}