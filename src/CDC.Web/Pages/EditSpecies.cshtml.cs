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

    /// <summary>Gets or sets a value indicating whether the page is in the "Edit data" flow from
    /// Maintain species data, rather than the read-only "View species data" flow.</summary>
    [BindProperty(SupportsGet = true)]
    public bool Edit { get; set; }

    /// <summary>Set by the redirect after a successful save, to show the confirmation banner.</summary>
    [BindProperty(SupportsGet = true)]
    public bool Saved { get; set; }

    /// <summary>Gets or sets the row version read when the edit form was displayed, posted back on save.</summary>
    [BindProperty]
    public string? LastUpdatedBase64 { get; set; }

    /// <summary>Gets the species' current display name, once loaded.</summary>
    public string? SpeciesName { get; private set; }

    /// <summary>Gets a value indicating whether the species failed to load.</summary>
    public bool HasError { get; private set; }

    /// <summary>Gets a confirmation message shown after a successful save.</summary>
    public string? SuccessMessage { get; private set; }

    /// <summary>Gets the message to show when a save attempt fails.</summary>
    public string? SaveErrorMessage { get; private set; }

    public EditSpeciesSection CurrentSection { get; private set; } = EditSpeciesSectionCatalog.Sections[0];

    public EditSpeciesSection? PreviousSection { get; private set; }

    public EditSpeciesSection? NextSection { get; private set; }

    /// <summary>Gets the row version last read for this species, needed to post a save.</summary>
    public byte[] AnswerDataLastUpdated { get; private set; } = [];

    /// <summary>Gets the real questions and recorded answers for <see cref="CurrentSection"/>, matched by
    /// name against <c>GET /api/species/metadata</c>. Empty when no matching section metadata exists.</summary>
    public IReadOnlyList<AccordionQuestionView> Questions { get; private set; } = [];

    /// <summary>Gets the editable field views for <see cref="CurrentSection"/>, used by the "Edit data" form.</summary>
    public IReadOnlyList<EditableQuestionView> EditableQuestions { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        SetCurrentSection();

        var loadResult = await LoadQuestionsAsync(cancellationToken);

        if (loadResult is not null)
        {
            return loadResult;
        }

        if (Saved)
        {
            SuccessMessage = "The species data was updated.";
        }

        return Page();
    }

    /// <summary>Validates and saves the question responses posted for <see cref="CurrentSection"/>.</summary>
    /// <param name="cancellationToken">Cancels the request if the client disconnects.</param>
    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        Edit = true;
        SetCurrentSection();

        byte[] lastUpdated;
        try
        {
            lastUpdated = Convert.FromBase64String(LastUpdatedBase64 ?? string.Empty);
        }
        catch (FormatException)
        {
            SaveErrorMessage = "The species data could not be saved. Reload and try again.";
            return await LoadQuestionsAsync(cancellationToken) ?? Page();
        }

        var metadata = await speciesApiService.GetSpeciesMetadataAsync(cancellationToken);
        var matchedSection = metadata.Sections.FirstOrDefault(
            section => string.Equals(section.Name, CurrentSection.Title, StringComparison.OrdinalIgnoreCase));

        var changes = matchedSection?.Questions
            .SelectMany(question => question.Fields)
            .Select(BuildChange)
            .ToList() ?? [];

        if (changes.Count == 0)
        {
            return await LoadQuestionsAsync(cancellationToken) ?? Page();
        }

        var result = await speciesApiService.UpdateSpeciesAnswerDataAsync(
            new UpdateSpeciesAnswerDataRequestDto { SpeciesId = SpeciesId, LastUpdated = lastUpdated, Changes = changes },
            cancellationToken);

        if (result.Outcome != SpeciesUpdateOutcome.Success)
        {
            logger.SpeciesAnswerSaveFailed(SpeciesId, result.Outcome);
            SaveErrorMessage = result.ErrorMessage ?? "We could not save this change. Try again later.";
            return await LoadQuestionsAsync(cancellationToken) ?? Page();
        }

        logger.SpeciesAnswerSaved(SpeciesId);

        // Post-redirect-get: reloads the section with the updated answers and shows the
        // confirmation banner without resubmitting the form on refresh.
        return RedirectToPage(new { speciesId = SpeciesId, section = CurrentSection.Key, edit = true, saved = true });
    }

    private void SetCurrentSection()
    {
        CurrentSection = EditSpeciesSectionCatalog.GetByKeyOrDefault(Section);
        var sections = EditSpeciesSectionCatalog.Sections;
        var currentIndex = sections.ToList().IndexOf(CurrentSection);
        PreviousSection = currentIndex > 0 ? sections[currentIndex - 1] : null;
        NextSection = currentIndex < sections.Count - 1 ? sections[currentIndex + 1] : null;
    }

    /// <summary>Loads the species and matches <see cref="CurrentSection"/> against real questionnaire
    /// metadata, building both the read-only and editable field views. Returns a terminating result
    /// (not-found or the error page) if loading failed; otherwise <see langword="null"/>.</summary>
    private async Task<IActionResult?> LoadQuestionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var answerData = await speciesApiService.GetSpeciesAnswerDataAsync(SpeciesId, cancellationToken);

            if (answerData is null)
            {
                return NotFound();
            }

            SpeciesName = answerData.SpeciesName;
            AnswerDataLastUpdated = answerData.LastUpdated;

            var metadata = await speciesApiService.GetSpeciesMetadataAsync(cancellationToken);
            var matchedSection = metadata.Sections.FirstOrDefault(
                section => string.Equals(section.Name, CurrentSection.Title, StringComparison.OrdinalIgnoreCase));

            if (matchedSection is null)
            {
                logger.SpeciesSectionMetadataNotFound(CurrentSection.Title);
                Questions = [];
                EditableQuestions = [];

                return null;
            }

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

            var questions = new List<AccordionQuestionView>();
            var editableQuestions = new List<EditableQuestionView>();

            foreach (var question in matchedSection.Questions)
            {
                var number = $"{matchedSection.SectionNumber}.{question.QuestionNumber}";
                var accordionFields = new List<AccordionFieldView>();
                var editableFields = new List<EditableFieldView>();

                foreach (var field in question.Fields)
                {
                    var recordedValues = fieldValuesByQuestion[question.Id]
                        .Where(value => value.FieldNumber == field.FieldNumber)
                        .ToList();

                    accordionFields.Add(BuildFieldView(field, recordedValues, referenceValuesByTable));
                    editableFields.Add(BuildEditableFieldView(field, recordedValues, referenceValuesByTable));
                }

                questions.Add(new AccordionQuestionView(number, question.Name, accordionFields));
                editableQuestions.Add(new EditableQuestionView(number, question.Name, editableFields));
            }

            Questions = questions;
            EditableQuestions = editableQuestions;

            return null;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.SpeciesDetailLoadFailed(exception, SpeciesId);
            HasError = true;

            return null;
        }
    }

    /// <summary>Builds the querystring URL for a left-nav/pagination link to a different section of this species.</summary>
    public string? BuildSectionUrl(string sectionKey) =>
        Url.Page("/EditSpecies", new { speciesId = SpeciesId, section = sectionKey, edit = Edit ? true : (bool?)null });

    /// <summary>Builds one field's editable view: checkbox options for a "MultiValueList" field, radio
    /// options for a "List" field, a boolean yes/no for a "Boolean" field, or plain text otherwise.</summary>
    private static EditableFieldView BuildEditableFieldView(
        SpeciesFieldMetadataDto field,
        IReadOnlyList<SpeciesFieldValueDto> recordedValues,
        Dictionary<Guid, IReadOnlyList<ReferenceValueDto>> referenceValuesByTable)
    {
        if (referenceValuesByTable.TryGetValue(field.ReferenceTableId, out var options))
        {
            var selectedValues = recordedValues.Where(value => value.ListValue is not null).Select(value => value.ListValue!.Value).ToHashSet();

            return new EditableFieldView(
                field.Id,
                field.Name,
                field.DataTypeName,
                null,
                null,
                [.. options.Select(option => new EditableFieldOptionView(option.Id, option.Value, selectedValues.Contains(option.Id)))]);
        }

        if (string.Equals(field.DataTypeName, "Boolean", StringComparison.OrdinalIgnoreCase))
        {
            var recordedBoolean = recordedValues.Select(value => value.BooleanValue).FirstOrDefault(value => value is not null);

            return new EditableFieldView(field.Id, field.Name, field.DataTypeName, recordedBoolean, null, []);
        }

        var recordedText = recordedValues.Select(value => value.TextValue).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        return new EditableFieldView(field.Id, field.Name, field.DataTypeName, null, recordedText, []);
    }

    /// <summary>Reads the posted value for one field, by the <c>field_{fieldId}</c> naming convention
    /// used by the "Edit data" form, and builds the corresponding API change.</summary>
    private SpeciesFieldValueChangeDto BuildChange(SpeciesFieldMetadataDto field)
    {
        var formKey = $"field_{field.Id}";

        if (string.Equals(field.DataTypeName, "MultiValueList", StringComparison.OrdinalIgnoreCase))
        {
            var selected = Request.Form[formKey].Where(value => !string.IsNullOrEmpty(value)).Select(value => Guid.Parse(value!)).ToList();

            return new SpeciesFieldValueChangeDto { FieldId = field.Id, Kind = SpeciesFieldValueKind.MultiValue, MultiValues = selected };
        }

        if (string.Equals(field.DataTypeName, "List", StringComparison.OrdinalIgnoreCase))
        {
            var raw = Request.Form[formKey].ToString();

            return new SpeciesFieldValueChangeDto
            {
                FieldId = field.Id,
                Kind = string.IsNullOrEmpty(raw) ? SpeciesFieldValueKind.None : SpeciesFieldValueKind.List,
                ListValue = string.IsNullOrEmpty(raw) ? null : Guid.Parse(raw)
            };
        }

        if (string.Equals(field.DataTypeName, "Boolean", StringComparison.OrdinalIgnoreCase))
        {
            var raw = Request.Form[formKey].ToString();

            return new SpeciesFieldValueChangeDto
            {
                FieldId = field.Id,
                Kind = string.IsNullOrEmpty(raw) ? SpeciesFieldValueKind.None : SpeciesFieldValueKind.Boolean,
                BooleanValue = raw == "true"
            };
        }

        var text = Request.Form[formKey].ToString();

        return new SpeciesFieldValueChangeDto
        {
            FieldId = field.Id,
            Kind = string.IsNullOrWhiteSpace(text) ? SpeciesFieldValueKind.None : SpeciesFieldValueKind.Text,
            TextValue = string.IsNullOrWhiteSpace(text) ? null : text
        };
    }

    /// <summary>Builds one field's read-only view: a checkbox group for a "List" type field (its options
    /// come from the field's reference table, checked against the recorded
    /// <see cref="SpeciesFieldValueDto.ListValue"/> rows), or a plain-text value for every other field type.</summary>
    private static AccordionFieldView BuildFieldView(
        SpeciesFieldMetadataDto field,
        IReadOnlyList<SpeciesFieldValueDto> recordedValues,
        Dictionary<Guid, IReadOnlyList<ReferenceValueDto>> referenceValuesByTable)
    {
        if (field.ReferenceTableId == Guid.Empty || !referenceValuesByTable.TryGetValue(field.ReferenceTableId, out var options))
        {
            return new AccordionFieldView(field.Name, FormatFieldValues(recordedValues), false, []);
        }

        var selectedValues = recordedValues.Where(value => value.ListValue is not null).Select(value => value.ListValue!.Value).ToHashSet();

        return new AccordionFieldView(
            field.Name,
            null,
            false,
            [.. options.Select(option => new AccordionFieldOptionView(option.Value, selectedValues.Contains(option.Id)))]);
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

    [LoggerMessage(EventId = 2102, Level = LogLevel.Warning, Message = "Failed to save answer data for species {SpeciesId}: {Outcome}")]
    public static partial void SpeciesAnswerSaveFailed(this ILogger logger, Guid speciesId, SpeciesUpdateOutcome outcome);

    [LoggerMessage(EventId = 2103, Level = LogLevel.Information, Message = "Saved answer data for species {SpeciesId}")]
    public static partial void SpeciesAnswerSaved(this ILogger logger, Guid speciesId);
}
