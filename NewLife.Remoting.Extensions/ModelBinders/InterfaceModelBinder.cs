using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace NewLife.Remoting.Extensions.ModelBinders;

/// <summary>接口模型绑定器</summary>
public class InterfaceModelBinder : IModelBinder
{
    /// <summary>接口到实现类型的缓存。避免每次请求都创建服务实例只为获取实现类型</summary>
    private static readonly ConcurrentDictionary<Type, Type> _typeCache = new();

    /// <summary>对于Json请求，从body中读取参数</summary>
    /// <param name="bindingContext"></param>
    /// <returns></returns>
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var provider = bindingContext.HttpContext.RequestServices;
        var modelType = bindingContext.ModelType;

        // 缓存接口到实现类型的映射。首次解析后复用，避免每次请求都创建服务实例
        var implType = _typeCache.GetOrAdd(modelType, t => provider.GetRequiredService(t)?.GetType() ?? t);

        try
        {
            var req = bindingContext.HttpContext.Request;
            var entityBody = await req.ReadFromJsonAsync(implType).ConfigureAwait(false);

            bindingContext.Result = ModelBindingResult.Success(entityBody);
        }
        catch (Exception ex)
        {
            bindingContext.ModelState.AddModelError(bindingContext.ModelName, ex.Message);
        }
    }
}

/// <summary>模型绑定器提供者</summary>
public class InterfaceModelBinderProvider : IModelBinderProvider
{
    /// <summary>获取绑定器</summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        if (!context.Metadata.IsComplexType) return null;

        var type = context.Metadata.ModelType;
        if (type.IsInterface && context.Services?.GetService(type) != null)
        {
            return new InterfaceModelBinder();
        }

        return null;
    }
}