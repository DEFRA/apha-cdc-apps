using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// Displays every note (reference/further information entry) recorded against a profile's
/// current version, across all of its sections, filterable by reference type. Replaces the
/// legacy <c>Notes.aspx</c> page.
/// </summary>
/// <param name="apiClient">Typed client for the profile management endpoints on CDC.Api.</param>
/// <param name="profileSectionsApiService">Typed client for the profile sections/notes endpoints on CDC.Api.</param>
/// <param name="logger">Structured logger.</param>
public class NotesModel(
    IApiClient apiClient,
    IProfileSectionsApiService profileSectionsApiService,
    ILogger<NotesModel> logger) : PageModel
{
    /// <summary>The empty-state message shown when the selected reference type has no notes.</summary>
    public const string NoNotesMessage = "No notes available for the selected reference type.";

    /// <summary>Gets the "Items per page" options, matching the legacy GridView's page size convention.</summary>
    public static IReadOnlyList<string> PageSizeOptions { get; } = ["10", "25", "50", "100"];

    /// <summary>Gets or sets the profile being browsed, bound from the page route.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid ProfileId { get; set; }

    /// <summary>Gets or sets the selected reference type, bound from the querystring.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid? ReferenceTypeId { get; set; }

    /// <summary>Gets or sets the current 1-based results page.</summary>
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    /// <summary>Gets or sets the number of notes shown per page.</summary>
    [BindProperty(SupportsGet = true)]
    public string PageSize { get; set; } = PageSizeOptions[0];

    /// <summary>Gets or sets which column the table is sorted by: "Reference" or "QuestionReference".</summary>
    [BindProperty(SupportsGet = true)]
    public string SortColumn { get; set; } = "Reference";

    /// <summary>Gets or sets the sort direction: "Ascending" or "Descending".</summary>
    [BindProperty(SupportsGet = true)]
    public string SortDirection { get; set; } = "Ascending";

    /// <summary>Gets the profile's full display title, once loaded.</summary>
    public string? ProfileTitle { get; private set; }

    /// <summary>Gets the profile's current version id, once loaded.</summary>
    public Guid CurrentProfileVersionId { get; private set; }

    /// <summary>Gets every profile reference section, for the left-hand navigation and the
    /// "Question reference" column's section.question number lookup.</summary>
    public IReadOnlyList<ProfileSectionMetadataDto> Sections { get; private set; } = [];

    /// <summary>Gets every reference type, for the dropdown.</summary>
    public IReadOnlyList<ProfileNoteTypeDto> ReferenceTypes { get; private set; } = [];

    /// <summary>Gets the reference type actually applied (the requested one if it exists,
    /// otherwise the legacy default), or <see langword="null"/> when there are none at all.</summary>
    public Guid? SelectedReferenceTypeId { get; private set; }

    /// <summary>Gets the heading shown above the table (the selected reference type's plural
    /// name), or empty when no reference type is selected yet.</summary>
    public string NotesHeading { get; private set; } = string.Empty;

    /// <summary>Gets the current page of notes, sorted and filtered by the selected reference type.</summary>
    public IReadOnlyList<ProfileNoteTableRowView> Rows { get; private set; } = [];

    /// <summary>Gets the total number of notes matching the selected reference type, before paging.</summary>
    public int TotalRecords { get; private set; }

    /// <summary>Gets the total number of result pages.</summary>
    public int TotalPages { get; private set; } = 1;

    /// <summary>Gets a value indicating whether the profile or its notes failed to load.</summary>
    public bool HasError { get; private set; }

    /// <summary>Gets a value indicating whether the profile has no current version to browse.</summary>
    public bool HasNoVersion { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            var profile = await apiClient.GetManageProfileAsync(ProfileId, cancellationToken);

            if (profile is null)
            {
                return NotFound();
            }

            ProfileTitle = profile.FullTitle;

            if (profile.CurrentProfileVersionId == Guid.Empty)
            {
                HasNoVersion = true;
                return Page();
            }

            CurrentProfileVersionId = profile.CurrentProfileVersionId;

            var metadata = await profileSectionsApiService.GetProfileQuestionnaireMetadataAsync(cancellationToken);
            Sections = metadata.Sections;

            await LoadReferenceTypesAsync(cancellationToken);
            await LoadNotesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.ProfileNotesPageLoadFailed(exception, ProfileId);
            HasError = true;
        }

        return Page();
    }

    private async Task LoadReferenceTypesAsync(CancellationToken cancellationToken)
    {
        ReferenceTypes = await profileSectionsApiService.GetProfileNoteTypesAsync(cancellationToken);

        // Legacy default: the dropdown starts on "Please select..." (no type chosen, nothing
        // loaded) until the user picks one; it is never defaulted to a particular type.
        var selected = ReferenceTypeId.HasValue
            ? ReferenceTypes.FirstOrDefault(type => type.Id == ReferenceTypeId.Value)
            : null;

        SelectedReferenceTypeId = selected?.Id;

        if (selected is null)
        {
            NotesHeading = string.Empty;
        }
        else
        {
            NotesHeading = string.IsNullOrEmpty(selected.PluralName) ? selected.Name : selected.PluralName;
        }
    }

    private async Task LoadNotesAsync(CancellationToken cancellationToken)
    {
        if (SelectedReferenceTypeId is null)
        {
            Rows = [];
            TotalRecords = 0;
            TotalPages = 1;
            return;
        }

        var notes = await profileSectionsApiService.GetProfileNotesByVersionAsync(
            CurrentProfileVersionId, SelectedReferenceTypeId.Value, cancellationToken);

        var questionNumbersById = ProfileNoteDisplayHelper.BuildQuestionNumbersById(Sections);

        var allRows = notes
            .Select(note => new ProfileNoteTableRowView(
                note.NoteText,
                ProfileTitleHtmlFormatter.ToPlainText(note.NoteText),
                string.Join(", ", note.QuestionReferences
                    .Select(reference => questionNumbersById.GetValueOrDefault(reference.ProfileQuestionId))
                    .Where(number => !string.IsNullOrEmpty(number)))))
            .ToList();

        var descending = string.Equals(SortDirection, "Descending", StringComparison.OrdinalIgnoreCase);
        var sortByQuestionReference = string.Equals(SortColumn, "QuestionReference", StringComparison.OrdinalIgnoreCase);

        IEnumerable<ProfileNoteTableRowView> sorted;

        if (sortByQuestionReference)
        {
            sorted = descending
                ? allRows.OrderByDescending(row => row.QuestionReferenceDisplay, StringComparer.OrdinalIgnoreCase)
                : allRows.OrderBy(row => row.QuestionReferenceDisplay, StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            sorted = descending
                ? allRows.OrderByDescending(row => row.ReferencePlainText, StringComparer.OrdinalIgnoreCase)
                : allRows.OrderBy(row => row.ReferencePlainText, StringComparer.OrdinalIgnoreCase);
        }

        TotalRecords = allRows.Count;

        var pageSize = ResolvePageSize(PageSize);
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalRecords / (double)pageSize));
        PageNumber = Math.Clamp(PageNumber, 1, TotalPages);

        Rows = [.. sorted.Skip((PageNumber - 1) * pageSize).Take(pageSize)];
    }

    private static int ResolvePageSize(string pageSize) =>
        int.TryParse(pageSize, out var parsedPageSize) && parsedPageSize > 0 ? parsedPageSize : 10;

    /// <summary>Builds the querystring URL for a results page link, preserving every other filter.</summary>
    public string? BuildPageUrl(int page) => BuildUrl(new
    {
        ProfileId,
        ReferenceTypeId = SelectedReferenceTypeId,
        PageNumber = page,
        PageSize,
        SortColumn,
        SortDirection
    });

    /// <summary>Builds the querystring URL for a sortable column header: ascending if a different
    /// column (or not yet sorted), otherwise toggling the current direction. Resets to page 1.</summary>
    public string? BuildSortUrl(string column)
    {
        var newDirection = string.Equals(SortColumn, column, StringComparison.OrdinalIgnoreCase)
            && string.Equals(SortDirection, "Ascending", StringComparison.OrdinalIgnoreCase)
                ? "Descending"
                : "Ascending";

        return BuildUrl(new
        {
            ProfileId,
            ReferenceTypeId = SelectedReferenceTypeId,
            PageNumber = 1,
            PageSize,
            SortColumn = column,
            SortDirection = newDirection
        });
    }

    private string? BuildUrl(object routeValues) => Url.Page("/SurveillanceProfiles/Notes", routeValues);
}

/// <summary>Source-generated structured log messages for <see cref="NotesModel"/>.</summary>
internal static partial class NotesLog
{
    [LoggerMessage(EventId = 2400, Level = LogLevel.Error, Message = "Failed to load profile '{ProfileId}' notes from CDC.Api")]
    public static partial void ProfileNotesPageLoadFailed(this ILogger logger, Exception exception, Guid profileId);
}
