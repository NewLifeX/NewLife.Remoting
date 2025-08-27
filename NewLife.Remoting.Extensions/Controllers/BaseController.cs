using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Remoting.Services;
using NewLife.Serialization;
using NewLife.Web;
using IWebFilter = Microsoft.AspNetCore.Mvc.Filters.IActionFilter;

namespace NewLife.Remoting.Extensions;

/// <summary>业务接口控制器基类</summary>
/// <remarks>
/// 提供统一的令牌解码验证架构
/// </remarks>
/// <remarks>实例化</remarks>
/// <param name="serviceProvider"></param>
[ApiFilter]
[Route("[controller]")]
public abstract class BaseController(IServiceProvider serviceProvider) : ControllerBase, IWebFilter, ILogProvider
{
    #region 属性
    /// <summary>令牌</summary>
    public String? Token { get; set; }

    /// <summary>令牌对象</summary>
    public JwtBuilder Jwt { get; set; } = null!;

    /// <summary>客户端标识</summary>
    public String ClientId { get; set; } = null!;

    /// <summary>用户主机</summary>
    public String UserHost { get; set; } = null!;

    private readonly ITokenService _tokenService = serviceProvider.GetRequiredService<ITokenService>();
    private IDictionary<String, Object?>? _args;
    private static Action<String>? _setip;
    #endregion

    #region 构造
    static BaseController()
    {
        // 反射获取ManageProvider.UserHost的Set方法，避免直接引用XCode
        _setip = "ManageProvider".GetTypeEx()?.GetPropertyEx("UserHost")?.SetMethod?.CreateDelegate<Action<String>>();
    }
    #endregion

    #region 令牌验证
    void IWebFilter.OnActionExecuting(ActionExecutingContext context)
    {
        _args = context.ActionArguments;

        // 向ManageProvider.UserHost写入用户主机IP地址
        var ip = UserHost = HttpContext.GetUserHost();
        //ManageProvider.UserHost = UserHost;
        if (!ip.IsNullOrEmpty()) _setip?.Invoke(ip);

        var token = Token = ApiFilterAttribute.GetToken(context.HttpContext);

        try
        {
            if (context.ActionDescriptor is ControllerActionDescriptor act && !act.MethodInfo.IsDefined(typeof(AllowAnonymousAttribute)))
            {
                // 匿名访问接口无需验证。例如星尘Node的SendCommand接口，并不使用Node令牌，而是使用App令牌
                var rs = OnAuthorize(token, context);
                if (!rs) throw new ApiException(ApiCode.Unauthorized, "认证失败");
            }
        }
        catch (Exception ex)
        {
            var msg = ex.Message;

            // 特殊处理数据库异常，避免泄漏SQL语句
            if (ex.GetType().FullName == "XCode.Exceptions.XSqlException")
                msg = "数据库SQL错误";

            var traceId = DefaultSpan.Current?.TraceId;
            context.Result = ex is ApiException aex
                ? new JsonResult(new { code = aex.Code, message = msg, traceId })
                : new JsonResult(new { code = 500, message = msg, traceId });

            WriteError(ex, context);
        }
    }

    /// <summary>验证令牌，并获取Jwt对象，子类可借助Jwt.Subject获取设备</summary>
    /// <param name="token"></param>
    /// <returns></returns>
    [Obsolete("=>OnAuthorize(String token, ActionExecutingContext context)", true)]
    protected virtual Boolean OnAuthorize(String token) => OnAuthorize(token, null!);

    /// <summary>验证令牌，并获取Jwt对象，子类可借助Jwt.Subject获取设备</summary>
    /// <param name="token">访问令牌</param>
    /// <param name="context"></param>
    /// <returns></returns>
    protected virtual Boolean OnAuthorize(String token, ActionContext context)
    {
        if (token.IsNullOrEmpty()) return false;

        var (jwt, ex) = _tokenService.DecodeToken(token);
        Jwt = jwt;
        ClientId = jwt?.Id!;
        if (ex != null) throw ex;

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

    #region 辅助
    /// <summary>写日志</summary>
    /// <param name="action"></param>
    /// <param name="success"></param>
    /// <param name="message"></param>
    public virtual void WriteLog(String action, Boolean success, String message) => XTrace.WriteLine($"[{action}]{message}");
    #endregion
}