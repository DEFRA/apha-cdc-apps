using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

namespace CDC.Web.Infrastructure.Pdf;

/// <summary>Renders a Razor view to an HTML string outside the normal response pipeline.</summary>
public interface IRazorViewRenderer
{
    /// <summary>Renders a view with a model.</summary>
    /// <typeparam name="TModel">The view's model type.</typeparam>
    /// <param name="viewPath">The view's application-relative path, for example <c>/Views/Pdf/ContributionsReport.cshtml</c>.</param>
    /// <param name="model">The model to render.</param>
    /// <returns>The rendered HTML.</returns>
    Task<string> RenderAsync<TModel>(string viewPath, TModel model);
}

/// <summary>Default <see cref="IRazorViewRenderer"/>, using the current request's services.</summary>
/// <param name="viewEngine">Locates Razor views.</param>
/// <param name="tempDataProvider">Supplies temp data for the view context.</param>
/// <param name="httpContextAccessor">Gives access to the current request.</param>
public sealed class RazorViewRenderer(
    IRazorViewEngine viewEngine,
    ITempDataProvider tempDataProvider,
    IHttpContextAccessor httpContextAccessor) : IRazorViewRenderer
{
    /// <inheritdoc />
    public async Task<string> RenderAsync<TModel>(string viewPath, TModel model)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("A view can only be rendered during a request.");

        var viewResult = viewEngine.GetView(executingFilePath: null, viewPath, isMainPage: true);

        if (!viewResult.Success)
        {
            throw new InvalidOperationException($"The view '{viewPath}' was not found.");
        }

        var actionContext = new ActionContext(httpContext, httpContext.GetRouteData(), new ActionDescriptor());
        var viewData = new ViewDataDictionary<TModel>(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model };

        await using var writer = new StringWriter();
        var viewContext = new ViewContext(
            actionContext,
            viewResult.View,
            viewData,
            new TempDataDictionary(httpContext, tempDataProvider),
            writer,
            new HtmlHelperOptions());

        await viewResult.View.RenderAsync(viewContext);

        return writer.ToString();
    }
}
