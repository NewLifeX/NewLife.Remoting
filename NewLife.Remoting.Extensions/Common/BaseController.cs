using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife.Log;
using NewLife.Remoting.Extensions.Models;
using NewLife.Remoting.Extensions.Services;
using NewLife.Serialization;
using NewLife.Web;
using IWebFilter = Microsoft.AspNetCore.Mvc.Filters.IActionFilter;

namespace NewLife.Remoting.Extensions;

/// <summary>业务接口控制器基类</summary>
/// <remarks>
/// 提供统一的令牌解码验证架构
/// </remarks>
[ApiFilter]
[Route("[controller]")]
public abstract class BaseController : ControllerBase, IWebFilter
{
    #region 属性
    /// <summary>令牌</summary>
    public String? Token { get; private set; }

    /// <summary>令牌对象</summary>
    public JwtBuilder Jwt { get; set; } = null!;

    /// <summary>用户主机</summary>
    public String UserHost => HttpContext.GetUserHost();

    private IDictionary<String, Object?>? _args;
    private readonly TokenService _tokenService;
    private readonly ITokenSetting _setting;
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    /// <param name="serviceProvider"></param>
    public BaseController(IServiceProvider serviceProvider)
    {
        _tokenService = serviceProvider.GetRequiredService<TokenService>();
        _setting = serviceProvider.GetRequiredService<ITokenSetting>();
    }
    #endregion

    #region 令牌验证
    void IWebFilter.OnActionExecuting(ActionExecutingContext context)
    {
        _args = context.ActionArguments;

        var token = Token = ApiFilterAttribute.GetToken(context.HttpContext);

        try
        {
            var rs = !token.IsNullOrEmpty() && OnAuthorize(token);

            if (!rs && context.ActionDescriptor is ControllerActionDescriptor act && !act.MethodInfo.IsDefined(typeof(AllowAnonymousAttribute)))
            {
                throw new ApiException(ApiCode.Forbidden, "认证失败");
            }
        }
        catch (Exception ex)
        {
            var traceId = DefaultSpan.Current?.TraceId;
            context.Result = ex is ApiException aex
                ? new JsonResult(new { code = aex.Code, data = aex.Message, traceId })
                : new JsonResult(new { code = 500, data = ex.Message, traceId });

            WriteError(ex, context);
        }
    }

    /// <summary>验证令牌，并获取应用</summary>
    /// <param name="token"></param>
    /// <returns></returns>
    protected virtual Boolean OnAuthorize(String token)
    {
        var jwt = _tokenService.DecodeToken(token, _setting.TokenSecret);
        Jwt = jwt;

        return jwt != null;
    }

    void IWebFilter.OnActionExecuted(ActionExecutedContext context)
    {
        if (context.Exception != null) WriteError(context.Exception, context);
    }

    private void WriteError(Exception ex, ActionContext context)
    {
        // 拦截全局异常，写日志
        var action = context.HttpContext.Request.Path + "";
        if (context.ActionDescriptor is ControllerActionDescriptor act) action = $"{act.ControllerName}/{act.ActionName}";

        OnWriteError(action, ex?.GetTrue() + Environment.NewLine + _args?.ToJson(true));
    }

    /// <summary>输出错误日志</summary>
    /// <param name="action"></param>
    /// <param name="message"></param>
    protected virtual void OnWriteError(String action, String message) => WriteLog(action, false, message);
    #endregion

    #region 方法
    ///// <summary>设备登录</summary>
    ///// <param name="model"></param>
    ///// <returns></returns>
    //[AllowAnonymous]
    //[HttpPost(nameof(Login))]
    //public LoginResponse Login(NewLife.Remoting.Models.LoginRequest model) => _deviceService.Login(model as LoginInfo, "Http", UserHost);

    #endregion

    #region 辅助
    /// <summary>写日志</summary>
    /// <param name="action"></param>
    /// <param name="success"></param>
    /// <param name="message"></param>
    protected virtual void WriteLog(String action, Boolean success, String message) => XTrace.WriteLine($"[{action}]{message}");
    #endregion
}