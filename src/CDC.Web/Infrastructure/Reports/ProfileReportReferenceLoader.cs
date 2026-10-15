using CDC.Common.Contracts;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure.Reports;

/// <summary>Loads the scientific paper, legislative and further-information references printed on the landscape pages of the full profile.</summary>
/// <param name="sectionsApi">Typed client for the profile sections and notes endpoints on CDC.Api.</param>
public sealed class ProfileReportReferenceLoader(IProfileSectionsApiService sectionsApi)
{
    private static readonly (string NormalisedType, string Title, string EmptyText)[] Kinds =
    [
        (ProfileNoteDisplayHelper.ScientificPaperReferenceType, "Scientific paper reference", "- No scientific paper references supplied -"),
        (ProfileNoteDisplayHelper.LegislativeReferenceType, "Legislative reference", "- No legislative references supplied -"),
        (ProfileNoteDisplayHelper.SourceOfFurtherInformationType, "Source of further information", "- No sources of further information supplied -")
    ];

    /// <summary>Loads every kind of reference for each section, or for one section.</summary>
    /// <param name="content">The profile version's report content, which supplies the sections and question numbers.</param>
    /// <param name="profileSectionId">The one section to load, or <see cref="Guid.Empty"/> for every section.</param>
    /// <param name="cancellationToken">Cancels the requests.</param>
    /// <returns>One entry per section, each with a table per kind of reference.</returns>
    public async Task<IReadOnlyList<ReportReferenceSectionView>> LoadAsync(ProfileReportContentDto content, Guid profileSectionId, CancellationToken cancellationToken)
    {
        var questionNumbers = content.Sections
            .SelectMany(section => section.Questions.Select(question => (question.Id, Number: $"{section.SectionNumber}.{question.QuestionNumber}")))
            .ToDictionary(pair => pair.Id, pair => pair.Number);
        var noteTypes = await sectionsApi.GetProfileNoteTypesAsync(cancellationToken);
        var notesByKind = new List<IReadOnlyList<ProfileNoteDto>>();

        foreach (var (normalisedType, _, _) in Kinds)
        {
            var noteType = noteTypes.FirstOrDefault(type => ProfileNoteDisplayHelper.NormaliseNoteTypeName(type.Name) == normalisedType);
            notesByKind.Add(noteType is null
                ? []
                : await sectionsApi.GetProfileNotesByVersionAsync(content.ProfileVersionId, noteType.Id, cancellationToken));
        }

        return
        [
            .. content.Sections
                .Where(section => profileSectionId == Guid.Empty || section.Id == profileSectionId)
                .Select(section => new ReportReferenceSectionView(
                    $"{section.SectionNumber}. {ProfileTitleHtmlFormatter.ToPlainText(section.Name)}",
                    [.. Kinds.Select((kind, index) => BuildTable(kind, notesByKind[index], section.Id, questionNumbers))]))
        ];
    }

    private static ReportReferenceTableView BuildTable(
        (string NormalisedType, string Title, string EmptyText) kind,
        IReadOnlyList<ProfileNoteDto> notes,
        Guid sectionId,
        Dictionary<Guid, string> questionNumbers)
    {
        var rows = notes
            .Where(note => note.QuestionReferences.Any(reference => reference.ProfileSectionId == sectionId))
            .OrderBy(note => ProfileTitleHtmlFormatter.ToPlainText(note.NoteText), StringComparer.OrdinalIgnoreCase)
            .Select(note => new ReportReferenceRow(
                string.Join(", ", note.QuestionReferences
                    .Select(reference => questionNumbers.GetValueOrDefault(reference.ProfileQuestionId))
                    .Where(number => !string.IsNullOrEmpty(number))),
                note.NoteText))
            .ToList();

        return new ReportReferenceTableView(kind.Title, kind.EmptyText, rows);
    }
}
