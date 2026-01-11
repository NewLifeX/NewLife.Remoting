namespace NewLife.Remoting.Clients;

/// <summary>客户端设置接口</summary>
/// <remarks>
/// 定义客户端连接服务端所需的基本配置信息，包括服务端地址、客户端编码和密钥。
/// 实现类通常用于持久化配置信息，支持自动注册时下发编码和密钥。
/// </remarks>
public interface IClientSetting
{
    /// <summary>服务端地址</summary>
    /// <remarks>支持http/tcp/udp协议，支持客户端负载均衡，多地址逗号分隔</remarks>
    String Server { get; set; }

    /// <summary>客户端编码</summary>
    /// <remarks>设备编码DeviceCode，或应用标识AppId</remarks>
    String Code { get; set; }

    /// <summary>客户端密钥</summary>
    /// <remarks>设备密钥DeviceSecret，或应用密钥AppSecret</remarks>
    String? Secret { get; set; }

    /// <summary>保存数据</summary>
    /// <remarks>在自动注册或密钥下发后，将配置持久化到存储介质</remarks>
    void Save();
}
