using System.Globalization;
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

    /// <summary>Gets the profile's current display title, once loaded.</summary>
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
    public IReadOnlyList<EditProfileQuestionsQuestionView> Questions { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            var profile = await apiClient.GetManageProfileAsync(ProfileId, cancellationToken);

            if (profile is null)
            {
                return NotFound();
            }

            ProfileTitle = profile.ProfileTitle;

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

            await LoadQuestionsAsync(profile.CurrentProfileVersionId, CurrentSection, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.ProfileQuestionsLoadFailed(exception, ProfileId);
            HasError = true;
        }

        return Page();
    }

    /// <summary>Builds the querystring URL for a left-nav/pagination link to a different section of this profile.</summary>
    public string? BuildSectionUrl(Guid sectionId) =>
        Url.Page("/SurveillanceProfiles/EditProfileQuestions", new { profileId = ProfileId, section = sectionId });

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
            .. section.Questions.Select(question => new EditProfileQuestionsQuestionView(
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
    private static EditProfileQuestionsFieldView BuildFieldView(
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

            return new EditProfileQuestionsFieldView(
                field.Name,
                null,
                false,
                [.. options.Select(option => new EditProfileQuestionsFieldOptionView(option.Value, selectedValues.Contains(option.Id)))]);
        }

        var isHtml = string.Equals(field.DataTypeName, "Long Text", StringComparison.OrdinalIgnoreCase);

        return new EditProfileQuestionsFieldView(field.Name, FormatFieldValues(recordedValues), isHtml, []);
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
}
