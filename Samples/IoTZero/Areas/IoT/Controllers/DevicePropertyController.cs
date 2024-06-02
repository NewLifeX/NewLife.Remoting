using System.ComponentModel;
using IoT.Data;
using IoTZero.Services;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.IoT;
using NewLife.IoT.ThingModels;
using NewLife.Serialization;
using NewLife.Web;
using XCode.Membership;

namespace IoTZero.Areas.IoT.Controllers;

[IoTArea]
[Menu(0, false)]
public class DevicePropertyController : EntityController<DeviceProperty>
{
    private readonly ThingService _thingService;

    static DevicePropertyController()
    {
        LogOnChange = true;

        ListFields.RemoveField("UnitName", "Length", "Rule", "Readonly", "Locked", "Timestamp", "FunctionId", "Remark");
        ListFields.RemoveCreateField();

        ListFields.TraceUrl("TraceId");

        {
            var df = ListFields.GetField("DeviceName") as ListField;
            df.Url = "/IoT/Device?Id={DeviceId}";
        }
        {
            var df = ListFields.GetField("Name") as ListField;
            df.Url = "/IoT/DeviceData?deviceId={DeviceId}&name={Name}";
        }
        {
            var df = ListFields.AddDataField("Value", "Unit") as ListField;
        }
        {
            var df = ListFields.AddDataField("Switch", "Enable") as ListField;
            df.DisplayName = "翻转";
            df.Url = "/IoT/DeviceProperty/Switch?id={Id}";
            df.DataAction = "action";
            df.DataVisible = e => (e as DeviceProperty).Type.EqualIgnoreCase("bool");
        }
    }

    public DevicePropertyController(ThingService thingService) => _thingService = thingService;

    protected override Boolean Valid(DeviceProperty entity, DataObjectMethodType type, Boolean post)
    {
        var fs = type switch
        {
            DataObjectMethodType.Insert => AddFormFields,
            DataObjectMethodType.Update => EditFormFields,
            _ => null,
        };

        if (fs != null)
        {
            var df = fs.FirstOrDefault(e => e.Name == "Type");
            if (df != null)
            {
                // 基础类型，加上所有产品类型
                var dic = new Dictionary<String, String>(TypeHelper.GetIoTTypes(true), StringComparer.OrdinalIgnoreCase);

                if (!entity.Type.IsNullOrEmpty() && !dic.ContainsKey(entity.Type)) dic[entity.Type] = entity.Type;
                df.DataSource = e => dic;
            }
        }

        return base.Valid(entity, type, post);
    }

    protected override IEnumerable<DeviceProperty> Search(Pager p)
    {
        var deviceId = p["deviceId"].ToInt(-1);
        var name = p["name"];

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return DeviceProperty.Search(deviceId, name, start, end, p["Q"], p);
    }

    [EntityAuthorize(PermissionFlags.Insert)]
    public async Task<ActionResult> Switch(Int32 id)
    {
        var msg = "";
        var entity = DeviceProperty.FindById(id);
        if (entity != null && entity.Enable)
        {
            var value = entity.Value.ToBoolean();
            value = !value;
            entity.Value = value + "";
            entity.Update();

            var model = new PropertyModel { Name = entity.Name, Value = value };

            // 执行远程调用
            var dp = entity;
            if (dp != null)
            {
                var input = new
                {
                    model.Name,
                    model.Value,
                };

                var rs = await _thingService.InvokeServiceAsync(entity.Device, "SetProperty", input.ToJson(), DateTime.Now.AddSeconds(5), 5000);
                if (rs != null && rs.Status >= ServiceStatus.已完成)
                    msg = $"{rs.Status} {rs.Data}";
            }
        }

        return JsonRefresh("成功！" + msg, 1000);
    }
}