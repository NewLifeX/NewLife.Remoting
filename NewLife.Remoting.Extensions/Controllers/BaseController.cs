using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife.Collections;
using NewLife.Log;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Serialization;
using NewLife.Web;
using XCode.Membership;
using IWebFilter = Microsoft.AspNetCore.Mvc.Filters.IActionFilter;

namespace NewLife.Remoting.Extensions;

/// <summary>业务接口控制器基类</summary>
/// <remarks>
/// 提供统一的令牌解码/鉴权与异常收敛，前置解析令牌，后置记录异常并回收池化的 DeviceContext。
/// 将 JWT 写入 Jwt/DeviceContext（Code、ClientId、设备/在线），并在 HttpContext.Items 暴露供中间件/日志使用。
/// 鉴权成功后映射 ClaimsPrincipal（ApiToken），可配合 [Authorize] 与策略（例如 DeviceRequired）。
/// 支持方法/控制器/Endpoint 的 AllowAnonymous 跳过鉴权；错误统一返回 code/message/traceId，并屏蔽 SQL细节。
/// 可覆写 OnAuthorize/OnWriteError/WriteLog；控制器为每请求实例，结合对象池保证性能与线程安全。
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
    //private static readonly Action<String>? _setip;
    private static readonly Pool<DeviceContext> _pool = new(256);
    #endregion

    #region 构造
    //static BaseController()
    //{
    //    // 反射获取ManageProvider.UserHost的Set方法，避免直接引用XCode
    //    _setip = "ManageProvider".GetTypeEx()?.GetPropertyEx("UserHost")?.SetMethod?.CreateDelegate<Action<String>>();
    //}

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
        ManageProvider.UserHost = ip;
        //if (!ip.IsNullOrEmpty()) _setip?.Invoke(ip);

        // 从池中获取上下文
        var ctx = Context = _pool.Get();
        ctx.UserHost = ip;
        ctx["__ActionContext"] = context;

        // 暴露到 HttpContext.Items，方便中间件/日志访问
        HttpContext.Items[nameof(DeviceContext)] = ctx;

        var token = ctx.Token = ApiFilterAttribute.GetToken(context.HttpContext);
        var span = DefaultSpan.Current;

        try
        {
            if (context.ActionDescriptor is ControllerActionDescriptor act)
            {
                var endpoint = context.HttpContext.GetEndpoint();
                var allowAnon = act.MethodInfo.IsDefined(typeof(AllowAnonymousAttribute), true)
                    || act.ControllerTypeInfo.IsDefined(typeof(AllowAnonymousAttribute), true)
                    || endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null;

                if (!allowAnon)
                {
                    // 匿名访问接口无需验证。例如星尘Node的SendCommand接口，并不使用Node令牌，而是使用App令牌
                    var rs = OnAuthorize(token, ctx);
                    if (!rs) throw new ApiException(ApiCode.Unauthorized, "认证失败");
                }
            }
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            span?.SetError(ex, null);

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
    [Obsolete("=>OnAuthorize(String token, DeviceContext context)", true)]
    protected virtual Boolean OnAuthorize(String token) => OnAuthorize(token, Context);

    /// <summary>验证令牌，并获取Jwt对象，子类可借助Jwt.Subject获取设备</summary>
    /// <param name="token">访问令牌</param>
    /// <param name="context"></param>
    /// <returns></returns>
    protected virtual Boolean OnAuthorize(String token, DeviceContext context)
    {
        if (token.IsNullOrEmpty()) return false;

        var (jwt, ex) = _tokenService.DecodeToken(token);
        Jwt = jwt;
        var code = context.Code = jwt?.Subject;
        context.ClientId = jwt?.Id!;
        context["__Jwt"] = jwt;

        var span = DefaultSpan.Current;
        span?.AppendTag($"code={context.Code} clientId={context.ClientId}");

        // 如果注入了设备服务，尝试获取设备。即使失败，也要继续往下走，最后再决定是否抛出异常
        if (_deviceService != null)
        {
            if (code.IsNullOrEmpty()) return false;

            var ds2 = _deviceService as IDeviceService2;

            var dv = ds2 != null ? ds2.GetDevice(code) : _deviceService.QueryDevice(code);
            if (dv == null || !dv.Enable) ex ??= new ApiException(ApiCode.Forbidden, "无效客户端！");

            context.Device = dv!;

            if (ds2 != null && context.Online == null) context.Online = ds2.GetOnline(context);
        }

        if (ex != null) throw ex;

        // 将成功解析的身份映射为 ClaimsPrincipal，便于 [Authorize]/策略授权
        if (jwt != null)
        {
            var claims = new List<Claim>(8);
            if (!code.IsNullOrEmpty())
            {
                claims.Add(new Claim("code", code));
                claims.Add(new Claim(ClaimTypes.NameIdentifier, code));
            }
            if (!context.ClientId.IsNullOrEmpty()) claims.Add(new Claim("client_id", context.ClientId));
            //if (!context.UserHost.IsNullOrEmpty()) claims.Add(new Claim("ip", context.UserHost));
            //if (context.Device != null)
            //{
            //    var name = context.Device.Name;
            //    if (!name.IsNullOrEmpty()) claims.Add(new Claim(ClaimTypes.Name, name));
            //}

            var identity = new ClaimsIdentity(claims, "ApiToken");
            var principal = new ClaimsPrincipal(identity);
            HttpContext.User = principal;
        }

        return jwt != null;
    }

    void IWebFilter.OnActionExecuted(ActionExecutedContext context)
    {
        if (context.Exception != null) WriteError(context.Exception, context);

        // 归还上下文到池
        var ctx = Context;
        if (ctx != null)
        {
            // 从 HttpContext.Items 移除暴露的 DeviceContext
            HttpContext.Items.Remove(nameof(DeviceContext));

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