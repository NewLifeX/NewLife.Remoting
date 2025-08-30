using NewLife.Log;

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
public interface IDeviceModel2 : IDeviceModel, ILogProvider
{
    /// <summary>密钥</summary>
    String Secret { get; set; }

    /// <summary>创建在线对象</summary>
    IOnlineModel CreateOnline(String sessionId);
}