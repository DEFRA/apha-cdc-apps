using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Pages;

/// <summary>
/// Displays the species and species group hierarchy retrieved from CDC.Api.
/// </summary>
/// <param name="speciesApiService">Typed client for the species endpoints on CDC.Api.</param>
/// <param name="logger">Structured logger.</param>
public class ViewSpeciesDataModel(ISpeciesApiService speciesApiService, ILogger<ViewSpeciesDataModel> logger)
    : BreadcrumbPageModelBase("Species Data", "View species data")
{
    private const string SpeciesKey = "species";

    /// <summary>Gets the species hierarchy, built from every active species returned by the API.</summary>
    public TreeViewViewModel SpeciesTree { get; private set; } = EmptyTree();

    /// <summary>Gets a value indicating whether the species API call failed.</summary>
    public bool HasError { get; private set; }

    /// <summary>Gets the message to show the user when <see cref="HasError"/> is <see langword="true"/>.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Loads the species list and builds the tree shown on the page.</summary>
    /// <param name="cancellationToken">Cancels the request if the client disconnects.</param>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            var species = await speciesApiService.GetAllSpeciesAsync(cancellationToken);

            SpeciesTree = new TreeViewViewModel
            {
                IdPrefix = SpeciesKey,
                FieldName = SpeciesKey,
                ItemNameSingular = SpeciesKey,
                ItemNamePlural = SpeciesKey,
                Nodes = SpeciesTreeBuilder.Build(species)
            };
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            // HttpRequestException: network/DNS failure or a non-success status code.
            // TaskCanceledException: the resilience handler's own request timeout expired.
            // NotSupportedException: the response was not valid JSON for SpeciesDto[].
            logger.SpeciesLoadFailed(exception);

            HasError = true;
            ErrorMessage = "We could not load species data. Try again later.";
        }
    }

    private static TreeViewViewModel EmptyTree() => new()
    {
        IdPrefix = SpeciesKey,
        FieldName = SpeciesKey,
        ItemNameSingular = SpeciesKey,
        ItemNamePlural = SpeciesKey,
        Nodes = []
    };
}

/// <summary>Source-generated structured log messages for <see cref="ViewSpeciesDataModel"/>.</summary>
internal static partial class ViewSpeciesDataLog
{
    [LoggerMessage(EventId = 2000, Level = LogLevel.Error, Message = "Failed to load species data from CDC.Api")]
    public static partial void SpeciesLoadFailed(this ILogger logger, Exception exception);
}
