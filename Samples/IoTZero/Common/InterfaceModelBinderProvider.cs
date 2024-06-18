using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using NewLife.Remoting.Models;

namespace IoTZero.Common
{
    /// <summary>
    /// 登录请求接口模型绑定器提供者程序
    /// </summary>
    public sealed class InterfaceModelBinderProvider : IModelBinderProvider
    {
        public IModelBinder GetBinder(ModelBinderProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (context.Metadata.ModelType == typeof(ILoginRequest))
            {
                return new BinderTypeModelBinder(typeof(LoginRequestModelBinder));
            }

            if (context.Metadata.ModelType == typeof(IPingRequest))
            {
                return new BinderTypeModelBinder(typeof(PingRequestModelBinder));
            }

            return null;
        }
    }
}
