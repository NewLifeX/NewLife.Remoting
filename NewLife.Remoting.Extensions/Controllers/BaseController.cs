using System.Collections;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife.Data;
using NewLife.Log;
using NewLife.Remoting.Extensions.Services;
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
/// 
/// 明确列出所需服务接口，便于子类感知依赖。
/// 未显式传入的可选服务（如 <paramref name="deviceService"/>）会尝试从 <paramref name="serviceProvider"/> 自动解析。
/// </remarks>
/// <param name="deviceService">设备服务。非必需，为 null 时尝试从 serviceProvider 获取</param>
/// <param name="tokenService">令牌服务</param>
/// <param name="serviceProvider">服务提供者</param>
[ApiFilter]
[Route("[controller]")]
public abstract class BaseController(IDeviceService? deviceService, ITokenService? tokenService, IServiceProvider serviceProvider) : ControllerBase, IWebFilter, ILogProvider
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

    private readonly IDeviceService? _deviceService = deviceService ?? serviceProvider.GetService<IDeviceService>();
    private readonly ITokenService _tokenService = tokenService ?? serviceProvider.GetRequiredService<ITokenService>();

    private IDictionary<String, Object?>? _args;
    #endregion

    #region 令牌验证
    /// <summary>Action执行前处理。验证令牌和鉴权</summary>
    /// <param name="context">Action执行上下文</param>
    void IWebFilter.OnActionExecuting(ActionExecutingContext context)
    {
        _args = context.ActionArguments;

        // 向ManageProvider.UserHost写入用户主机IP地址
        var ip = HttpContext.GetUserHost();
        ManageProvider.UserHost = ip;

        // 从池中获取上下文
        var ctx = Context = DeviceContextPool.Rent();
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
                    var rs = OnAuthorize(token!, ctx);
                    if (!rs) throw new ApiException(ApiCode.Unauthorized, L.T("Authentication failed"));
                }
            }
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);

            var traceId = DefaultSpan.Current?.TraceId;
            // 非业务异常不把原文回客户端（数据库语句、文件路径、内部类型），排障依赖 traceId 与日志
            context.Result = ex is ApiException aex
                ? new JsonResult(new { code = aex.Code, message = aex.Message, traceId })
                : new JsonResult(new { code = 500, message = "Internal Server Error", traceId });

            WriteError(ex, context);
        }
    }

    /// <summary>验证令牌，并获取Jwt对象，子类可借助Jwt.Subject获取设备</summary>
    /// <param name="token">访问令牌</param>
    /// <param name="context">设备上下文</param>
    /// <returns>验证是否通过</returns>
    protected virtual Boolean OnAuthorize(String token, DeviceContext context)
    {
        // 与 Endpoints/RemotingEndpoints 的鉴权段互为镜像，修改须同步
        if (token.IsNullOrEmpty()) return false;

        var (jwt, ex) = _tokenService.DecodeToken(token);
        Jwt = jwt;

        // 刷新令牌只能用于刷新，禁止作为访问令牌调用业务接口（V-14 令牌分离）
        if (ex == null && jwt != null && jwt["kind"] + "" == "refresh")
            ex = new ApiException(ApiCode.Unauthorized, L.T("A refresh token cannot be used as an access token!"));

        var code = context.Code = jwt?.Subject;
        context.ClientId = jwt?.Id!;
        context["__Jwt"] = jwt;

        var span = DefaultSpan.Current;
        span?.AppendTag($"code={context.Code} clientId={context.ClientId}");

        // 如果注入了设备服务，尝试获取设备/在线信息。
        // 此处收集所有异常（设备不存在/已禁用/解码失败等），统一在下方抛出，
        // 避免多次 try-catch 分散异常处理逻辑。
        if (_deviceService != null)
        {
            if (code.IsNullOrEmpty()) return false;

            var ds2 = _deviceService as IDeviceService2;

            var dv = ds2 != null ? ds2.GetDevice(code) : _deviceService.QueryDevice(code);
            if (dv == null || !dv.Enable) ex ??= new ApiException(ApiCode.Forbidden, L.T("Invalid client!"));

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

    /// <summary>Action执行后处理。记录异常并回收上下文</summary>
    /// <param name="context">Action执行上下文</param>
    void IWebFilter.OnActionExecuted(ActionExecutedContext context)
    {
        if (context.Exception != null) WriteError(context.Exception, context);

        // 归还上下文到池
        var ctx = Context;
        if (ctx != null)
        {
            // 从 HttpContext.Items 移除暴露的 DeviceContext
            HttpContext.Items.Remove(nameof(DeviceContext));

            DeviceContextPool.Return(ctx);
            Context = null!;
        }
    }

    /// <summary>记录错误信息</summary>
    /// <param name="ex">异常对象</param>
    /// <param name="context">Action上下文</param>
    private void WriteError(Exception ex, ActionContext context)
    {
        // 拦截全局异常，写日志
        var action = context.HttpContext.Request.Path + "";
        if (context.ActionDescriptor is ControllerActionDescriptor act) action = $"{act.ControllerName}/{act.ActionName}";

        OnWriteError(action, ex?.GetTrue() + Environment.NewLine + BuildErrorLogArgs());
    }

    /// <summary>序列化动作参数用于错误日志，对凭据类字段脱敏</summary>
    /// <remarks>登录/鉴权失败时参数里往往就是明文口令与密钥，直接落库落日志等于把凭据外流</remarks>
    /// <returns>脱敏后的参数 JSON，无参数时返回 null</returns>
    protected virtual String? BuildErrorLogArgs()
    {
        if (_args == null || _args.Count == 0) return null;

        var dic = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in _args)
            dic[item.Key] = IsSensitiveArgument(item.Key) ? "***" : SanitizeArgument(item.Value, 0);

        return dic.ToJson(true);
    }

    /// <summary>递归脱敏参数值中的凭据字段</summary>
    /// <remarks>动作参数常常是请求模型对象（如登录请求），密钥在对象属性而不是动作参数名上，只掩盖顶层键名挡不住</remarks>
    /// <param name="value">参数值</param>
    /// <param name="depth">当前递归深度，限制层数避免环路与超大对象</param>
    /// <returns>脱敏后的值</returns>
    private static Object? SanitizeArgument(Object? value, Int32 depth)
    {
        if (value == null || value is String or ValueType or Byte[]) return value;
        if (value is IPacket pk) return $"<packet:{pk.Total}>";
        if (depth >= 3) return value;

        if (value is IDictionary<String, Object?> dic)
        {
            var rs = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in dic)
                rs[item.Key] = IsSensitiveArgument(item.Key) ? "***" : SanitizeArgument(item.Value, depth + 1);
            return rs;
        }
        if (value is System.Collections.IDictionary dic2)
        {
            var rs = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry item in dic2)
            {
                var key = item.Key + "";
                rs[key] = IsSensitiveArgument(key) ? "***" : SanitizeArgument(item.Value, depth + 1);
            }
            return rs;
        }
        if (value is IEnumerable list)
        {
            var rs = new List<Object?>();
            foreach (var item in list) rs.Add(SanitizeArgument(item, depth + 1));
            return rs;
        }

        // POCO 先转名值字典再递归，属性名同样检查；转换失败时原样输出，日志不能因脱敏而失败
        try
        {
            return SanitizeArgument(value.ToDictionary(), depth + 1);
        }
        catch
        {
            return value;
        }
    }

    /// <summary>判断参数名是否为凭据类字段（密钥、口令、令牌）</summary>
    /// <param name="name">参数名</param>
    /// <returns>需要脱敏时返回 true</returns>
    public static Boolean IsSensitiveArgument(String? name)
    {
        if (name.IsNullOrEmpty()) return false;

        // 归一化分隔符，让 api_key/api-key 一类命名也能命中
        var key = name.Replace("_", "").Replace("-", "");

        return key.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || key.Contains("password", StringComparison.OrdinalIgnoreCase)
            || key.Contains("passwd", StringComparison.OrdinalIgnoreCase)
            || key.Contains("pwd", StringComparison.OrdinalIgnoreCase)
            || key.Contains("token", StringComparison.OrdinalIgnoreCase)
            || key.Contains("authorization", StringComparison.OrdinalIgnoreCase)
            || key.Contains("apikey", StringComparison.OrdinalIgnoreCase)
            || key.Contains("accesskey", StringComparison.OrdinalIgnoreCase)
            || key.Contains("privatekey", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>输出错误日志</summary>
    /// <param name="action">动作名称</param>
    /// <param name="message">错误消息</param>
    protected virtual void OnWriteError(String action, String message) => WriteLog(action, false, message);
    #endregion

    #region 辅助
    /// <summary>写日志</summary>
    /// <param name="action">动作名称</param>
    /// <param name="success">是否成功</param>
    /// <param name="message">消息内容</param>
    public virtual void WriteLog(String action, Boolean success, String message)
    {
        if (_deviceService != null)
            _deviceService.WriteHistory(Context, action, success, message);
        else
            XTrace.WriteLine($"[{action}]{message}");
    }
    #endregion
}