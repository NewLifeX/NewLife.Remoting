using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace NewLife.Remoting.Extensions.ModelBinders;

/// <summary>接口模型绑定器</summary>
/// <remarks>
/// 仅用于 ASP.NET Core MVC 宿主。MVC 本身不支持 NativeAOT（官方支持矩阵：MVC ❌），
/// 故此处按业务既定行为使用基于 <see cref="Type"/> 的 JSON 反序列化，并以抑制声明标注该边界；
/// AOT 应用请使用最小 API/端点方式接入，不要注册本绑定器
/// </remarks>
public class InterfaceModelBinder : IModelBinder
{
    /// <summary>对于Json请求，从body中读取参数</summary>
    /// <param name="bindingContext"></param>
    /// <returns></returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "MVC 宿主不支持 NativeAOT；AOT 应用使用最小 API 端点接入，不注册本绑定器")]
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var provider = bindingContext.HttpContext.RequestServices;
        var modelType = bindingContext.ModelType;

        // 从当前请求的 DI 容器解析接口的实现类型。
        // 不能使用进程级静态缓存：同一进程内可能运行多个 Web 应用（如测试中 IoTZero 与 ZeroServer），
        // 各自对同一接口注册不同的实现类型，静态缓存会被先请求的应用污染，导致后请求者反序列化成错误类型。
        // 代价是每次请求创建一个临时 DTO 实例来获取类型，相比 JSON 反序列化开销可忽略。
        var implType = provider.GetService(modelType)?.GetType() ?? modelType;

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
/// <remarks>仅用于 ASP.NET Core MVC 宿主，参见 <see cref="InterfaceModelBinder"/></remarks>
public class InterfaceModelBinderProvider : IModelBinderProvider
{
    /// <summary>获取绑定器</summary>
    /// <param name="context"></param>
    /// <returns></returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "MVC 宿主不支持 NativeAOT；AOT 应用使用最小 API 端点接入，不注册本绑定器")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "MVC 宿主不支持 NativeAOT；AOT 应用使用最小 API 端点接入，不注册本绑定器")]
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