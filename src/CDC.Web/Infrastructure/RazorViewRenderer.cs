using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

namespace CDC.Web.Infrastructure;

/// <summary>Renders a standalone Razor view (no layout) to an HTML string.</summary>
public interface IRazorViewRenderer
{
    /// <param name="viewPath">Application-relative path, for example <c>~/Views/Reports/Example.cshtml</c>.</param>
    /// <param name="model">The view model.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<string> RenderAsync<TModel>(string viewPath, TModel model, CancellationToken cancellationToken = default);
}

/// <summary>Default <see cref="IRazorViewRenderer"/>.</summary>
public sealed class RazorViewRenderer(
    IRazorViewEngine viewEngine,
    ITempDataProvider tempDataProvider,
    IServiceProvider serviceProvider) : IRazorViewRenderer
{
    /// <inheritdoc />
    public async Task<string> RenderAsync<TModel>(string viewPath, TModel model, CancellationToken cancellationToken = default)
    {
        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider, RequestAborted = cancellationToken };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor(), new ModelStateDictionary());

        var result = viewEngine.GetView(executingFilePath: null, viewPath, isMainPage: false);

        if (!result.Success)
        {
            throw new InvalidOperationException($"View '{viewPath}' was not found.");
        }

        var viewData = new ViewDataDictionary<TModel>(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model };

        await using var writer = new StringWriter();
        var viewContext = new ViewContext(
            actionContext,
            result.View,
            viewData,
            new TempDataDictionary(httpContext, tempDataProvider),
            writer,
            new HtmlHelperOptions());

        await result.View.RenderAsync(viewContext);

        return writer.ToString();
    }
}
