namespace NewLife.Remoting.Models;

/// <summary>登录事件参数</summary>
public class LoginEventArgs(ILoginRequest? request, ILoginResponse? response) : EventArgs
{
    /// <summary>请求</summary>
    public ILoginRequest? Request { get; set; } = request;

    /// <summary>响应</summary>
    public ILoginResponse? Response { get; set; } = response;
}
