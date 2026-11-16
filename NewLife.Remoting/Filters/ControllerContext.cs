namespace NewLife.Remoting;

/// <summary>控制器上下文</summary>
public class ControllerContext
{
    /// <summary>控制器实例</summary>
    public Object? Controller { get; set; }

    /// <summary>处理动作</summary>
    public ApiAction? Action { get; set; }

    /// <summary>真实动作名称</summary>
    public String? ActionName { get; set; }

    /// <summary>会话</summary>
    public IApiSession? Session { get; set; }

    /// <summary>请求</summary>
    public Object? Request { get; set; }

    /// <summary>请求参数</summary>
    public IDictionary<String, Object?>? Parameters { get; set; }

    /// <summary>获取或设置操作方法参数。</summary>
    public virtual IDictionary<String, Object?>? ActionParameters { get; set; }

    /// <summary>获取或设置由操作方法返回的结果。</summary>
    public Object? Result { get; set; }

    /// <summary>获取或设置在操作方法的执行过程中发生的异常（如果有）。</summary>
    public virtual Exception? Exception { get; set; }

    /// <summary>获取或设置一个值，该值指示是否处理异常。</summary>
    public Boolean ExceptionHandled { get; set; }

    /// <summary>扩展数据字典</summary>
    public IDictionary<String, Object?> Items { get; } = new Dictionary<String, Object?>();

    /// <summary>实例化</summary>
    public ControllerContext() { }

    ///// <summary>拷贝实例化</summary>
    ///// <param name="context"></param>
    //public ControllerContext(ControllerContext context)
    //{
    //    Controller = context.Controller;
    //    Action = context.Action;
    //    ActionName = context.ActionName;
    //    Session = context.Session;
    //    Request = context.Request;
    //    Parameters = context.Parameters;
    //}

#if NET45
    // .NET Framework 4.5 没有 AsyncLocal，只能退回线程静态；异步动作续体切线程时可能读不到上下文
    [ThreadStatic]
    private static ControllerContext? _Current;
    /// <summary>当前线程上下文</summary>
    public static ControllerContext? Current { get => _Current; set => _Current = value; }
#else
    // 动作可能是异步的，续体会切线程；线程静态会导致 A 请求的上下文被 B 请求读到或被覆盖，
    // 因此按异步流程存放，await 前后与并行请求互不干扰
    private static readonly AsyncLocal<ControllerContext?> _Current = new();
    /// <summary>当前异步流程上下文</summary>
    public static ControllerContext? Current { get => _Current.Value; set => _Current.Value = value; }
#endif

    /// <summary>重置为默认状态</summary>
    public void Reset()
    {
        Controller = null;
        Action = null;
        ActionName = null;
        Session = null;
        Request = null;
        Parameters = null;
        ActionParameters = null;
        Result = null;
        Exception = null;
        ExceptionHandled = false;

        // 上下文对象会被重用，上一次请求留下的扩展数据不能带给下一次请求
        Items.Clear();
    }
}