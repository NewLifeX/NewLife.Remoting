using System.ComponentModel;
using IoT.Data;
using NewLife.Cube;
using NewLife.Log;
using NewLife.Web;
using XCode;
using XCode.Membership;

namespace IoTZero.Areas.IoT.Controllers;

[IoTArea]
//[DisplayName("设备管理")]
[Menu(80, true, Icon = "fa-mobile")]
public class DeviceController : EntityController<Device>
{
    private readonly ITracer _tracer;

    static DeviceController()
    {
        LogOnChange = true;

        ListFields.RemoveField("Secret", "Uuid", "ProvinceId", "IP", "Period", "Address", "Location", "Logins", "LastLogin", "LastLoginIP", "OnlineTime", "RegisterTime", "Remark", "AreaName");
        ListFields.RemoveCreateField();
        ListFields.RemoveUpdateField();

        {
            var df = ListFields.AddListField("history", "Online");
            df.DisplayName = "历史";
            df.Url = "/IoT/DeviceHistory?deviceId={Id}";
        }

        {
            var df = ListFields.AddListField("property", "Online");
            df.DisplayName = "属性";
            df.Url = "/IoT/DeviceProperty?deviceId={Id}";
        }

        {
            var df = ListFields.AddListField("data", "Online");
            df.DisplayName = "数据";
            df.Url = "/IoT/DeviceData?deviceId={Id}";
        }
    }

    public DeviceController(ITracer tracer) => _tracer = tracer;

    protected override IEnumerable<Device> Search(Pager p)
    {
        var id = p["Id"].ToInt(-1);
        if (id > 0)
        {
            var node = Device.FindById(id);
            if (node != null) return new[] { node };
        }

        var productId = p["productId"].ToInt(-1);
        var groupId = p["groupId"].ToInt(-1);
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        //// 如果没有指定产品和主设备，则过滤掉子设备
        //if (productId < 0 && parentId < 0) parentId = 0;

        return Device.Search(productId, groupId, enable, start, end, p["Q"], p);
    }

    protected override Int32 OnInsert(Device entity)
    {
        var rs = base.OnInsert(entity);

        entity.Product?.Fix();
        return rs;
    }

    protected override Int32 OnUpdate(Device entity)
    {
        var rs = base.OnUpdate(entity);

        entity.Product?.Fix();

        return rs;
    }

    protected override Int32 OnDelete(Device entity)
    {
        // 删除设备时需要顺便把设备属性删除
        var dpList = DeviceProperty.FindAllByDeviceId(entity.Id);
        _ = dpList.Delete();

        var rs = base.OnDelete(entity);

        entity.Product?.Fix();

        return rs;
    }
}