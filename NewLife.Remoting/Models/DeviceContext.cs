using NewLife.Data;

namespace NewLife.Remoting.Models;

/// <summary>设备上下文。该对象经由池化管理，勿保存</summary>
public class DeviceContext : IExtend
{
    #region 属性
    /// <summary>编码。设备编码或应用AppId，作为JWT的Subject</summary>
    public String? Code { get; set; }

    /// <summary>设备</summary>
    public IDeviceModel? Device { get; set; }

    /// <summary>在线信息</summary>
    public IOnlineModel? Online { get; set; }

    /// <summary>令牌</summary>
    public String? Token { get; set; }

    /// <summary>客户端标识</summary>
    public String? ClientId { get; set; }

    /// <summary>用户主机</summary>
    public String? UserHost { get; set; }

    /// <summary>扩展数据</summary>
    public IDictionary<String, Object?> Items { get; } = new Dictionary<String, Object?>();

    /// <summary>索引器</summary>
    public Object? this[String key] { get => Items.TryGetValue(key, out var v) ? v : null; set => Items[key] = value; }
    #endregion

    #region 方法
    /// <summary>重置</summary>
    public void Clear()
    {
        Code = null;
        Device = null;
        Online = null;
        Token = null;
        ClientId = null;
        UserHost = null;

        Items.Clear();
    }
    #endregion
}
