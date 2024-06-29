using IoT.Data;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using XCode.Membership;

namespace IoTZero.Areas.IoT.Controllers;

/// <summary>设备在线</summary>
[Menu(70, true, Icon = "fa-table")]
[IoTArea]
public class DeviceOnlineController : EntityController<DeviceOnline>
{
    static DeviceOnlineController()
    {
        //LogOnChange = true;

        ListFields.RemoveField("Token");
        ListFields.RemoveCreateField().RemoveRemarkField();

        {
            var df = ListFields.GetField("DeviceName") as ListField;
            df.Url = "/IoT/Device?Id={DeviceId}";
        }
        {
            var df = ListFields.AddListField("property", "Pings");
            df.DisplayName = "属性";
            df.Url = "/IoT/DeviceProperty?deviceId={DeviceId}";
        }
        {
            var df = ListFields.AddListField("data", "Pings");
            df.DisplayName = "数据";
            df.Url = "/IoT/DeviceData?deviceId={DeviceId}";
        }
    }

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<DeviceOnline> Search(Pager p)
    {
        var productId = p["productId"].ToInt(-1);

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return DeviceOnline.Search(null, productId, start, end, p["Q"], p);
    }
}