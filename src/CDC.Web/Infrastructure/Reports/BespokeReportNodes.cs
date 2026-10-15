using CDC.Web.Models;

namespace CDC.Web.Infrastructure.Reports;

/// <summary>
/// Encodes and decodes the values of the "Create bespoke report" tree's nodes. A node's value is its
/// identifier followed by its type's number (0 profile, 1 section, 2 question, 3 guidance), for
/// example <c>{section id}1</c>: the legacy page stored exactly that in each template's ticked items,
/// so templates are shared with it in both directions.
/// </summary>
public static class BespokeReportNodes
{
    private const int GuidLength = 36;

    /// <summary>Builds a node's value.</summary>
    /// <param name="id">The section, question or profile version identifier.</param>
    /// <param name="type">The node's type.</param>
    /// <returns>The identifier followed by the type's number.</returns>
    public static string Value(Guid id, BespokeNodeType type) => $"{id:D}{(int)type}";

    /// <summary>Reads a node's value.</summary>
    /// <param name="value">The identifier followed by the type as a number or, as a fallback, as a name.</param>
    /// <param name="id">The identifier.</param>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> when the value is well formed.</returns>
    public static bool TryParse(string? value, out Guid id, out BespokeNodeType type)
    {
        id = Guid.Empty;
        type = BespokeNodeType.Profile;

        if (value is null || value.Length <= GuidLength || !Guid.TryParseExact(value.AsSpan(0, GuidLength), "D", out id))
        {
            return false;
        }

        var suffix = value[GuidLength..];

        return suffix.All(char.IsAsciiLetterOrDigit) && Enum.TryParse(suffix, out type) && Enum.IsDefined(type);
    }

    /// <summary>Sorts the ticked nodes into the sections, questions and guidance they stand for. Malformed values are ignored.</summary>
    /// <param name="checkedValues">The values of the ticked nodes.</param>
    /// <returns>The selection.</returns>
    public static BespokeReportSelection ToSelection(IEnumerable<string>? checkedValues)
    {
        HashSet<Guid> sections = [];
        HashSet<Guid> questions = [];
        HashSet<Guid> guidance = [];

        foreach (var value in checkedValues ?? [])
        {
            if (!TryParse(value, out var id, out var type))
            {
                continue;
            }

            switch (type)
            {
                case BespokeNodeType.Section:
                    sections.Add(id);
                    break;
                case BespokeNodeType.Question:
                    questions.Add(id);
                    break;
                case BespokeNodeType.Guidance:
                    guidance.Add(id);
                    break;
            }
        }

        return new BespokeReportSelection(sections, questions, guidance);
    }
}
