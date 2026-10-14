using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

namespace CDC.Web.Features.DiseaseRanking;

/// <summary>Renders a Razor view to a string outside a normal request pipeline.</summary>
public interface IRazorViewToStringRenderer
{
    /// <summary>Renders the view at <paramref name="viewPath"/> with <paramref name="model"/>.</summary>
    /// <typeparam name="TModel">The view's model type.</typeparam>
    /// <param name="viewPath">An application-relative path such as <c>~/Features/.../View.cshtml</c>.</param>
    /// <param name="model">The model.</param>
    Task<string> RenderAsync<TModel>(string viewPath, TModel model);
}

/// <summary>Default <see cref="IRazorViewToStringRenderer"/> using the application's Razor view engine.</summary>
/// <param name="viewEngine">Locates views.</param>
/// <param name="tempDataProvider">Supplies temp data to the view.</param>
/// <param name="serviceProvider">Supplies services to the view.</param>
public sealed class RazorViewToStringRenderer(
    IRazorViewEngine viewEngine,
    ITempDataProvider tempDataProvider,
    IServiceProvider serviceProvider) : IRazorViewToStringRenderer
{
    /// <inheritdoc />
    public async Task<string> RenderAsync<TModel>(string viewPath, TModel model)
    {
        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        var result = viewEngine.GetView(executingFilePath: null, viewPath, isMainPage: true);

        if (!result.Success)
        {
            throw new InvalidOperationException($"The view '{viewPath}' was not found. Searched: {string.Join(", ", result.SearchedLocations)}");
        }

        await using var writer = new StringWriter();
        var viewData = new ViewDataDictionary<TModel>(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model };
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
