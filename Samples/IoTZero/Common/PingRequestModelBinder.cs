using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace IoTZero.Common;

/// <summary>
/// Ping请求接口模型绑定器
/// </summary>
public sealed class PingRequestModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        throw new NotImplementedException();
    }
}