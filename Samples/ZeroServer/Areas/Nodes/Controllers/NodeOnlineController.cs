using Microsoft.AspNetCore.Mvc;
using Zero.Data.Nodes;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using XCode.Membership;
using static Zero.Data.Nodes.NodeOnline;

namespace ZeroServer.Areas.Nodes.Controllers;

/// <summary>节点在线</summary>
[Menu(20, true, Icon = "fa-table")]
[NodesArea]
public class NodeOnlineController : EntityController<NodeOnline>
{
    static NodeOnlineController()
    {
        //LogOnChange = true;

        //ListFields.RemoveField("Id", "Creator");
        ListFields.RemoveCreateField().RemoveRemarkField();

        //{
        //    var df = ListFields.GetField("Code") as ListField;
        //    df.Url = "?code={Code}";
        //}
        //{
        //    var df = ListFields.AddListField("devices", null, "Onlines");
        //    df.DisplayName = "查看设备";
        //    df.Url = "Device?groupId={Id}";
        //    df.DataVisible = e => (e as NodeOnline).Devices > 0;
        //}
        //{
        //    var df = ListFields.GetField("Kind") as ListField;
        //    df.GetValue = e => ((Int32)(e as NodeOnline).Kind).ToString("X4");
        //}
        //ListFields.TraceUrl("TraceId");
    }

    //private readonly ITracer _tracer;

    //public NodeOnlineController(ITracer tracer)
    //{
    //    _tracer = tracer;
    //}

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<NodeOnline> Search(Pager p)
    {
        //var deviceId = p["deviceId"].ToInt(-1);
        //var enable = p["enable"]?.Boolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return NodeOnline.Search(start, end, p["Q"], p);
    }
}