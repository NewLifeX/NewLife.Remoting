using NewLife.Data;

namespace NewLife.Remoting.Models;

/// <summary>设备信息接口</summary>
public interface IDeviceModel
{
    /// <summary>编码</summary>
    String Code { get; set; }

    /// <summary>名称</summary>
    String Name { get; set; }

    /// <summary>启用</summary>
    Boolean Enable { get; set; }
}

/// <summary>设备信息接口（扩展）</summary>
public interface IDeviceModel2 : IDeviceModel
{
    /// <summary>密钥</summary>
    String Secret { get; set; }

    /// <summary>心跳周期</summary>
    Int32 Period { get; set; }

    /// <summary>创建设备历史</summary>
    /// <param name="action"></param>
    /// <param name="success"></param>
    /// <param name="content"></param>
    /// <returns></returns>
    IExtend CreateHistory(String action, Boolean success, String content);

    /// <summary>创建在线对象</summary>
    /// <param name="sessionId">会话标识</param>
    /// <returns></returns>
    IOnlineModel CreateOnline(String sessionId);
}