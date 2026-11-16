using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife.Log;

namespace NewLife.Remoting.Extensions;

/// <summary>统一Api过滤处理</summary>
/// <remarks>
/// 1，解析访问令牌
/// 2，包装响应结果为标准Json格式
/// 3，拦截异常，包装为标准Json格式
/// </remarks>
public sealed class ApiFilterAttribute : ActionFilterAttribute
{
    /// <summary>是否允许从查询字符串读取令牌。默认 false，避免令牌进入 URL 日志与 Referer</summary>
    public static Boolean AllowTokenInQuery { get; set; }

    /// <summary>是否允许从 Cookie 读取令牌。默认 false，避免 GET 接口出现 CSRF 面</summary>
    public static Boolean AllowTokenInCookie { get; set; }

    /// <summary>是否允许从 X-Token 请求头读取令牌。默认 false，仅保留标准 Authorization 头</summary>
    public static Boolean AllowTokenInHeader { get; set; }

    /// <summary>从请求头中获取令牌</summary>
    /// <remarks>
    /// 默认只认标准的 <c>Authorization: Bearer</c> 头；查询字符串、Cookie 与 X-Token 头必须显式开启，
    /// 否则令牌会进入 URL 日志/Referer，或让 GET 接口暴露 CSRF 面。
    /// 浏览器 WebSocket 无法自定义请求头，确有需要时再开启查询字符串方式。
    /// </remarks>
    /// <param name="httpContext"></param>
    /// <returns>令牌字符串，未携带时返回空字符串</returns>
    public static String GetToken(HttpContext httpContext)
    {
        var request = httpContext.Request;
        var token = (request.Headers.Authorization + "").TrimPrefix("Bearer ");
        if (token.IsNullOrEmpty() && AllowTokenInHeader) token = request.Headers["X-Token"] + "";
        if (token.IsNullOrEmpty() && AllowTokenInQuery) token = request.Query["Token"] + "";
        if (token.IsNullOrEmpty() && AllowTokenInCookie) token = request.Cookies["Token"] + "";

        return token;
    }

    /// <summary>执行前，验证模型</summary>
    /// <param name="context"></param>
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        //if (!context.ModelState.IsValid)
        //    throw new ApplicationException(context.ModelState.Values.First(p => p.Errors.Count > 0).Errors[0].ErrorMessage);

        // 请求语言：宿主已启用 RequestLocalization 时沿用其选择，否则按 Accept-Language 切换当前 UI 文化
        // 过滤器可能因继承链重复注册，先释放旧作用域，避免文化无法还原
        if (context.HttpContext.Items.TryGetValue(typeof(RequestCultureScope), out var old) && old is IDisposable oldScope)
        {
            oldScope.Dispose();
            context.HttpContext.Items.Remove(typeof(RequestCultureScope));
        }
        if (context.HttpContext.Features.Get<IRequestCultureFeature>() == null)
            context.HttpContext.Items[typeof(RequestCultureScope)] = RequestCultureScope.Create(context.HttpContext.Request.Headers.AcceptLanguage.ToString());

        // 访问令牌
        var token = GetToken(context.HttpContext);
        context.HttpContext.Items["Token"] = token;
        if (!context.ActionArguments.ContainsKey("token")) context.ActionArguments.Add("token", token);

        base.OnActionExecuting(context);
    }

    /// <summary>执行后，包装结果和异常</summary>
    /// <param name="context"></param>
    public override void OnActionExecuted(ActionExecutedContext context)
    {
        try
        {
            if (context.HttpContext.WebSockets.IsWebSocketRequest) return;

            if (context.Result != null)
            {
                if (context.Result is ObjectResult obj)
                {
                    context.Result = new JsonResult(new { code = obj.StatusCode ?? 0, data = obj.Value });
                }
                else if (context.Result is EmptyResult)
                {
                    DefaultTracer.Instance?.NewSpan("apiFilter-EmptyResult");
                    context.Result = new JsonResult(new { code = 0, data = new { } });
                }
            }
            else if (context.Exception != null && !context.ExceptionHandled)
            {
                var ex = context.Exception.GetTrue();
                if (ex is ApiException aex)
                    context.Result = new JsonResult(new { code = aex.Code, message = aex.Message });
                else
                {
                    // 非业务异常不把原文回客户端，避免数据库语句、文件路径、内部类型外泄
                    context.Result = new JsonResult(new { code = 500, message = "Internal Server Error", traceId = DefaultSpan.Current?.TraceId });

                    // 埋点拦截业务异常
                    var action = context.HttpContext.Request.Path + "";
                    if (context.ActionDescriptor is ControllerActionDescriptor act) action = $"/{act.ControllerName}/{act.ActionName}";

                    DefaultTracer.Instance?.NewError(action, ex);
                }

                context.ExceptionHandled = true;
            }

            base.OnActionExecuted(context);
        }
        finally
        {
            if (context.HttpContext.Items.TryGetValue(typeof(RequestCultureScope), out var value) && value is IDisposable scope)
            {
                scope.Dispose();
                context.HttpContext.Items.Remove(typeof(RequestCultureScope));
            }
        }
    }
}