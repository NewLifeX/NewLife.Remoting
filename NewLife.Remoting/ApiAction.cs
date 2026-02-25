using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using NewLife.Data;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Serialization;

namespace NewLife.Remoting;

/// <summary>Api动作</summary>
public class ApiAction : IExtend
{
    /// <summary>动作名称</summary>
    public String Name { get; set; } = null!;

    /// <summary>动作所在类型</summary>
    public Type Type { get; set; } = null!;

    /// <summary>方法</summary>
    public MethodInfo Method { get; set; } = null!;

    /// <summary>控制器对象</summary>
    /// <remarks>如果指定控制器对象，则每次调用前不再实例化对象</remarks>
    public Object? Controller { get; set; }

    /// <summary>是否二进制参数</summary>
    public Boolean IsPacketParameter { get; }

    /// <summary>是否二进制返回</summary>
    public Boolean IsPacketReturn { get; }

    /// <summary>是否Accessor参数</summary>
    public Boolean IsAccessorParameter { get; }

    /// <summary>是否Accessor返回</summary>
    public Boolean IsAccessorReturn { get; }

    /// <summary>是否无参数方法</summary>
    public Boolean IsNoParameter { get; }

    /// <summary>预编译的快速调用委托</summary>
    public Func<Object, Object?[], Object?>? FastInvoker { get; private set; }

    /// <summary>处理统计</summary>
    public ICounter StatProcess { get; set; } = new PerfCounter();

    /// <summary>最后会话</summary>
    public String? LastSession { get; set; }

    /// <summary>扩展数据</summary>
    public IDictionary<String, Object?> Items { get; } = new Dictionary<String, Object?>();

    /// <summary>索引器</summary>
    public Object? this[String key] { get => Items.TryGetValue(key, out var v) ? v : null; set => Items[key] = value; }

    /// <summary>实例化</summary>
    public ApiAction() { }

    /// <summary>实例化</summary>
    public ApiAction(MethodInfo method, Type type)
    {
        if (type == null) type = method.DeclaringType!;
        Name = GetName(type, method);

        // 必须同时记录类型和方法，因为有些方法位于继承的不同层次，那样会导致实例化的对象不一致
        Type = type;
        Method = method;

        var ps = method.GetParameters();
        if (ps != null && ps.Length == 1)
        {
            if (ps[0].ParameterType.As<IPacket>()) IsPacketParameter = true;
            if (ps[0].ParameterType.As<IAccessor>()) IsAccessorParameter = true;
        }

        IsNoParameter = ps == null || ps.Length == 0;

        var returnType = method.ReturnType;
        if (returnType.As(typeof(Task<>)))
            returnType = returnType.GetGenericArguments()[0];

        if (returnType.As<IPacket>()) IsPacketReturn = true;
        if (returnType.As<IAccessor>()) IsAccessorReturn = true;

        // 预编译快速调用委托
        FastInvoker = CompileInvoker(method);
    }

    /// <summary>使用表达式树编译快速调用委托，避免每次调用走反射</summary>
    /// <param name="method">方法信息</param>
    /// <returns>编译后的委托，参数为(instance, args[])，返回Object</returns>
    private static Func<Object, Object?[], Object?>? CompileInvoker(MethodInfo method)
    {
        try
        {
            var instanceParam = Expression.Parameter(typeof(Object), "instance");
            var argsParam = Expression.Parameter(typeof(Object?[]), "args");

            // 转换实例对象类型
            var instance = method.IsStatic ? null : Expression.Convert(instanceParam, method.DeclaringType!);

            // 构造参数列表
            var parameters = method.GetParameters();
            var argExpressions = new Expression[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                var index = Expression.ArrayIndex(argsParam, Expression.Constant(i));
                argExpressions[i] = Expression.Convert(index, parameters[i].ParameterType);
            }

            // 调用方法
            var call = method.IsStatic
                ? Expression.Call(method, argExpressions)
                : Expression.Call(instance!, method, argExpressions);

            // 处理返回值
            Expression body;
            if (method.ReturnType == typeof(void))
            {
                body = Expression.Block(call, Expression.Constant(null, typeof(Object)));
            }
            else
            {
                body = Expression.Convert(call, typeof(Object));
            }

            var lambda = Expression.Lambda<Func<Object, Object?[], Object?>>(body, instanceParam, argsParam);
            return lambda.Compile();
        }
        catch
        {
            // 编译失败时回退到反射调用
            return null;
        }
    }

    /// <summary>获取名称</summary>
    /// <param name="type"></param>
    /// <param name="method"></param>
    /// <returns></returns>
    public static String GetName(Type? type, MethodInfo method)
    {
        if (type == null) type = method.DeclaringType!;
        //if (type == null) return null;

        var typeName = type.Name.TrimEnd("Controller", "Service");
        var att = type.GetCustomAttribute<ApiAttribute>(true);
        if (att != null) typeName = att.Name;

        var miName = method.Name;
        att = method.GetCustomAttribute<ApiAttribute>();
        if (att != null) miName = att.Name;

        if (typeName.IsNullOrEmpty() || miName.Contains('/'))
            return miName;
        else
            return $"{typeName}/{miName}";
    }

    /// <summary>已重载。</summary>
    /// <returns></returns>
    public override String ToString()
    {
        var mi = Method;

        var returnType = mi.ReturnType;
        var rtype = returnType.Name;
        if (returnType.As<Task>())
        {
            if (!returnType.IsGenericType)
                rtype = "void";
            else
            {
                returnType = returnType.GetGenericArguments()[0];
                rtype = returnType.Name;
            }
        }

        var ps = mi.GetParameters().Select(pi => $"{pi.ParameterType.Name} {pi.Name}").Join(", ");
        return $"{rtype} {mi.Name}({ps})";
    }
}