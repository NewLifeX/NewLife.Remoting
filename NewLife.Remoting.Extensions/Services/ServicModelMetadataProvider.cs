using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Extensions.Options;
using NewLife.Remoting.Models;

namespace NewLife.Remoting.Extensions.Services;

class ServiceModelBinder : IModelBinder
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IList<IInputFormatter> _formatters;
    private readonly Func<Stream, Encoding, TextReader> _readerFactory;
    private readonly MvcOptions _options;

    public ServiceModelBinder(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _formatters = serviceProvider.GetServices<IInputFormatter>().ToList();
        _readerFactory = _serviceProvider.GetRequiredService<IHttpRequestStreamReaderFactory>().CreateReader;
        _options = serviceProvider.GetRequiredService<IOptions<MvcOptions>>().Value;
    }

    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext, nameof(bindingContext));
        var modelBindingKey = (!bindingContext.IsTopLevelObject) ? bindingContext.ModelName : (bindingContext.BinderModelName ?? String.Empty);
        var httpContext = bindingContext.HttpContext;
        var inputFormatterContext = new InputFormatterContext(httpContext, modelBindingKey, bindingContext.ModelState, bindingContext.ModelMetadata, _readerFactory, false);
        IInputFormatter formatter = null;
        for (var i = 0; i < _formatters.Count; i++)
        {
            if (_formatters[i].CanRead(inputFormatterContext))
            {
                formatter = _formatters[i];
                break;
            }
        }
        if (formatter == null)
        {
            var exception = new UnsupportedContentTypeException(httpContext.Request.ContentType);
            bindingContext.ModelState.AddModelError(modelBindingKey, exception, bindingContext.ModelMetadata);
            return;
        }
        try
        {
            var inputFormatterResult = await formatter.ReadAsync(inputFormatterContext);
            if (inputFormatterResult.HasError)
            {
                return;
            }
            if (inputFormatterResult.IsModelSet)
            {
                var model = inputFormatterResult.Model;
                bindingContext.Result = ModelBindingResult.Success(model);
            }
            else
            {
                var errorMessage = bindingContext.ModelMetadata.ModelBindingMessageProvider.MissingRequestBodyRequiredValueAccessor();
                bindingContext.ModelState.AddModelError(modelBindingKey, errorMessage);
            }
        }
        catch (Exception ex) when (ex is InputFormatterException || ShouldHandleException(formatter))
        {
            bindingContext.ModelState.AddModelError(modelBindingKey, ex, bindingContext.ModelMetadata);
        }
    }

    private static Boolean ShouldHandleException(IInputFormatter formatter)
    {
        return ((formatter as IInputFormatterExceptionPolicy)?.ExceptionPolicy ?? InputFormatterExceptionPolicy.MalformedInputExceptions) == InputFormatterExceptionPolicy.AllExceptions;
    }
}

class ServiceModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        if (!context.Metadata.IsComplexType) return null;

        var type = context.Metadata.ModelType;
        if (type.IsInterface && context.Services?.GetService(type) != null)
        {
            return new ServiceModelBinder(context.Services);
        }

        return null;
    }
}

class ServicModelMetadataProvider : DefaultModelMetadataProvider
{
    private readonly IServiceProvider _serviceProvider;

    public ServicModelMetadataProvider(ICompositeMetadataDetailsProvider detailsProvider, IServiceProvider serviceProvider)
        : base(detailsProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override ModelMetadata GetMetadataForType(Type modelType)
    {
        if (modelType.IsInterface)
        {
            var momdel = _serviceProvider.GetService(modelType);
            if (momdel != null)
            {
                modelType = momdel.GetType();
            }
        }

        return base.GetMetadataForType(modelType);
    }

    //protected override ModelMetadata CreateModelMetadata(DefaultMetadataDetails entry) => base.CreateModelMetadata(entry);

    protected override DefaultMetadataDetails CreateParameterDetails(ModelMetadataIdentity key) => base.CreateParameterDetails(key);

    protected override DefaultMetadataDetails[] CreatePropertyDetails(ModelMetadataIdentity key) => base.CreatePropertyDetails(key);

    protected override DefaultMetadataDetails CreateTypeDetails(ModelMetadataIdentity key) => base.CreateTypeDetails(key);

    public override IEnumerable<ModelMetadata> GetMetadataForProperties(Type modelType) => base.GetMetadataForProperties(modelType);

    protected override ModelMetadata CreateModelMetadata(DefaultMetadataDetails entry)
    {
        var modelType = entry.Key.ModelType;

        if (modelType.IsInterface && (modelType == typeof(ILoginRequest) || modelType == typeof(IPingRequest)))
        {
            var model = _serviceProvider.GetService(modelType);
            if (model != null)
            {
                var key = ModelMetadataIdentity.ForType(model.GetType());
                entry = new DefaultMetadataDetails(key, entry.ModelAttributes);
            }
        }

        return base.CreateModelMetadata(entry);
    }
}