namespace CDC.Web.Models;

/// <summary>
/// Data needed to render the shared <c>_ProfileLeftNav</c> partial: the "Profile reports" links,
/// the "Notes" link, and the profile reference section list - shown on every disease-profile
/// page (EditProfileQuestions, Notes), so it is built and rendered once.
/// </summary>
/// <param name="CanPrintProfileSection">Whether the "Print full profile" link is shown.</param>
/// <param name="CanViewProfileReports">Whether the summary report links are shown.</param>
/// <param name="NotesUrl">The URL to the standalone Notes page.</param>
/// <param name="IsNotesActive">Whether the Notes page is the one currently being viewed.</param>
/// <param name="Subnav">The profile reference section list (1-16).</param>
public sealed record ProfileLeftNavViewModel(
    bool CanPrintProfileSection,
    bool CanViewProfileReports,
    string? NotesUrl,
    bool IsNotesActive,
    SectionSubnavViewModel Subnav);
