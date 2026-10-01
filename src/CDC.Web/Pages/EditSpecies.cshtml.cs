using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages;

/// <summary>
/// Displays the fixed-section species data accordion for a single species, loading each
/// section's real questions and recorded answers from CDC.Api. Replaces the legacy
/// <c>EditSpecies.aspx</c> page in its read-only ("View species data") mode.
/// </summary>
public class EditSpeciesModel : PageModel
{
    private readonly ISpeciesApiService speciesApiService;
    private readonly ILogger<EditSpeciesModel> logger;

    public EditSpeciesModel(ISpeciesApiService speciesApiService, ILogger<EditSpeciesModel> logger)
    {
        this.speciesApiService = speciesApiService;
        this.logger = logger;
    }

    /// <summary>Gets or sets the species being viewed, bound from the page route.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid SpeciesId { get; set; }

    /// <summary>Gets or sets the selected left-nav section key, bound from the querystring.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Section { get; set; }

    /// <summary>Gets the species' current display name, once loaded.</summary>
    public string? SpeciesName { get; private set; }

    /// <summary>Gets a value indicating whether the species failed to load.</summary>
    public bool HasError { get; private set; }

    public EditSpeciesSection CurrentSection { get; private set; } = EditSpeciesSectionCatalog.Sections[0];

    public EditSpeciesSection? PreviousSection { get; private set; }

    public EditSpeciesSection? NextSection { get; private set; }

    /// <summary>Gets the real questions and recorded answers for <see cref="CurrentSection"/>, matched by
    /// name against <c>GET /api/species/metadata</c>. Empty when no matching section metadata exists.</summary>
    public IReadOnlyList<EditSpeciesQuestionView> Questions { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        CurrentSection = EditSpeciesSectionCatalog.GetByKeyOrDefault(Section);
        var sections = EditSpeciesSectionCatalog.Sections;
        var currentIndex = sections.ToList().IndexOf(CurrentSection);
        PreviousSection = currentIndex > 0 ? sections[currentIndex - 1] : null;
        NextSection = currentIndex < sections.Count - 1 ? sections[currentIndex + 1] : null;

        try
        {
            var answerData = await speciesApiService.GetSpeciesAnswerDataAsync(SpeciesId, cancellationToken);

            if (answerData is null)
            {
                return NotFound();
            }

            SpeciesName = answerData.SpeciesName;

            var metadata = await speciesApiService.GetSpeciesMetadataAsync(cancellationToken);
            var matchedSection = metadata.Sections.FirstOrDefault(
                section => string.Equals(section.Name, CurrentSection.Title, StringComparison.OrdinalIgnoreCase));

            if (matchedSection is null)
            {
                logger.SpeciesSectionMetadataNotFound(CurrentSection.Title);
                Questions = [];
            }
            else
            {
                var answeredFields = answerData.Sections
                    .FirstOrDefault(section => section.SectionId == matchedSection.Id)?
                    .FieldValues ?? [];
                var fieldValuesByQuestion = answeredFields.ToLookup(value => value.QuestionId);

                // "List" type fields (ReferenceTableId set) render as real checkbox options rather
                // than plain text, so fetch every distinct reference table used by this section once.
                var referenceTableIds = matchedSection.Questions
                    .SelectMany(question => question.Fields)
                    .Select(field => field.ReferenceTableId)
                    .Where(id => id != Guid.Empty)
                    .Distinct()
                    .ToList();

                var referenceValuesByTable = new Dictionary<Guid, IReadOnlyList<ReferenceValueDto>>();
                foreach (var referenceTableId in referenceTableIds)
                {
                    referenceValuesByTable[referenceTableId] =
                        await speciesApiService.GetReferenceValuesAsync(referenceTableId, cancellationToken);
                }

                Questions =
                [
                    .. matchedSection.Questions.Select(question => new EditSpeciesQuestionView(
                        $"{matchedSection.SectionNumber}.{question.QuestionNumber}",
                        question.Name,
                        [
                            .. question.Fields.Select(field => BuildFieldView(
                                field,
                                fieldValuesByQuestion[question.Id].Where(value => value.FieldNumber == field.FieldNumber).ToList(),
                                referenceValuesByTable))
                        ]))
                ];
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.SpeciesDetailLoadFailed(exception, SpeciesId);
            HasError = true;
        }

        return Page();
    }

    /// <summary>Builds the querystring URL for a left-nav/pagination link to a different section of this species.</summary>
    public string? BuildSectionUrl(string sectionKey) =>
        Url.Page("/EditSpecies", new { speciesId = SpeciesId, section = sectionKey });

    /// <summary>Builds one field's view: a checkbox group for a "List" type field (its options come
    /// from the field's reference table, checked against the recorded <see cref="SpeciesFieldValueDto.ListValue"/>
    /// rows), or a plain-text value for every other field type.</summary>
    private static EditSpeciesFieldView BuildFieldView(
        SpeciesFieldMetadataDto field,
        IReadOnlyList<SpeciesFieldValueDto> recordedValues,
        Dictionary<Guid, IReadOnlyList<ReferenceValueDto>> referenceValuesByTable)
    {
        if (field.ReferenceTableId == Guid.Empty || !referenceValuesByTable.TryGetValue(field.ReferenceTableId, out var options))
        {
            return new EditSpeciesFieldView(field.Name, FormatFieldValues(recordedValues), []);
        }

        var selectedValues = recordedValues.Where(value => value.ListValue is not null).Select(value => value.ListValue!.Value).ToHashSet();

        return new EditSpeciesFieldView(
            field.Name,
            null,
            [.. options.Select(option => new EditSpeciesFieldOptionView(option.Value, selectedValues.Contains(option.Id)))]);
    }

    /// <summary>Formats every recorded value for one field. More than one row means a multi-select
    /// list answer; zero rows means the field has not been answered.</summary>
    private static string FormatFieldValues(IEnumerable<SpeciesFieldValueDto> values)
    {
        var list = values.ToList();

        return list.Count == 0 ? "Not answered" : string.Join(", ", list.Select(FormatSingleFieldValue));
    }

    /// <summary>Formats one recorded value for a non-list field.</summary>
    private static string FormatSingleFieldValue(SpeciesFieldValueDto value)
    {
        if (value.BooleanValue is { } booleanValue)
        {
            return booleanValue ? "Yes" : "No";
        }

        if (!string.IsNullOrWhiteSpace(value.TextValue))
        {
            return value.TextValue;
        }

        return "Not answered";
    }
}


/// <summary>Source-generated structured log messages for <see cref="EditSpeciesModel"/>.</summary>
internal static partial class EditSpeciesLog
{
    [LoggerMessage(EventId = 2100, Level = LogLevel.Error, Message = "Failed to load species '{SpeciesId}' detail from CDC.Api")]
    public static partial void SpeciesDetailLoadFailed(this ILogger logger, Exception exception, Guid speciesId);

    [LoggerMessage(EventId = 2101, Level = LogLevel.Warning, Message = "No species questionnaire section metadata found matching '{SectionTitle}'")]
    public static partial void SpeciesSectionMetadataNotFound(this ILogger logger, string sectionTitle);
}
