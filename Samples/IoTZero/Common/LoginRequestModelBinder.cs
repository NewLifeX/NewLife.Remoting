#nullable enable
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Text.Json;
using NewLife.IoT.Models;
using NewLife.Remoting.Models;

namespace IoTZero.Common
{
    /// <summary>
    /// 登录请求接口模型绑定器
    /// </summary>
    public sealed class LoginRequestModelBinder : IModelBinder
    {
        public async Task BindModelAsync(ModelBindingContext bindingContext)
        {
            if (bindingContext == null)
            {
                throw new ArgumentNullException(nameof(bindingContext));
            }

            HttpRequest request = bindingContext.HttpContext.Request;
            if (request.ContentType != "application/json")
            {
                bindingContext.Result = ModelBindingResult.Failed();
                return;
            }

            try
            {
                // 从request.Body中解析JSON
                using JsonDocument jsonDocument = await JsonDocument.ParseAsync(request.Body);

                // 根据不同的字段特征，解析不同的登录模型类型。
                // 例如，根据是否包含ProductKey、Name、UUID字段，判断是否为LoginInfo模型。
                // TODO: 这里需要原开发者根据实际情况来调整字段特征条件。
                ILoginRequest? queryRequest;
                if (jsonDocument.RootElement.TryGetProperty("ProductKey", out _) &&
                    jsonDocument.RootElement.TryGetProperty("Name", out _) &&
                    jsonDocument.RootElement.TryGetProperty("UUID", out _))
                {
                    queryRequest = jsonDocument.Deserialize<LoginInfo>();
                }
                else if (jsonDocument.RootElement.TryGetProperty("Code", out _) &&
                         jsonDocument.RootElement.TryGetProperty("ClientId", out _))
                {
                    queryRequest = jsonDocument.Deserialize<LoginRequest>();
                }
                else
                {
                    queryRequest = null;
                }

                bindingContext.Result = queryRequest == null ? ModelBindingResult.Failed() : ModelBindingResult.Success(queryRequest);
            }
            catch (JsonException)
            {
                // 处理解析错误
                bindingContext.Result = ModelBindingResult.Failed();
            }
        }
    }
}
