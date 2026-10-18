using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using NewLife.Model;
using NewLife.Reflection;

namespace NewLife.Remoting;

/// <summary>接口管理器</summary>
public interface IApiManager
{
    /// <summary>可提供服务的方法</summary>
    IDictionary<String, ApiAction> Services { get; }

    /// <summary>注册服务提供类。该类的所有公开方法将直接暴露</summary>
    /// <typeparam name="TService">服务控制器类型。AOT 与裁剪下，该类型的公开/非公开方法与公开构造将按需保留</typeparam>
    void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] TService>();

    /// <summary>注册服务</summary>
    /// <param name="controller">控制器对象</param>
    /// <param name="method">动作名称。为空时遍历控制器所有公有成员方法</param>
    /// <remarks>运行时类型入口，裁剪与 AOT 下无法静态保证动作方法被保留，请优先使用泛型入口 <see cref="Register{TService}()"/></remarks>
    [RequiresUnreferencedCode("控制器类型在运行时确定，裁剪与 AOT 下无法静态保留其动作方法，请改用泛型入口 Register<TService>()")]
    void Register(Object controller, String? method);

    /// <summary>查找服务</summary>
    /// <param name="action"></param>
    /// <returns></returns>
    ApiAction? Find(String action);

    /// <summary>创建控制器实例</summary>
    /// <param name="api">Api动作</param>
    /// <param name="serviceProvider">服务提供者</param>
    /// <returns></returns>
    Object CreateController(ApiAction api, IServiceProvider serviceProvider);
}

/// <summary>接口管理器</summary>
public class ApiManager(IServiceProvider serviceProvider) : IApiManager
{
    /// <summary>可提供服务的方法</summary>
    public IDictionary<String, ApiAction> Services { get; } = new Dictionary<String, ApiAction>(StringComparer.OrdinalIgnoreCase);

    private void RegisterAll(Object? controller, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] Type type)
    {
        // 找到容器，注册控制器
        var container = serviceProvider?.GetService<IObjectContainer>();
        if (container != null)
        {
            if (controller == null)
                container.AddTransient(type, type);
            else
                container.AddSingleton(type, controller);
        }

        // 是否要求Api特性
        var requireApi = type.GetCustomAttribute<ApiAttribute>() != null;

        var flag = BindingFlags.Public | BindingFlags.Instance;
        // 如果要求Api特性，则还需要遍历私有方法和静态方法
        if (requireApi) flag |= BindingFlags.NonPublic | BindingFlags.Static;
        foreach (var mi in type.GetMethods(flag))
        {
            if (mi.IsSpecialName) continue;
            if (mi.DeclaringType == typeof(Object)) continue;
            if (requireApi && mi.GetCustomAttribute<ApiAttribute>() == null) continue;

            var act = new ApiAction(mi, type)
            {
                Controller = controller
            };

            //Services[act.Name] = act;
            Add(act);
        }
    }

    /// <summary>注册服务提供类。该类的所有公开方法将直接暴露</summary>
    /// <typeparam name="TService">服务控制器类型。AOT 与裁剪下，该类型的公开/非公开方法与公开构造将按需保留</typeparam>
    public virtual void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] TService>() => RegisterAll(null, typeof(TService));

    /// <summary>注册服务</summary>
    /// <param name="controller">控制器对象</param>
    /// <param name="method">动作名称。为空时遍历控制器所有公有成员方法</param>
    /// <remarks>运行时类型入口，裁剪与 AOT 下无法静态保证动作方法被保留，请优先使用泛型入口 <see cref="Register{TService}()"/></remarks>
    [RequiresUnreferencedCode("控制器类型在运行时确定，裁剪与 AOT 下无法静态保留其动作方法，请改用泛型入口 Register<TService>()")]
    public virtual void Register(Object controller, String? method)
    {
        if (controller == null) throw new ArgumentNullException(nameof(controller));

        var type = controller is Type t ? t : controller.GetType();

        if (!method.IsNullOrEmpty())
        {
            var mi = type.GetMethodEx(method) ?? throw new ArgumentOutOfRangeException(nameof(method));
            var act = new ApiAction(mi, type)
            {
                Controller = controller
            };

            //Services[act.Name] = act;
            Add(act);
        }
        else
        {
            RegisterAll(controller, type);
        }
    }

    /// <summary>添加服务</summary>
    public virtual void Add(ApiAction api) => Services[api.Name] = api;

    /// <summary>查找服务</summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public virtual ApiAction? Find(String action)
    {
        if (Services.TryGetValue(action, out var mi)) return mi;

        // 局部模糊匹配
        var p = action.IndexOf('/');
        if (p >= 0)
        {
            var ctrl = action.Substring(0, p);
            if (Services.TryGetValue(ctrl + "/*", out mi)) return mi;
        }

        // 全局模糊匹配
        if (Services.TryGetValue("*", out mi)) return mi;

        return null;
    }

    /// <summary>创建控制器实例</summary>
    /// <param name="api">Api动作</param>
    /// <param name="serviceProvider2">服务提供者</param>
    /// <returns></returns>
    public virtual Object CreateController(ApiAction api, IServiceProvider serviceProvider2)
    {
        var controller = api.Controller;
        if (controller != null) return controller;

        // 从容器里拿控制器实例，或者借助容器创建控制器实例
        controller ??= serviceProvider2?.GetService(api.Type);
        controller ??= serviceProvider2?.CreateInstance(api.Type);

        controller ??= serviceProvider?.GetService(api.Type);
        controller ??= serviceProvider?.CreateInstance(api.Type);

        controller ??= api.Type.CreateInstance();
        if (controller == null) throw new InvalidDataException(L.T("Cannot create an instance of [{0}]", api.Type.FullName));

        return controller;
    }
}