using System.ComponentModel;
using NewLife;
using NewLife.Cube;

namespace ZeroServer.Areas.Nodes;

[DisplayName("节点管理")]
public class NodesArea : AreaBase
{
    public NodesArea() : base(nameof(NodesArea).TrimEnd("Area")) { }
}