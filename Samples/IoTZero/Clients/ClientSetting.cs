using System.ComponentModel;
using NewLife.Configuration;

namespace IoTEdge;

/// <summary>配置</summary>
[Config("IoTClient")]
public class ClientSetting : Config<ClientSetting>
{
    #region 属性
    /// <summary>服务端地址。IoT服务平台地址</summary>
    [Description("服务端地址。IoT服务平台地址")]
    public String Server { get; set; } = "http://localhost:1880";

    /// <summary>设备证书。在一机一密时手工填写，一型一密时自动下发</summary>
    [Description("设备证书。在一机一密时手工填写，一型一密时自动下发")]
    public String DeviceCode { get; set; }

    /// <summary>设备密钥。在一机一密时手工填写，一型一密时自动下发</summary>
    [Description("设备密钥。在一机一密时手工填写，一型一密时自动下发")]
    public String DeviceSecret { get; set; }

    /// <summary>产品证书。用于一型一密验证，对一机一密无效</summary>
    [Description("产品证书。用于一型一密验证，对一机一密无效")]
    public String ProductKey { get; set; } = "EdgeGateway";
    #endregion
}