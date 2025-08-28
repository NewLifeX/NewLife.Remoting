using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife.Collections;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
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
public abstract class BaseController : ControllerBase, IWebFilter, ILogProvider
{
    #region 属性
    /// <summary>令牌</summary>
    public String? Token => Context?.Token;

    /// <summary>令牌对象</summary>
    public JwtBuilder Jwt { get; set; } = null!;

    /// <summary>用户主机</summary>
    public String UserHost => Context?.UserHost ?? HttpContext.GetUserHost();

    /// <summary>设备上下文</summary>
    public DeviceContext Context { get; set; } = null!;

    private readonly IDeviceService? _deviceService;
    private readonly ITokenService _tokenService;
    private IDictionary<String, Object?>? _args;
    private static readonly Action<String>? _setip;
    private static readonly Pool<DeviceContext> _pool = new(64);
    #endregion

    #region 构造
    static BaseController()
    {
        // 反射获取ManageProvider.UserHost的Set方法，避免直接引用XCode
        _setip = "ManageProvider".GetTypeEx()?.GetPropertyEx("UserHost")?.SetMethod?.CreateDelegate<Action<String>>();
    }

    /// <summary>实例化</summary>
    /// <param name="serviceProvider"></param>
    public BaseController(IServiceProvider serviceProvider) => _tokenService = serviceProvider.GetRequiredService<ITokenService>();

    /// <summary>实例化</summary>
    /// <param name="deviceServicde"></param>
    /// <param name="tokenService"></param>
    /// <param name="serviceProvider"></param>
    public BaseController(IDeviceService? deviceServicde, ITokenService? tokenService, IServiceProvider serviceProvider)
    {
        _deviceService = deviceServicde;
        _tokenService = tokenService ?? serviceProvider.GetRequiredService<ITokenService>();
    }
    #endregion

    #region 令牌验证
    void IWebFilter.OnActionExecuting(ActionExecutingContext context)
    {
        _args = context.ActionArguments;

        // 向ManageProvider.UserHost写入用户主机IP地址
        var ip = HttpContext.GetUserHost();
        //ManageProvider.UserHost = UserHost;
        if (!ip.IsNullOrEmpty()) _setip?.Invoke(ip);

        // 从池中获取上下文
        var ctx = Context = _pool.Get();
        ctx.UserHost = ip;
        ctx["__ActionContext"] = context;

        var token = ctx.Token = ApiFilterAttribute.GetToken(context.HttpContext);

        try
        {
            if (context.ActionDescriptor is ControllerActionDescriptor act && !act.MethodInfo.IsDefined(typeof(AllowAnonymousAttribute)))
            {
                // 匿名访问接口无需验证。例如星尘Node的SendCommand接口，并不使用Node令牌，而是使用App令牌
                var rs = OnAuthorize(token, ctx);
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
    protected virtual Boolean OnAuthorize(String token, DeviceContext context)
    {
        if (token.IsNullOrEmpty()) return false;

        var (jwt, ex) = _tokenService.DecodeToken(token);
        Jwt = jwt;
        context.Code = jwt?.Subject;
        context.ClientId = jwt?.Id!;

        // 如果注入了设备服务，尝试获取设备。即使失败，也要继续往下走，最后再决定是否抛出异常
        if (_deviceService != null)
        {
            if (context.Code.IsNullOrEmpty()) return false;

            var dv = _deviceService.QueryDevice(context.Code);
            if (dv == null || !dv.Enable) ex ??= new ApiException(ApiCode.Forbidden, "无效客户端！");

            context.Device = dv!;
        }

        if (ex != null) throw ex;

        return jwt != null;
    }

    void IWebFilter.OnActionExecuted(ActionExecutedContext context)
    {
        if (context.Exception != null) WriteError(context.Exception, context);

        // 归还上下文到池
        var ctx = Context;
        if (ctx != null)
        {
            ctx.Clear();
            _pool.Return(ctx);
            Context = null!;
        }
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
    public virtual void WriteLog(String action, Boolean success, String message)
    {
        if (_deviceService != null)
            _deviceService.WriteHistory(Context, action, success, message);
        else
            XTrace.WriteLine($"[{action}]{message}");
    }
    #endregion
}