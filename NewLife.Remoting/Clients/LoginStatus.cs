namespace NewLife.Remoting.Clients;

/// <summary>登录状态</summary>
public enum LoginStatus
{
    /// <summary>就绪</summary>
    Ready = 0,

    /// <summary>正在登录</summary>
    LoggingIn = 1,

    /// <summary>已登录</summary>
    LoggedIn = 2,

    /// <summary>已注销</summary>
    LoggedOut = 3,
}
