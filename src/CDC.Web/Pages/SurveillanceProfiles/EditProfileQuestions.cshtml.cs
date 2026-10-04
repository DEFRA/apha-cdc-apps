using System.Globalization;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// Displays the profile reference sections and their real questions/answers for a profile's
/// current version, loaded entirely from CDC.Api. Replaces the legacy
/// <c>EditProfileQuestions.aspx</c> page in its read-only ("browse profile") mode.
/// </summary>
/// <param name="apiClient">Typed client for the profile management endpoints on CDC.Api.</param>
/// <param name="profileSectionsApiService">Typed client for the profile sections endpoints on CDC.Api.</param>
/// <param name="logger">Structured logger.</param>
public class EditProfileQuestionsModel(
    IApiClient apiClient,
    IProfileSectionsApiService profileSectionsApiService,
    ILogger<EditProfileQuestionsModel> logger) : PageModel
{
    /// <summary>Gets or sets the profile being browsed, bound from the page route.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid ProfileId { get; set; }

    /// <summary>Gets or sets the selected left-nav section id, bound from the querystring.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid? Section { get; set; }

    /// <summary>Gets the profile's full display title (with its scenario title in brackets, for
    /// a "what-if" scenario), once loaded. Matches the legacy <c>ProfileVersionInfo.FullTitle</c>.</summary>
    public string? ProfileTitle { get; private set; }

    /// <summary>Gets a value indicating whether the profile failed to load.</summary>
    public bool HasError { get; private set; }

    /// <summary>Gets a value indicating whether the profile has no version to browse.</summary>
    public bool HasNoVersion { get; private set; }

    /// <summary>Gets the 16 profile reference sections, in section-number order, read directly
    /// from <c>GET /api/profile-sections/metadata</c> (never hardcoded).</summary>
    public IReadOnlyList<ProfileSectionMetadataDto> Sections { get; private set; } = [];

    public ProfileSectionMetadataDto? CurrentSection { get; private set; }

    public ProfileSectionMetadataDto? PreviousSection { get; private set; }

    public ProfileSectionMetadataDto? NextSection { get; private set; }

    /// <summary>Gets the real questions and recorded answers for <see cref="CurrentSection"/>.</summary>
    public IReadOnlyList<AccordionQuestionView> Questions { get; private set; } = [];

    /// <summary>Gets the current section's scientific paper references, for the References tab.</summary>
    public ProfileNoteGroupView ScientificPaperReferences { get; private set; } = EmptyNoteGroup("Scientific paper references");

    /// <summary>Gets the current section's legislative references, for the References tab.</summary>
    public ProfileNoteGroupView LegislativeReferences { get; private set; } = EmptyNoteGroup("Legislative references");

    /// <summary>Gets the current section's sources of further information, for the Further
    /// information tab.</summary>
    public ProfileNoteGroupView FurtherInformation { get; private set; } = EmptyNoteGroup("Sources of further information");

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var profileVersionId = Guid.Empty;

        try
        {
            var profile = await apiClient.GetManageProfileAsync(ProfileId, cancellationToken);

            if (profile is null)
            {
                return NotFound();
            }

            ProfileTitle = profile.FullTitle;

            var metadata = await profileSectionsApiService.GetProfileQuestionnaireMetadataAsync(cancellationToken);
            Sections = metadata.Sections;

            if (Sections.Count == 0)
            {
                return Page();
            }

            CurrentSection = (Section.HasValue ? Sections.FirstOrDefault(s => s.Id == Section.Value) : null) ?? Sections[0];
            var currentIndex = Sections.ToList().IndexOf(CurrentSection);
            PreviousSection = currentIndex > 0 ? Sections[currentIndex - 1] : null;
            NextSection = currentIndex < Sections.Count - 1 ? Sections[currentIndex + 1] : null;

            if (profile.CurrentProfileVersionId == Guid.Empty)
            {
                HasNoVersion = true;
                return Page();
            }

            profileVersionId = profile.CurrentProfileVersionId;
            await LoadQuestionsAsync(profileVersionId, CurrentSection, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.ProfileQuestionsLoadFailed(exception, ProfileId);
            HasError = true;
        }

        // Kept outside the Questions try/catch above: the References and Further information
        // tabs are a separate data source, so a failure there must not blank out the Questions
        // tab that already loaded successfully.
        if (!HasError && !HasNoVersion && CurrentSection is not null)
        {
            try
            {
                await LoadNotesAsync(profileVersionId, CurrentSection, cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
            {
                logger.ProfileNotesLoadFailed(exception, ProfileId);
            }
        }

        return Page();
    }

    /// <summary>Builds the querystring URL for a left-nav/pagination link to a different section of this profile.</summary>
    public string? BuildSectionUrl(Guid sectionId) =>
        Url.Page("/SurveillanceProfiles/EditProfileQuestions", new { profileId = ProfileId, section = sectionId });

    /// <summary>Loads the current section's References and Further information tab content.
    /// Resolves each note type by name rather than a hardcoded id, since note types are reference
    /// data (<c>GET /api/profile-notes/types</c>); a missing type degrades to an empty group
    /// rather than failing the whole page.</summary>
    private async Task LoadNotesAsync(Guid profileVersionId, ProfileSectionMetadataDto section, CancellationToken cancellationToken)
    {
        var noteTypes = await profileSectionsApiService.GetProfileNoteTypesAsync(cancellationToken);

        var questionNumbersById = Sections
            .SelectMany(s => s.Questions.Select(question => (question.Id, Display: $"{s.SectionNumber}.{question.QuestionNumber}")))
            .ToDictionary(pair => pair.Id, pair => pair.Display);

        ScientificPaperReferences = await LoadNoteGroupAsync(
            noteTypes, "SCIENTIFICPAPERREFERENCE", "Scientific paper references",
            profileVersionId, section.Id, questionNumbersById, cancellationToken);

        LegislativeReferences = await LoadNoteGroupAsync(
            noteTypes, "LEGISLATIVEREFERENCE", "Legislative references",
            profileVersionId, section.Id, questionNumbersById, cancellationToken);

        FurtherInformation = await LoadNoteGroupAsync(
            noteTypes, "SOURCEOFFURTHERINFORMATION", "Sources of further information",
            profileVersionId, section.Id, questionNumbersById, cancellationToken);
    }

    private async Task<ProfileNoteGroupView> LoadNoteGroupAsync(
        IReadOnlyList<ProfileNoteTypeDto> noteTypes,
        string normalisedNoteTypeName,
        string fallbackHeading,
        Guid profileVersionId,
        Guid profileSectionId,
        IReadOnlyDictionary<Guid, string> questionNumbersById,
        CancellationToken cancellationToken)
    {
        var noteType = noteTypes.FirstOrDefault(type => NormaliseNoteTypeName(type.Name) == normalisedNoteTypeName);

        if (noteType is null)
        {
            return EmptyNoteGroup(fallbackHeading);
        }

        var notes = await profileSectionsApiService.GetProfileNotesBySectionAsync(
            profileVersionId, profileSectionId, noteType.Id, cancellationToken);

        var heading = string.IsNullOrEmpty(noteType.PluralName) ? fallbackHeading : noteType.PluralName;

        var rows = notes
            .OrderBy(note => ProfileTitleHtmlFormatter.ToPlainText(note.NoteText), StringComparer.OrdinalIgnoreCase)
            .Select(note => new ProfileNoteRowView(
                note.NoteText,
                string.Join(", ", note.QuestionReferences
                    .Select(reference => questionNumbersById.GetValueOrDefault(reference.ProfileQuestionId))
                    .Where(number => !string.IsNullOrEmpty(number)))))
            .ToList();

        return new ProfileNoteGroupView(heading, BuildEmptyMessage(heading), rows);
    }

    /// <summary>Strips everything but letters so a reference-data display name (however spaced or
    /// cased) can be matched against the legacy note type's PascalCase identifier.</summary>
    private static string NormaliseNoteTypeName(string name) =>
        new string([.. name.Where(char.IsLetter)]).ToUpperInvariant();

    private static ProfileNoteGroupView EmptyNoteGroup(string heading) => new(heading, BuildEmptyMessage(heading), []);

    private static string BuildEmptyMessage(string heading) => $"There are no {heading.ToLowerInvariant()} to display.";

    private async Task LoadQuestionsAsync(Guid profileVersionId, ProfileSectionMetadataDto section, CancellationToken cancellationToken)
    {
        var answers = await profileSectionsApiService.GetProfileSectionAnswersAsync(profileVersionId, section.Id, cancellationToken);

        var namesByQuestion = answers.QuestionNames.ToDictionary(name => name.Id);
        var valuesByQuestion = answers.FieldValues.ToLookup(value => value.QuestionId);

        // "List"/"MultiValueList" fields render as real checkbox options rather than plain text,
        // so fetch every distinct reference table used by this section once.
        var referenceTableIds = section.Questions
            .SelectMany(question => question.Fields)
            .Select(field => field.ReferenceTableId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var referenceValuesByTable = new Dictionary<Guid, IReadOnlyList<ReferenceValueDto>>();
        foreach (var referenceTableId in referenceTableIds)
        {
            referenceValuesByTable[referenceTableId] =
                await profileSectionsApiService.GetReferenceValuesAsync(referenceTableId, cancellationToken);
        }

        Questions =
        [
            .. section.Questions.Select(question => new AccordionQuestionView(
                $"{section.SectionNumber}.{question.QuestionNumber}",
                namesByQuestion.TryGetValue(question.Id, out var name) ? name.Name : question.ShortName,
                [
                    // "Field Group" entries are containers for a repeating question's nested
                    // fields, not values themselves, so they have nothing to render on their own.
                    .. question.Fields
                        .Where(field => !string.Equals(field.DataTypeName, "Field Group", StringComparison.OrdinalIgnoreCase))
                        .Select(field => BuildFieldView(
                            field,
                            valuesByQuestion[question.Id].Where(value => value.FieldNumber == field.FieldNumber).ToList(),
                            referenceValuesByTable))
                ]))
        ];
    }

    /// <summary>Builds one field's view: a checkbox group for a "List"/"MultiValueList" type field
    /// (its options come from the field's reference table, checked against the recorded
    /// <see cref="ProfileFieldValueDto.ListValue"/> rows), rich/HTML content for a "Long Text"
    /// field, or a plain-text value for every other field type.</summary>
    private static AccordionFieldView BuildFieldView(
        ProfileFieldMetadataDto field,
        IReadOnlyList<ProfileFieldValueDto> recordedValues,
        Dictionary<Guid, IReadOnlyList<ReferenceValueDto>> referenceValuesByTable)
    {
        var isListType =
            string.Equals(field.DataTypeName, "List", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(field.DataTypeName, "MultiValueList", StringComparison.OrdinalIgnoreCase);

        if (isListType && field.ReferenceTableId != Guid.Empty && referenceValuesByTable.TryGetValue(field.ReferenceTableId, out var options))
        {
            var selectedValues = recordedValues
                .Where(value => value.ListValue is not null)
                .Select(value => value.ListValue!.Value)
                .ToHashSet();

            return new AccordionFieldView(
                field.Name,
                null,
                false,
                [.. options.Select(option => new AccordionFieldOptionView(option.Value, selectedValues.Contains(option.Id)))]);
        }

        var isHtml = string.Equals(field.DataTypeName, "Long Text", StringComparison.OrdinalIgnoreCase);

        return new AccordionFieldView(field.Name, FormatFieldValues(recordedValues), isHtml, []);
    }

    /// <summary>Formats every recorded value for one field. More than one row means a multi-select
    /// list answer; zero rows means the field has not been answered.</summary>
    private static string FormatFieldValues(IEnumerable<ProfileFieldValueDto> values)
    {
        var list = values.ToList();

        return list.Count == 0 ? "Not answered" : string.Join(", ", list.Select(FormatSingleFieldValue));
    }

    /// <summary>Formats one recorded value for a non-list field.</summary>
    private static string FormatSingleFieldValue(ProfileFieldValueDto value)
    {
        if (value.BooleanValue is { } booleanValue)
        {
            return booleanValue ? "Yes" : "No";
        }

        if (value.DecimalValue is { } decimalValue)
        {
            return decimalValue.ToString("0.##", CultureInfo.InvariantCulture);
        }

        if (value.DateValue is { } dateValue)
        {
            return dateValue.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(value.TextValue))
        {
            return value.TextValue;
        }

        return "Not answered";
    }
}

/// <summary>Source-generated structured log messages for <see cref="EditProfileQuestionsModel"/>.</summary>
internal static partial class EditProfileQuestionsLog
{
    [LoggerMessage(EventId = 2300, Level = LogLevel.Error, Message = "Failed to load profile '{ProfileId}' questions from CDC.Api")]
    public static partial void ProfileQuestionsLoadFailed(this ILogger logger, Exception exception, Guid profileId);

    [LoggerMessage(EventId = 2301, Level = LogLevel.Error, Message = "Failed to load profile '{ProfileId}' references/further information from CDC.Api")]
    public static partial void ProfileNotesLoadFailed(this ILogger logger, Exception exception, Guid profileId);
}
