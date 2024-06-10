namespace NewLife.Remoting.Clients;

/// <summary>客户端设置</summary>
public interface IClientSetting
{
    /// <summary>服务端地址</summary>
    String Server { get; set; }

    /// <summary>客户端编码</summary>
    String Code { get; set; }

    /// <summary>客户端密钥</summary>
    String? Secret { get; set; }

    /// <summary>保存数据</summary>
    void Save();
}
