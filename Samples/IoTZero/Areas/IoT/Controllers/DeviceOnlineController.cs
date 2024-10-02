using System.ComponentModel;
using IoT.Data;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Remoting.Extensions.Services;
using NewLife.Remoting.Models;
using NewLife.Serialization;
using NewLife.Web;
using XCode.Membership;

namespace IoTZero.Areas.IoT.Controllers;

/// <summary>设备在线</summary>
[Menu(70, true, Icon = "fa-table")]
[IoTArea]
public class DeviceOnlineController : EntityController<DeviceOnline>
{
    private readonly IDeviceService _deviceService;

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

    public DeviceOnlineController(IDeviceService deviceService) => _deviceService = deviceService;

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

    [DisplayName("检查更新")]
    [EntityAuthorize((PermissionFlags)16)]
    public async Task<ActionResult> CheckUpgrade()
    {
        var ts = new List<Task>();
        foreach (var item in SelectKeys)
        {
            var online = DeviceOnline.FindById(item.ToInt());
            if (online?.Device != null)
            {
                //ts.Add(_starFactory.SendNodeCommand(online.Device.Code, "device/upgrade", null, 600, 0));
                var code = online.Device.Code;
                var cmd = new CommandModel
                {
                    //Code = online.Device.Code,
                    Command = "device/upgrade",
                    Expire = DateTime.Now.AddSeconds(600),
                };
                var queue = _deviceService.GetQueue(code);
                queue.Add(cmd.ToJson());
            }
        }

        await Task.WhenAll(ts);

        return JsonRefresh("操作成功！");
    }

    [DisplayName("执行命令")]
    [EntityAuthorize((PermissionFlags)16)]
    public async Task<ActionResult> Execute(String command, String argument)
    {
        if (GetRequest("keys") == null) throw new ArgumentNullException(nameof(SelectKeys));
        if (command.IsNullOrEmpty()) throw new ArgumentNullException(nameof(command));

        var ts = new List<Task<Int32>>();
        foreach (var item in SelectKeys)
        {
            var online = DeviceOnline.FindById(item.ToInt());
            if (online?.Device != null)
            {
                //ts.Add(_starFactory.SendNodeCommand(online.Device.Code, command, argument, 30, 0));
                var code = online.Device.Code;
                var cmd = new CommandModel
                {
                    //Code = online.Device.Code,
                    Command = command,
                    Argument = argument,
                    Expire = DateTime.Now.AddSeconds(30),
                };
                var queue = _deviceService.GetQueue(code);
                queue.Add(cmd.ToJson());
                ts.Add(Task.FromResult(1));
            }
        }

        var rs = await Task.WhenAll(ts);

        return JsonRefresh($"操作成功！下发指令{rs.Length}个，成功{rs.Count(e => e > 0)}个");
    }
}