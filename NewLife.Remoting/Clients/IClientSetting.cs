namespace NewLife.Remoting.Clients;

/// <summary>客户端设置</summary>
public interface IClientSetting
{
    /// <summary>服务端地址。IoT服务平台地址</summary>
    String Server { get; set; }

    /// <summary>设备证书。在一机一密时手工填写，一型一密时自动下发</summary>
    String DeviceCode { get; set; }

    /// <summary>设备密钥。在一机一密时手工填写，一型一密时自动下发</summary>
    String? DeviceSecret { get; set; }

    /// <summary>保存数据</summary>
    void Save();
}
