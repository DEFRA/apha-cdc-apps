using System.Globalization;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// The Contributions tab of a profile section: who has contributed to it and when, in a sortable, paged table. Replaces the
/// legacy <c>EditProfileContributions.aspx</c>. Shown only to users allowed to see it (legacy
/// <c>ProfileContributionInfoList.CanGetProfileContributionInfoList()</c>).
/// </summary>
/// <param name="apiClient">Typed client for the profile management endpoints on CDC.Api.</param>
/// <param name="profileSectionsApiService">Typed client for the profile sections endpoints on CDC.Api.</param>
/// <param name="logger">Structured logger.</param>
public class EditProfileContributionsModel(
    IApiClient apiClient,
    IProfileSectionsApiService profileSectionsApiService,
    ILogger<EditProfileContributionsModel> logger) : ProfileSectionTabPageModel(apiClient, profileSectionsApiService, logger)
{
    /// <summary>The <see cref="SortColumn"/> value of the Username column.</summary>
    public const string UserNameSortColumn = "UserName";

    /// <summary>The <see cref="SortColumn"/> value of the Full name column.</summary>
    public const string FullNameSortColumn = "FullName";

    /// <summary>The <see cref="SortColumn"/> value of the Organisation column.</summary>
    public const string OrganisationSortColumn = "OrganisationName";

    /// <summary>The <see cref="SortColumn"/> value of the Contribution date and time column, which the legacy grid sorted by first.</summary>
    public const string ContributionDateSortColumn = "LastContributionDate";

    /// <summary>The <see cref="SortDirection"/> value for lowest first.</summary>
    public const string AscendingDirection = "Ascending";

    /// <summary>The <see cref="SortDirection"/> value for highest first, which the legacy grid used for the contribution date.</summary>
    public const string DescendingDirection = "Descending";

    private static readonly string[] SortColumns = [UserNameSortColumn, FullNameSortColumn, OrganisationSortColumn, ContributionDateSortColumn];

    /// <inheritdoc />
    public override ProfileSectionTab ActiveTab => ProfileSectionTab.Contributions;

    /// <inheritdoc />
    public override int ListPageNumber => PageNumber;

    /// <inheritdoc />
    public override int ListTotalPages => TotalPages;

    /// <summary>Gets or sets the current 1-based results page.</summary>
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    /// <summary>Gets or sets the number of contributions shown per page, one of <see cref="NotesModel.PageSizeOptions"/>.</summary>
    [BindProperty(SupportsGet = true)]
    public string PageSize { get; set; } = NotesModel.PageSizeOptions[0];

    /// <summary>Gets or sets the column the table is sorted by; the legacy grid's <c>LastContributionDate DESC</c> by default.</summary>
    [BindProperty(SupportsGet = true)]
    public string SortColumn { get; set; } = ContributionDateSortColumn;

    /// <summary>Gets or sets the direction the table is sorted in: <see cref="AscendingDirection"/> or <see cref="DescendingDirection"/>.</summary>
    [BindProperty(SupportsGet = true)]
    public string SortDirection { get; set; } = DescendingDirection;

    /// <summary>Gets the current page of the selected section's logged contributions.</summary>
    public IReadOnlyList<ProfileSectionContributionView> Contributions { get; private set; } = [];

    /// <summary>Gets the total number of contributions, before paging.</summary>
    public int TotalRecords { get; private set; }

    /// <summary>Gets the total number of result pages.</summary>
    public int TotalPages { get; private set; } = 1;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var notFound = await LoadProfileContextAsync(cancellationToken);

        if (notFound is not null)
        {
            return notFound;
        }

        if (!IsReady)
        {
            return Page();
        }

        if (!CanViewContributions)
        {
            return NotFound();
        }

        await LoadContributionsAsync(cancellationToken);

        return Page();
    }

    /// <summary>Builds the URL of one page of the table, in the sort and page size it is showing.</summary>
    /// <param name="page">The results page, or <see langword="null"/> for the current one.</param>
    /// <returns>The page URL.</returns>
    public string? BuildListUrl(int? page = null) => BuildUrl(page ?? PageNumber, SortColumn, SortDirection);

    /// <summary>Builds the URL of a sortable column header: ascending when the table is not yet sorted by that column, otherwise
    /// toggling the direction. Returns to the first page.</summary>
    /// <param name="column">The column.</param>
    /// <returns>The page URL.</returns>
    public string? BuildSortUrl(string column)
    {
        var direction = string.Equals(SortColumn, column, StringComparison.OrdinalIgnoreCase)
            && string.Equals(SortDirection, AscendingDirection, StringComparison.OrdinalIgnoreCase)
                ? DescendingDirection
                : AscendingDirection;

        return BuildUrl(1, column, direction);
    }

    /// <summary>Gets the <c>aria-sort</c> value of a sortable column header.</summary>
    /// <param name="column">The column.</param>
    /// <returns>"none" unless the table is sorted by the column.</returns>
    public string GetAriaSort(string column)
    {
        if (!string.Equals(SortColumn, column, StringComparison.OrdinalIgnoreCase))
        {
            return "none";
        }

        return string.Equals(SortDirection, DescendingDirection, StringComparison.OrdinalIgnoreCase) ? "descending" : "ascending";
    }

    private static string ResolveSortColumn(string? column) =>
        SortColumns.FirstOrDefault(known => string.Equals(known, column, StringComparison.OrdinalIgnoreCase)) ?? ContributionDateSortColumn;

    private static IEnumerable<ProfileSectionContributionDto> Sort(IReadOnlyList<ProfileSectionContributionDto> contributions, string column, bool descending) => column switch
    {
        UserNameSortColumn => OrderBy(contributions, contribution => contribution.UserName, StringComparer.OrdinalIgnoreCase, descending),
        FullNameSortColumn => OrderBy(contributions, contribution => contribution.FullName, StringComparer.OrdinalIgnoreCase, descending),
        OrganisationSortColumn => OrderBy(contributions, contribution => contribution.OrganisationName, StringComparer.OrdinalIgnoreCase, descending),
        _ => OrderBy(contributions, contribution => contribution.LastContributionDate, Comparer<DateTime>.Default, descending)
    };

    private static IOrderedEnumerable<ProfileSectionContributionDto> OrderBy<TKey>(
        IEnumerable<ProfileSectionContributionDto> contributions,
        Func<ProfileSectionContributionDto, TKey> key,
        IComparer<TKey> comparer,
        bool descending) =>
        descending ? contributions.OrderByDescending(key, comparer) : contributions.OrderBy(key, comparer);

    private string? BuildUrl(int page, string sortColumn, string sortDirection) =>
        Url.Page(
            ProfileSectionTabs.PagePath(ProfileSectionTab.Contributions),
            new { profileId = ProfileId, section = CurrentSection?.Id, pageNumber = page, pageSize = PageSize, sortColumn, sortDirection });

    /// <summary>Loads the contributions, resiliently: a failure degrades to an empty table rather than failing the page.</summary>
    private async Task LoadContributionsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ProfileSectionContributionDto> contributions = [];

        try
        {
            contributions = await ProfileSectionsApi.GetProfileVersionSectionContributionsAsync(CurrentProfileVersionId, CurrentSection!.Id, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            Logger.ProfileSectionContributionsLoadFailed(exception, ProfileId);
        }

        // Keep the table's choices to the ones it offers, so the links and the drop-down always agree with what is shown.
        SortColumn = ResolveSortColumn(SortColumn);
        var descending = !string.Equals(SortDirection, AscendingDirection, StringComparison.OrdinalIgnoreCase);
        SortDirection = descending ? DescendingDirection : AscendingDirection;

        if (!NotesModel.PageSizeOptions.Contains(PageSize))
        {
            PageSize = NotesModel.PageSizeOptions[0];
        }

        var pageSize = int.Parse(PageSize, CultureInfo.InvariantCulture);

        TotalRecords = contributions.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalRecords / (double)pageSize));
        PageNumber = Math.Clamp(PageNumber, 1, TotalPages);

        Contributions =
        [
            .. Sort(contributions, SortColumn, descending)
                .Skip((PageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(contribution => new ProfileSectionContributionView(
                    contribution.FullName,
                    contribution.UserName,
                    contribution.OrganisationName,
                    contribution.LastContributionDate.ToString(DateTimeDisplayFormat, CultureInfo.InvariantCulture)))
        ];
    }
}
