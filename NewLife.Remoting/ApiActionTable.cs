using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace NewLife.Remoting;

/// <summary>Api动作静态表。承载源生成器在编译期为控制器预生成的动作调用委托与流式泵，NativeAOT 友好</summary>
/// <remarks>
/// <para>由 NewLife.Remoting 源生成器在编译期扫描 <c>Register&lt;TService&gt;()</c> 等注册调用点后生成注册代码，
/// 生成物以 <c>[ModuleInitializer]</c> 注册，先于任何静态构造。</para>
/// <para>表按「控制器类型 + 方法名」索引，命中时零反射调用；未命中（自定义注册、动态类型、生成器未覆盖的签名等）
/// 自动回退原有表达式编译与反射路径，运行语义不变。</para>
/// <para>流式泵按「元素类型」索引，用于承接 <c>IAsyncEnumerable&lt;T&gt;</c> 中运行期才确定的值类型元素，
/// 避免 NativeAOT 下不可用的运行期泛型实例化。</para>
/// </remarks>
public static class ApiActionTable
{
    #region 属性
    private static readonly ConcurrentDictionary<(Type, String), Func<Object, Object?[], Object?>> _invokers = new();
    private static readonly ConcurrentDictionary<Type, ApiStreamPumpDelegate> _streamPumps = new();
    #endregion

    #region 静态
    static ApiActionTable() => RegisterCommonStreamPumps();

    /// <summary>注册常见值类型元素的流式泵。泛型实例化在此处静态扎根，NativeAOT 下可编译期生成代码</summary>
    private static void RegisterCommonStreamPumps()
    {
        RegisterStreamPump(typeof(Boolean), ApiStreamPump.PumpAsync<Boolean>);
        RegisterStreamPump(typeof(Byte), ApiStreamPump.PumpAsync<Byte>);
        RegisterStreamPump(typeof(SByte), ApiStreamPump.PumpAsync<SByte>);
        RegisterStreamPump(typeof(Char), ApiStreamPump.PumpAsync<Char>);
        RegisterStreamPump(typeof(Int16), ApiStreamPump.PumpAsync<Int16>);
        RegisterStreamPump(typeof(UInt16), ApiStreamPump.PumpAsync<UInt16>);
        RegisterStreamPump(typeof(Int32), ApiStreamPump.PumpAsync<Int32>);
        RegisterStreamPump(typeof(UInt32), ApiStreamPump.PumpAsync<UInt32>);
        RegisterStreamPump(typeof(Int64), ApiStreamPump.PumpAsync<Int64>);
        RegisterStreamPump(typeof(UInt64), ApiStreamPump.PumpAsync<UInt64>);
        RegisterStreamPump(typeof(Single), ApiStreamPump.PumpAsync<Single>);
        RegisterStreamPump(typeof(Double), ApiStreamPump.PumpAsync<Double>);
        RegisterStreamPump(typeof(Decimal), ApiStreamPump.PumpAsync<Decimal>);
        RegisterStreamPump(typeof(DateTime), ApiStreamPump.PumpAsync<DateTime>);
        RegisterStreamPump(typeof(DateTimeOffset), ApiStreamPump.PumpAsync<DateTimeOffset>);
        RegisterStreamPump(typeof(TimeSpan), ApiStreamPump.PumpAsync<TimeSpan>);
        RegisterStreamPump(typeof(Guid), ApiStreamPump.PumpAsync<Guid>);
    }
    #endregion

    #region 动作调用
    /// <summary>注册动作调用委托</summary>
    /// <param name="controllerType">控制器类型。AOT 与裁剪下，该类型的公开/非公开方法与公开构造按需保留，
    /// 使宽面入口 <c>Register(controller, method)</c> 的按名查找同样可用</param>
    /// <param name="methodName">方法名</param>
    /// <param name="invoker">强类型调用委托，参数为(控制器实例, 参数数组)，返回Object</param>
    public static void RegisterInvoker([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] Type controllerType, String methodName, Func<Object, Object?[], Object?> invoker)
    {
        if (controllerType == null) throw new ArgumentNullException(nameof(controllerType));
        if (methodName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(methodName));

        _invokers[(controllerType, methodName)] = invoker ?? throw new ArgumentNullException(nameof(invoker));
    }

    /// <summary>尝试获取动作调用委托</summary>
    /// <param name="controllerType">控制器类型</param>
    /// <param name="methodName">方法名</param>
    /// <param name="invoker">强类型调用委托；未注册时返回 null</param>
    /// <returns>是否命中</returns>
    public static Boolean TryGetInvoker(Type controllerType, String methodName, out Func<Object, Object?[], Object?>? invoker)
    {
        invoker = null;
        if (controllerType == null || methodName.IsNullOrEmpty()) return false;

        return _invokers.TryGetValue((controllerType, methodName), out invoker);
    }
    #endregion

    #region 流式泵
    /// <summary>注册元素类型的流式泵</summary>
    /// <param name="elementType">元素类型</param>
    /// <param name="pump">流式泵委托</param>
    public static void RegisterStreamPump(Type elementType, ApiStreamPumpDelegate pump)
    {
        if (elementType == null) throw new ArgumentNullException(nameof(elementType));

        _streamPumps[elementType] = pump ?? throw new ArgumentNullException(nameof(pump));
    }

    /// <summary>尝试获取元素类型的流式泵</summary>
    /// <param name="elementType">元素类型</param>
    /// <param name="pump">流式泵委托；未注册时返回 null</param>
    /// <returns>是否命中</returns>
    public static Boolean TryGetStreamPump(Type elementType, out ApiStreamPumpDelegate? pump)
    {
        pump = null;
        if (elementType == null) return false;

        return _streamPumps.TryGetValue(elementType, out pump);
    }
    #endregion

    #region 元数据保留
    /// <summary>声明动作入参与返回类型的成员保留。由源生成器为编译期已知的动作签名调用，使运行时类型 JSON 绑定可在裁剪与 NativeAOT 下工作</summary>
    /// <param name="type">动作签名中的类型</param>
    /// <remarks>保留要求与序列化宿主一致：公开无参构造、公开字段、公开属性</remarks>
    public static void Preserve([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
    {
    }
    #endregion
}
