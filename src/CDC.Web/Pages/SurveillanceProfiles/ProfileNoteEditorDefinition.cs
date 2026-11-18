using CDC.Web.Infrastructure;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>What tells one kind of note that a tab edits from another.</summary>
/// <param name="Key">Names the kind in the querystring when a tab edits several (<c>type</c>), and prefixes the names of that table's own
/// paging and sort values. Empty for a tab that edits one kind, whose values then have no prefix.</param>
/// <param name="NormalisedName">The kind's name as <see cref="ProfileNoteDisplayHelper.NormaliseNoteTypeName"/> reduces CDC.Api's, to find its note type.</param>
/// <param name="FallbackName">The singular name used until CDC.Api's note type is read, and when it cannot be.</param>
/// <param name="FallbackPluralName">The plural name used when CDC.Api's note type has none.</param>
public sealed record ProfileNoteEditorDefinition(string Key, string NormalisedName, string FallbackName, string FallbackPluralName)
{
    /// <summary>The key of the scientific paper references, which names their table's values in the querystring.</summary>
    public const string ScientificPaperKey = "scientific";

    /// <summary>The key of the legislative references, which names their table's values in the querystring.</summary>
    public const string LegislativeKey = "legislative";

    /// <summary>Gets the sources of further information the Further information tab edits.</summary>
    public static ProfileNoteEditorDefinition SourceOfFurtherInformation { get; } = new(
        string.Empty,
        ProfileNoteDisplayHelper.SourceOfFurtherInformationType,
        "Source of further information",
        "Sources of further information");

    /// <summary>Gets the scientific paper references the References tab edits.</summary>
    public static ProfileNoteEditorDefinition ScientificPaperReference { get; } = new(
        ScientificPaperKey,
        ProfileNoteDisplayHelper.ScientificPaperReferenceType,
        "Scientific paper reference",
        "Scientific paper references");

    /// <summary>Gets the legislative references the References tab edits.</summary>
    public static ProfileNoteEditorDefinition LegislativeReference { get; } = new(
        LegislativeKey,
        ProfileNoteDisplayHelper.LegislativeReferenceType,
        "Legislative reference",
        "Legislative references");
}
