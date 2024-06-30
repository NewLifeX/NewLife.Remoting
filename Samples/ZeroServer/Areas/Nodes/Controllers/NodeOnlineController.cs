﻿using Microsoft.AspNetCore.Mvc;
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
public class NodeOnlineController : NodeEntityController<NodeOnline>
{
    static NodeOnlineController()
    {
        //LogOnChange = true;

        ListFields.RemoveField("ProvinceName", "Token");
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

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<NodeOnline> Search(Pager p)
    {
        var nodeId = p["nodeId"].ToInt(-1);
        var rids = p["areaId"].SplitAsInt("/");
        var provinceId = rids.Length > 0 ? rids[0] : -1;
        var cityId = rids.Length > 1 ? rids[1] : -1;
        var category = p["category"];

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return NodeOnline.Search(nodeId, provinceId, cityId, category, start, end, p["Q"], p);
    }
}