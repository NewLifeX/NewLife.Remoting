namespace NewLife.Remoting.Clients;

/// <summary>事件提供者接口</summary>
/// <remarks>
/// 定义客户端上报事件的能力，支持向服务端推送各类事件信息。
/// 事件类型包括info/alert/error等，用于设备状态上报、告警通知等场景。
/// </remarks>
public interface IEventProvider
{
    /// <summary>写事件</summary>
    /// <param name="type">事件类型。info/alert/error等</param>
    /// <param name="name">事件名称。例如LightOpen、DeviceStart</param>
    /// <param name="remark">事件内容。详细描述信息</param>
    /// <returns>是否成功加入事件队列</returns>
    Boolean WriteEvent(String type, String name, String? remark);
}

/// <summary>事件客户端助手</summary>
public static class EventProviderHelper
{
    /// <summary>写信息事件</summary>
    /// <param name="client">事件提供者</param>
    /// <param name="name">事件名称</param>
    /// <param name="remark">事件内容</param>
    public static void WriteInfoEvent(this IEventProvider client, String name, String? remark) => client.WriteEvent("info", name, remark);

    /// <summary>写错误事件</summary>
    /// <param name="client">事件提供者</param>
    /// <param name="name">事件名称</param>
    /// <param name="remark">事件内容</param>
    public static void WriteErrorEvent(this IEventProvider client, String name, String? remark) => client.WriteEvent("error", name, remark);
}