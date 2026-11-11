using CDC.Web.Authorization.Constants;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.CrossProfileAdmin;

/// <summary>
/// Printable list of one prioritisation category's criteria, weightings and value scores - replaces the legacy
/// <c>PrintPrioritisationVariables.aspx</c> opened by the "Print category variables" button.
/// </summary>
/// <param name="prioritisationVariablesApiService">Reads the categories from CDC.Api.</param>
/// <param name="logger">Structured logger.</param>
[Authorize(Policy = AuthorizationPolicies.CanManagePrioritisationVariables)]
public class PrintPrioritisationVariablesModel(
    IPrioritisationVariablesApiService prioritisationVariablesApiService,
    ILogger<PrintPrioritisationVariablesModel> logger)
    : BreadcrumbPageModelBase("Print category variables")
{
    /// <summary>Gets or sets the category to print, from the query string.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid CategoryId { get; set; }

    /// <summary>Gets the category being printed.</summary>
    public PrioritisationCategoryDto? Category { get; private set; }

    /// <summary>Gets the message to show when the category could not be loaded.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Loads the category.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The page, or not found when the category does not exist.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        // Relative to this page, so it resolves to the sibling maintenance page.
        Breadcrumb = new BreadcrumbViewModel("Print category variables", "Maintain prioritisation variables", "PrioritisationVariables");

        try
        {
            var categories = await prioritisationVariablesApiService.GetCategoriesAsync(cancellationToken);
            Category = categories.FirstOrDefault(category => category.Id == CategoryId);

            return Category is null ? NotFound() : Page();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.PrintCategoryLoadFailed(exception);
            ErrorMessage = "We could not load the prioritisation category. Try again later.";

            return Page();
        }
    }
}

/// <summary>Source-generated structured log messages for the print page.</summary>
internal static partial class PrintPrioritisationVariablesLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load the prioritisation category to print")]
    public static partial void PrintCategoryLoadFailed(this ILogger logger, Exception exception);
}
