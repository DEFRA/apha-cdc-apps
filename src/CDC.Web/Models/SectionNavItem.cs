namespace CDC.Web.Models;

/// <summary>One entry in a page's section sub-navigation list or previous/next pagination link.</summary>
/// <param name="IsValid">
/// Whether the section has passed validation, shown as a green tick/red cross before the title.
/// <see langword="null"/> when the page does not track validation status, in which case no icon
/// is shown.
/// </param>
public sealed record SectionNavItem(int Number, string Title, string? Url, bool IsCurrent = false, bool? IsValid = null);

/// <summary>Data needed to render the shared <c>_SectionSubnav</c> partial.</summary>
/// <param name="AriaLabel">The navigation landmark's accessible name.</param>
/// <param name="Items">The sections to list, in display order.</param>
public sealed record SectionSubnavViewModel(string AriaLabel, IReadOnlyList<SectionNavItem> Items);

/// <summary>Data needed to render the shared <c>_SectionPagination</c> partial.</summary>
public sealed record SectionPaginationViewModel(SectionNavItem? Previous, SectionNavItem? Next);
