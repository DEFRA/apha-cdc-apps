using System.Text.Json;

namespace CDC.Web.Infrastructure;

/// <summary>
/// The Search page's Display checkboxes and selected letter as the user last chose them, held in the session so that
/// they are as the user left them on coming back to the page from another one, as the legacy page kept them in its
/// session. The keyword, species, sort order and page size are not kept, as in legacy.
/// </summary>
internal sealed record RememberedSearchFilters(
    bool DisplayPublished,
    bool DisplayDraft,
    bool DisplayScenarios,
    string SelectedLetter)
{
    private const string SessionKey = "SearchFilters";
    private const string AllLetters = "All";

    /// <summary>Gets what the session holds, or <see langword="null"/> before the user has chosen any filters.</summary>
    public static RememberedSearchFilters? Read(ISession session)
    {
        var json = session.GetString(SessionKey);

        return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<RememberedSearchFilters>(json);
    }

    /// <summary>Keeps these filters in the session. Only a letter the alphabet links offer is kept; any other becomes "All".</summary>
    public void Write(ISession session) =>
        session.SetString(SessionKey, JsonSerializer.Serialize(this with { SelectedLetter = ToKnownLetter(SelectedLetter) }));

    private static string ToKnownLetter(string? letter) =>
        letter is { Length: 1 } && char.IsAsciiLetter(letter[0]) ? letter.ToUpperInvariant() : AllLetters;
}
