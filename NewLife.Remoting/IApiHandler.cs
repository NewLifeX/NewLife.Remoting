using System.Collections;
using System.Reflection;
using NewLife.Collections;
using NewLife.Data;
using NewLife.Log;
using NewLife.Messaging;
using NewLife.Model;
using NewLife.Net;
using NewLife.Reflection;
using NewLife.Remoting.Http;
using NewLife.Serialization;

namespace NewLife.Remoting;

/// <summary>Api处理器</summary>
public interface IApiHandler
{
    /// <summary>执行</summary>
    /// <param name="session">会话</param>
    /// <param name="action">动作</param>
    /// <param name="args">参数</param>
    /// <param name="msg">消息</param>
    /// <param name="serviceProvider">当前作用域的服务提供者</param>
    /// <returns></returns>
    Object? Execute(IApiSession session, String action, Object? args, IMessage msg, IServiceProvider serviceProvider);
}

/// <summary>默认处理器</summary>
/// <remarks>
/// 在基于令牌Token的无状态验证模式中，可以借助Token重写IApiHandler.Prepare，来达到同一个Token共用相同的IApiSession.Items
/// </remarks>
public class ApiHandler : IApiHandler
{
    #region 属性
    /// <summary>Api接口主机</summary>
    public IApiHost Host { get; set; } = null!;
    #endregion

    #region 执行
    /// <summary>执行</summary>
    /// <param name="session">会话</param>
    /// <param name="action">动作</param>
    /// <param name="args">参数</param>
    /// <param name="msg">消息</param>
    /// <param name="serviceProvider">当前作用域的服务提供者</param>
    /// <returns></returns>
    public virtual Object? Execute(IApiSession session, String action, Object? args, IMessage msg, IServiceProvider serviceProvider)
    {
        if (action.IsNullOrEmpty()) action = "Api/Info";

        // IApiManager存在于Host中，如果没有则从服务提供者获取
        var provider = (Host as IServiceProvider);
        var manager = provider?.GetService<IApiManager>();
        manager ??= serviceProvider.GetService<IApiManager>();
        var api = manager?.Find(action) ?? throw new ApiException(ApiCode.NotFound, $"无法找到名为[{action}]的服务！");

        // 全局共用控制器，或者每次创建对象实例
        var controller = manager.CreateController(api, serviceProvider)
            ?? throw new ApiException(ApiCode.Forbidden, $"无法创建名为[{api.Name}]的服务！");
        if (controller is IApi capi) capi.Session = session;
        if (session is INetSession ss)
            api.LastSession = ss.Remote + "";
        else
            api.LastSession = session + "";

        var st = api.StatProcess;
        var sw = st.StartCount();

        // 准备调用上下文
        var ctx = Prepare(session, action, args, api, msg);
        ctx.Controller = controller;

        // 释放参数到跟踪片段
        if (ctx.Parameters != null && DefaultSpan.Current is ISpan span)
        {
            span.Detach(ctx.Parameters);
            foreach (var item in ctx.Parameters)
            {
                if (item.Value is ITraceMessage tm && !tm.TraceId.IsNullOrEmpty())
                {
                    span.Detach(tm.TraceId);
                    break;
                }
            }
        }

        Object? rs = null;
        try
        {
            // 执行动作前的过滤器
            if (controller is IActionFilter filter)
            {
                filter.OnActionExecuting(ctx);
                rs = ctx.Result;
            }

            // 执行动作
            if (rs == null)
            {
                // 特殊处理参数和返回类型都是IPacket的服务
                if (api.IsPacketParameter && api.IsPacketReturn)
                {
                    var func = api.Method.As<Func<IPacket?, IPacket?>>(controller)
                        ?? throw new ArgumentOutOfRangeException(nameof(api.Method));
                    rs = func(args as IPacket);
                }
                else if (api.IsPacketParameter)
                {
                    rs = controller.Invoke(api.Method, args);
                }
                else
                {
                    rs = controller.InvokeWithParams(api.Method, ctx.ActionParameters as IDictionary);
                }

                if (rs is Task task) rs = GetTaskResult(task);

                ctx.Result = rs;
            }

            // 执行动作后的过滤器
            if (controller is IActionFilter filter2)
            {
                filter2.OnActionExecuted(ctx);
                rs = ctx.Result;
            }

            // 特殊处理IAccessor返回值，直接进行二进制序列化
            if (rs is IAccessor accessor) rs = accessor.ToPacket();
        }
        catch (ThreadAbortException) { throw; }
        catch (Exception ex)
        {
            ctx.Exception = ex.GetTrue();

            // 执行动作后的过滤器
            if (controller is IActionFilter filter)
            {
                filter.OnActionExecuted(ctx);
                rs = ctx.Result;
            }
            if (ctx.Exception != null && !ctx.ExceptionHandled) throw;
        }
        finally
        {
            // 重置上下文，待下次重用对象
            ctx.Reset();

            st.StopCount(sw);
        }

        return rs;
    }

    private static Object? GetTaskResult(Task task)
    {
        task.GetAwaiter().GetResult();

        var taskType = task.GetType();
        if (!taskType.IsGenericType) return null;

        var resultProperty = taskType.GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
        return resultProperty?.GetValue(task);
    }

    /// <summary>准备上下文，可以借助Token重写Session会话集合</summary>
    /// <param name="session">Api会话</param>
    /// <param name="action">接口名</param>
    /// <param name="args">参数</param>
    /// <param name="api">Api接口</param>
    /// <param name="msg">消息内容，辅助数据解析</param>
    /// <returns></returns>
    public virtual ControllerContext Prepare(IApiSession session, String action, Object? args, ApiAction api, IMessage msg)
    {
        //var enc = Host.Encoder;
        var enc = session["Encoder"] as IEncoder ?? Host.Encoder;

        // 当前上下文
        var ctx = ControllerContext.Current;
        if (ctx == null)
        {
            ctx = new ControllerContext();
            ControllerContext.Current = ctx;
        }
        ctx.Action = api;
        ctx.ActionName = action;
        ctx.Session = session;
        ctx.Request = args;

        // 如果服务只有一个二进制参数，则走快速通道
        if (api.IsPacketParameter) return ctx;

        // IAccessor参数，直接进行二进制反序列化
        if (api.IsAccessorParameter)
        {
            if (args == null) return ctx;

            var pi = api.Method.GetParameters().FirstOrDefault();
            if (pi != null && !pi.Name.IsNullOrEmpty())
            {
                var value = pi.ParameterType.CreateInstance();
                if (value is IAccessor accessor && args is IPacket pk2)
                {
                    if (accessor.Read(pk2.GetStream(), args))
                    {
                        ctx.ActionParameters = new NullableDictionary<String, Object?>(StringComparer.OrdinalIgnoreCase)
                        {
                            [pi.Name] = value
                        };
                    }

                    return ctx;
                }
            }
        }

        // 不允许参数字典为空。接口只有一个入参时，客户端可能用基础类型封包传递
        IDictionary<String, Object?>? dic = null;
        Object? raw = null;
        if (args is IPacket pk && pk.Total > 0)
        {
            raw = enc.DecodeParameters(action, pk, msg);
            if (raw is IDictionary<String, Object?> dic2)
                dic = dic2;
        }

        dic ??= new NullableDictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);

        ctx.Parameters = dic;
        //session.Parameters = dic;

        // 令牌，作为参数或者http头传递
        if (dic.TryGetValue("Token", out var token)) session.Token = token + "";
        if (session.Token.IsNullOrEmpty() && msg is HttpMessage hmsg && hmsg.Headers != null)
        {
            // post、package、byte三种情况将token 写入请求头
            if (hmsg.Headers.TryGetValue("x-token", out var token2))
                session.Token = token2;
            else if (hmsg.Headers.TryGetValue("Authorization", out token2))
                session.Token = token2.TrimStart("Bearer ");
        }

        // 准备好参数
        var ps = GetParameterValues(api.Method, dic, raw, args, enc);
        ctx.ActionParameters = ps;

        return ctx;
    }

    /// <summary>获取接口方法对应的参数值集合</summary>
    /// <param name="method">接口方法</param>
    /// <param name="dic">请求参数</param>
    /// <param name="raw">原始的请求解码参数</param>
    /// <param name="args">原始参数</param>
    /// <param name="encoder">编解码器</param>
    /// <returns></returns>
    protected virtual IDictionary<String, Object?> GetParameterValues(MethodInfo method, IDictionary<String, Object?> dic, Object? raw, Object? args, IEncoder encoder)
    {
        var ps = new Dictionary<String, Object?>();

        // 该方法没有参数，无视外部传入参数
        var pis = method.GetParameters();
        if (pis == null || pis.Length <= 0) return ps;

        if (pis.Length == 1 && dic.Count == 0)
        {
            var pi = pis[0];
            if (!pi.Name.IsNullOrEmpty())
            {
                // 唯一参数，客户端直传数组而没有传字典
                if (pi.ParameterType.GetTypeCode() == TypeCode.Object)
                {
                    //ps[pi.Name] = raw.ChangeType(pi.ParameterType);
                    ps[pi.Name] = raw == null ? null : encoder.Convert(raw, pi.ParameterType);

                    return ps;
                }
                // 接口只有一个基础类型入参时，客户端可能用基础类型封包传递（字符串）。
                // 例如接口 Say(String text)，客户端可用 InvokeAsync<Object>("Say", "Hello NewLife!")
                else if (args != null)
                {
                    //ps[pi.Name] = args.ToStr().ChangeType(pi.ParameterType);
                    ps[pi.Name] = raw == null ? null : encoder.Convert(raw, pi.ParameterType);

                    return ps;
                }
            }
        }

        foreach (var pi in pis)
        {
            var name = pi.Name;
            if (name.IsNullOrEmpty()) continue;

            Object? v = null;
            if (dic != null && dic.TryGetValue(name, out var v2)) v = v2;

            // 基本类型
            if (pi.ParameterType.GetTypeCode() != TypeCode.Object)
            {
                //ps[name] = v.ChangeType(pi.ParameterType);
                ps[name] = v == null ? null : encoder.Convert(v, pi.ParameterType);
            }
            // 复杂对象填充，各个参数填充到一个模型参数里面去
            else
            {
                // 特殊处理字节数组
                if (pi.ParameterType == typeof(Byte[]))
                    ps[name] = Convert.FromBase64String(v + "");
                else
                {
                    v ??= dic;
                    ps[name] = v == null ? null : encoder.Convert(v, pi.ParameterType);
                }
            }
        }

        return ps;
    }
    #endregion
}

/// <summary>带令牌会话的处理器</summary>
/// <remarks>
/// 在基于令牌Token的无状态验证模式中，可以借助Token重写IApiHandler.Prepare，来达到同一个Token共用相同的IApiSession.Items。
/// 支持内存缓存和Redis缓存。
/// </remarks>
public class TokenApiHandler : ApiHandler
{
    ///// <summary>会话存储</summary>
    //public ICache Cache { get; set; } = new MemoryCache { Expire = 20 * 60 };

    /// <summary>准备上下文，可以借助Token重写Session会话集合</summary>
    /// <param name="session">Api会话</param>
    /// <param name="action">接口名</param>
    /// <param name="args">参数</param>
    /// <param name="api">Api接口</param>
    /// <param name="msg">消息内容，辅助数据解析</param>
    /// <returns></returns>
    public override ControllerContext Prepare(IApiSession session, String action, Object? args, ApiAction api, IMessage msg)
    {
        var ctx = base.Prepare(session, action, args, api, msg);

        var token = session.Token;
        if (!token.IsNullOrEmpty())
        {
            // 第一用户数据是本地字典，用于记录是否启用了第二数据
            if (session is IExtend ns && ns["Token"] + "" != token)
            {
                //var key = GetKey(token);
                //// 采用哈希结构。内存缓存用并行字典，Redis用Set
                //ns.Items2 = Cache.GetDictionary<Object>(key);
                ns["Token"] = token;
            }
        }

        return ctx;
    }

    /// <summary>根据令牌活期缓存Key</summary>
    /// <param name="token"></param>
    /// <returns></returns>
    protected virtual String GetKey(String token) => (!token.IsNullOrEmpty() && token.Length > 16) ? token.MD5() : token;
}