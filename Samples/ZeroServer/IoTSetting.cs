using System.ComponentModel;
using NewLife;
using NewLife.Configuration;
using NewLife.Remoting.Models;
using NewLife.Security;
using XCode.Configuration;

namespace ZeroServer;

/// <summary>配置</summary>
[Config("ZeroServer")]
public class IoTSetting : Config<IoTSetting>, ITokenSetting
{
    #region 静态
    static IoTSetting() => Provider = new DbConfigProvider { UserId = 0, Category = "IoTServer" };
    #endregion

    #region 属性
    ///// <summary>MQTT服务端口。默认1883</summary>
    //[Description("MQTT服务端口。默认1883")]
    //public Int32 MqttPort { get; set; } = 1883;

    ///// <summary>MQTT证书地址。设置了才启用安全连接，默认为空</summary>
    //[Description("MQTT证书地址。设置了才启用安全连接，默认为空")]
    //public String MqttCertPath { get; set; }

    ///// <summary>MMQTT证书密码</summary>
    //[Description("MQTT证书密码")]
    //public String MqttCertPassword { get; set; }
    #endregion

    #region 设备管理
    /// <summary>令牌密钥。用于生成JWT令牌的算法和密钥，如HS256:ABCD1234</summary>
    [Description("令牌密钥。用于生成JWT令牌的算法和密钥，如HS256:ABCD1234")]
    [Category("设备管理")]
    public String TokenSecret { get; set; }

    /// <summary>令牌有效期。默认2*3600秒</summary>
    [Description("令牌有效期。默认2*3600秒")]
    [Category("设备管理")]
    public Int32 TokenExpire { get; set; } = 2 * 3600;

    /// <summary>会话超时。默认600秒</summary>
    [Description("会话超时。默认600秒")]
    [Category("设备管理")]
    public Int32 SessionTimeout { get; set; } = 600;

    /// <summary>自动注册。允许客户端自动注册，默认true</summary>
    [Description("自动注册。允许客户端自动注册，默认true")]
    [Category("设备管理")]
    public Boolean AutoRegister { get; set; } = true;
    #endregion

    #region 数据存储
    /// <summary>历史数据保留时间。默认30天</summary>
    [Description("历史数据保留时间。默认30天")]
    [Category("数据存储")]
    public Int32 DataRetention { get; set; } = 30;
    #endregion

    #region 方法
    /// <summary>加载时触发</summary>
    protected override void OnLoaded()
    {
        if (TokenSecret.IsNullOrEmpty() || TokenSecret.Split(':').Length != 2) TokenSecret = $"HS256:{Rand.NextString(16)}";

        base.OnLoaded();
    }
    #endregion
}