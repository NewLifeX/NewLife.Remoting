using Microsoft.AspNetCore.Mvc;
using NewLife.Data;
using NewLife.Remoting.Extensions.Models;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Web;

namespace NewLife.Remoting.Extensions;

/// <summary>OAuth控制器基类。向应用提供令牌颁发与验证服务</summary>
/// <param name="tokenService"></param>
/// <param name="setting"></param>
[Route("[controller]/[action]")]
public abstract class BaseOAuthController(ITokenService tokenService, ITokenSetting setting) : ControllerBase
{
    /// <summary>验证密码颁发令牌，或刷新令牌</summary>
    /// <param name="model"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    [ApiFilter]
    public virtual TokenModel Token([FromBody] TokenInModel model)
    {
        var set = setting;

        if (model.grant_type.IsNullOrEmpty()) model.grant_type = "password";

        var ip = HttpContext.GetUserHost();
        var clientId = model.ClientId;

        try
        {
            // 密码模式
            if (model.grant_type == "password")
            {
                if (model.UserName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.UserName));

                var app = Authorize(model.UserName, model.Password, set.AutoRegister, ip);

                var tokenModel = tokenService.IssueToken(app.Name, clientId);

                app.WriteLog("Authorize", true, model.UserName, ip, clientId);

                return tokenModel;
            }
            // 刷新令牌
            else if (model.grant_type == "refresh_token")
            {
                if (model.refresh_token.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.refresh_token));

                var (jwt, ex) = tokenService.DecodeToken(model.refresh_token);

                // 验证应用
                var name = jwt?.Subject;
                var app = name.IsNullOrEmpty() ? null : FindByName(name);
                if (app == null || !app.Enable)
                    ex ??= new ApiException(ApiCode.Forbidden, $"无效应用[{name}]");

                if (jwt != null && clientId.IsNullOrEmpty()) clientId = jwt.Id;

                if (ex != null)
                {
                    app?.WriteLog("RefreshToken", false, ex.ToString(), ip, clientId);
                    throw ex;
                }

                var tokenModel = tokenService.IssueToken(app!.Name, clientId);

                //app.WriteHistory("RefreshToken", true, model.refresh_token, ip, clientId);

                return tokenModel;
            }
            else
                throw new NotSupportedException($"未支持 grant_type={model.grant_type}");
        }
        catch (Exception ex)
        {
            var app = FindByName(model.UserName!);
            app?.WriteLog("Authorize", false, ex.ToString(), ip, clientId);

            throw;
        }
    }

    /// <summary>验证应用密码，不存在时新增</summary>
    /// <param name="username"></param>
    /// <param name="password"></param>
    /// <param name="autoRegister"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    protected IAppModel Authorize(String username, String? password, Boolean autoRegister, String? ip = null)
    {
        if (username.IsNullOrEmpty()) throw new ArgumentNullException(nameof(username));
        //if (password.IsNullOrEmpty()) throw new ArgumentNullException(nameof(password));

        // 查找应用
        var app = FindByName(username);
        // 查找或创建应用，避免多线程创建冲突
        app ??= Register(username, password, autoRegister, ip);
        if (app == null) throw new ApiException(ApiCode.NotFound, $"[{username}]无效！");

        //// 检查黑白名单
        //if (!app.ValidSource(ip))
        //    throw new ApiException(ApiCode.Forbidden, $"应用[{username}]禁止{ip}访问！");

        // 检查应用有效性
        if (!app.Enable) throw new ApiException(ApiCode.Forbidden, $"[{username}]已禁用！");
        //if (!app.Secret.IsNullOrEmpty() && password != app.Secret) throw new ApiException(401, $"非法访问应用[{username}]！");
        if (!OnAuthorize(app, password, ip)) throw new ApiException(ApiCode.Forbidden, $"非法访问[{username}]！");

        return app;
    }

    /// <summary>查找应用</summary>
    /// <param name="username"></param>
    /// <returns></returns>
    protected abstract IAppModel FindByName(String username);

    /// <summary>应用注册</summary>
    /// <param name="username"></param>
    /// <param name="password"></param>
    /// <param name="autoRegister"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    protected abstract IAppModel Register(String username, String? password, Boolean autoRegister, String? ip = null);

    /// <summary>应用鉴权</summary>
    /// <param name="app"></param>
    /// <param name="password"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    protected virtual Boolean OnAuthorize(IAppModel app, String? password, String? ip = null) => app.Authorize(password, ip);

    /// <summary>根据令牌获取应用信息，同时也是验证令牌是否有效</summary>
    /// <param name="token"></param>
    /// <returns></returns>
    [ApiFilter]
    public Object Info(String token)
    {
        var (jwt, ex) = tokenService.DecodeToken(token);
        if (ex != null) throw ex;

        var name = jwt?.Subject;
        var app = name.IsNullOrEmpty() ? default : FindByName(name);
        if (app is IModel model)
            return new
            {
                Id = model["Id"],
                Name = name,
                DisplayName = model["DisplayName"],
                Category = model["Category"],
            };
        else
            return new
            {
                app?.Name,
            };
    }
}