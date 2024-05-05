﻿namespace NewLife.Remoting.Models;

/// <summary>登录事件参数</summary>
public class LoginEventArgs(LoginRequest? request, LoginResponse? response) : EventArgs
{
    /// <summary>请求</summary>
    public LoginRequest? Request { get; set; } = request;

    /// <summary>响应</summary>
    public LoginResponse? Response { get; set; } = response;
}
