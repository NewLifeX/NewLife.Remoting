namespace NewLife.Remoting.Clients;

/// <summary>客户端功能特性</summary>
[Flags]
public enum ClientFeatures : Byte
{
    /// <summary>登录</summary>
    Login = 1 << 0,

    /// <summary>注销</summary>
    Logout = 1 << 1,

    /// <summary>心跳</summary>
    Ping = 1 << 2,

    /// <summary>下行通知</summary>
    Notify = 1 << 3,

    /// <summary>更新</summary>
    Upgrade = 1 << 4,

    /// <summary>所有</summary>
    All = 0xFF,
}
