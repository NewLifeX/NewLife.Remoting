namespace NewLife.Remoting.Models;

/// <summary>命令响应模型</summary>
public class CommandReplyModel
{
    /// <summary>服务编号</summary>
    public Int64 Id { get; set; }

    /// <summary>状态</summary>
    public CommandStatus Status { get; set; }

    /// <summary>返回数据</summary>
    public String? Data { get; set; }

    /// <summary>设备编码。用于响应广播路由</summary>
    public String? Code { get; set; }

    /// <summary>发起方节点标识。用于集群精确定位 SendCommand 发起实例</summary>
    public String? SenderNodeId { get; set; }
}