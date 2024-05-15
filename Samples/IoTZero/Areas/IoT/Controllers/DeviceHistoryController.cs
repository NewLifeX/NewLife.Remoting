using IoT.Data;
using NewLife.Cube;
using NewLife.Web;
using XCode.Membership;

namespace IoTZero.Areas.IoT.Controllers;

[IoTArea]
[Menu(60, true)]
public class DeviceHistoryController : ReadOnlyEntityController<DeviceHistory>
{
    protected override IEnumerable<DeviceHistory> Search(Pager p)
    {
        var deviceId = p["deviceId"].ToInt(-1);
        var action = p["action"];

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        //if (start.Year < 2000)
        //{
        //    start = new DateTime(DateTime.Today.Year, 1, 1);
        //    p["dtStart"] = start.ToString("yyyy-MM-dd");
        //}

        if (start.Year < 2000)
        {
            using var split = DeviceHistory.Meta.CreateShard(DateTime.Today);
            return DeviceHistory.Search(deviceId, action, start, end, p["Q"], p);
        }
        else
            return DeviceHistory.Search(deviceId, action, start, end, p["Q"], p);
    }
}