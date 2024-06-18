using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace NewLife.Remoting.Extensions.ModelBinders;

/// <summary>接口模型绑定器</summary>
public class InterfaceModelBinder : IModelBinder
{
    /// <summary>对于Json请求，从body中读取参数</summary>
    /// <param name="bindingContext"></param>
    /// <returns></returns>
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var provider = bindingContext.HttpContext.RequestServices;
        var modelType = bindingContext.ModelType;

        // 从容器中获取接口类型对应实例
        var model = provider.GetRequiredService(modelType);

        try
        {
            var req = bindingContext.HttpContext.Request;
            var entityBody = await req.ReadFromJsonAsync(model!.GetType());

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