using System.Globalization;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// The first tab of a profile section: its questions and recorded answers for the profile's current version,
/// loaded entirely from CDC.Api. Replaces the legacy <c>EditProfileQuestions.aspx</c>. The other tabs are their own
/// pages (<see cref="EditProfileReferencesModel"/>, <see cref="EditProfileFurtherInformationModel"/>,
/// <see cref="EditProfileReviewCommentsModel"/>, <see cref="EditProfileAllRevisionDatesModel"/> and
/// <see cref="EditProfileContributionsModel"/>), reached through the shared tab strip.
/// </summary>
/// <param name="apiClient">Typed client for the profile management endpoints on CDC.Api.</param>
/// <param name="profileSectionsApiService">Typed client for the profile sections endpoints on CDC.Api.</param>
/// <param name="logger">Structured logger.</param>
public class EditProfileQuestionsModel(
    IApiClient apiClient,
    IProfileSectionsApiService profileSectionsApiService,
    ILogger<EditProfileQuestionsModel> logger) : ProfileSectionTabPageModel(apiClient, profileSectionsApiService, logger)
{
    /// <inheritdoc />
    public override ProfileSectionTab ActiveTab => ProfileSectionTab.Questions;

    /// <summary>Gets the mandatory questions with no recorded answer in the current section, from
    /// the most recent "Validate" button submission. Empty until Validate has been used.</summary>
    public IReadOnlyList<string> UnansweredQuestions { get; private set; } = [];

    /// <summary>Gets a value indicating whether "Validate" has been run for the current section
    /// during this request.</summary>
    public bool HasValidated { get; private set; }

    /// <summary>Gets the number of fields cleared by the most recent "Clear" button submission,
    /// or <see langword="null"/> until Clear has been used.</summary>
    public int? FieldsClearedCount { get; private set; }

    /// <summary>Gets the number of fields saved by the most recent "Save" button submission, or
    /// <see langword="null"/> until Save has been used.</summary>
    public int? FieldsSavedCount { get; private set; }

    /// <summary>Gets the field-level validation errors (for example, an invalid date or decimal)
    /// from the most recent "Save" button submission. Empty when Save succeeded or has not been
    /// used; when non-empty, the submitted answers were not sent to CDC.Api.</summary>
    public IReadOnlyList<string> SaveValidationErrors { get; private set; } = [];

    /// <summary>Gets the real questions and recorded answers for the selected section, each field either
    /// rendered as a real editable control or, for a per-species/repeating question, as a static read-only value.</summary>
    public IReadOnlyList<ProfileQuestionEditView> Questions { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var notFound = await LoadProfileContextAsync(cancellationToken);

        if (notFound is not null)
        {
            return notFound;
        }

        if (IsReady)
        {
            try
            {
                await LoadQuestionsAsync(CurrentProfileVersionId, CurrentSection!, cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
            {
                Logger.ProfileQuestionsLoadFailed(exception, ProfileId);
                HasError = true;
            }
        }

        return Page();
    }

    /// <summary>
    /// Handles the "Validate" button: checks the current section's mandatory fields, matching the
    /// legacy <c>ProfileVersionSection.Validate()</c> check, and re-renders the page with the
    /// result.
    /// </summary>
    /// <param name="profileVersionId">The profile version to validate, posted from a hidden field.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The page, with the validation result populated.</returns>
    public async Task<IActionResult> OnPostValidateSectionAsync(Guid profileVersionId, CancellationToken cancellationToken)
    {
        await OnGetAsync(cancellationToken);

        if (!IsReady)
        {
            return Page();
        }

        try
        {
            var result = await ProfileSectionsApi.ValidateProfileVersionSectionAsync(
                profileVersionId, CurrentSection!.Id, cancellationToken);
            UnansweredQuestions = result.UnansweredQuestions;
            HasValidated = true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            Logger.ProfileSectionValidateFailed(exception, ProfileId, CurrentSection!.Id);
            HasError = true;
        }

        return Page();
    }

    /// <summary>
    /// Handles the "Clear" button: clears every recorded answer in the current section, matching
    /// the legacy <c>ProfileVersionSection.Clear()</c> + <c>Save()</c> pathway, then re-renders
    /// the page with the refreshed (now empty) answers.
    /// </summary>
    /// <param name="profileVersionId">The profile version to clear, posted from a hidden field.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The page, with the section's answers cleared.</returns>
    public async Task<IActionResult> OnPostClearSectionAsync(Guid profileVersionId, CancellationToken cancellationToken)
    {
        await OnGetAsync(cancellationToken);

        if (!IsReady)
        {
            return Page();
        }

        try
        {
            var result = await ProfileSectionsApi.ClearProfileVersionSectionAsync(
                profileVersionId, CurrentSection!.Id, cancellationToken);
            FieldsClearedCount = result.FieldsClearedCount;

            // Re-load so the page reflects the now-cleared answers rather than the stale ones
            // already fetched by OnGetAsync above.
            await LoadQuestionsAsync(profileVersionId, CurrentSection, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            Logger.ProfileSectionClearFailed(exception, ProfileId, CurrentSection!.Id);
            HasError = true;
        }

        return Page();
    }

    /// <summary>
    /// Handles the "Save" button: writes every submitted field answer for a standard
    /// (non-per-species, non-repeating) question in the current section, matching the legacy
    /// <c>IProfileField.Save()</c> pathway, then re-renders the page with the saved answers.
    /// Fields with an invalid decimal or date are reported back without calling CDC.Api.
    /// </summary>
    /// <param name="profileVersionId">The profile version being answered, posted from a hidden field.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The page, with the section's answers saved or the submission's validation errors.</returns>
    public async Task<IActionResult> OnPostSaveSectionAsync(Guid profileVersionId, CancellationToken cancellationToken)
    {
        await OnGetAsync(cancellationToken);

        if (IsReady)
        {
            await TrySaveSectionAsync(profileVersionId, cancellationToken);
        }

        return Page();
    }

    /// <summary>
    /// Handles a click on a tab. Legacy <c>tabStrip_TabClick</c> persists the section's answers before opening the
    /// tab's page and stays put when they are invalid, so an edit is never lost by switching tab.
    /// </summary>
    /// <param name="profileVersionId">The profile version being answered, posted from a hidden field.</param>
    /// <param name="tab">The name of the tab clicked.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A redirect to the tab's page, or this page again when the answers could not be saved.</returns>
    public async Task<IActionResult> OnPostNavigateTabAsync(Guid profileVersionId, string? tab, CancellationToken cancellationToken)
    {
        await OnGetAsync(cancellationToken);

        if (!IsReady
            || !Enum.TryParse<ProfileSectionTab>(tab, ignoreCase: true, out var target)
            || !ProfileSectionTabs.VisibleTabs(CanViewReviewComments, CanViewAllRevisionDates, CanViewContributions).Contains(target))
        {
            return Page();
        }

        if (CanEditProfileSection && !await TrySaveSectionAsync(profileVersionId, cancellationToken))
        {
            return Page();
        }

        return RedirectToPage(ProfileSectionTabs.PagePath(target), new { profileId = ProfileId, section = CurrentSection!.Id });
    }

    /// <summary>Saves the submitted answers and reloads the page's answers; reports a rejected value or a CDC.Api
    /// failure on the page.</summary>
    /// <returns><see langword="true"/> when the answers were saved.</returns>
    private async Task<bool> TrySaveSectionAsync(Guid profileVersionId, CancellationToken cancellationToken)
    {
        var (answers, validationErrors) = ParseSubmittedAnswers(CurrentSection!);

        if (validationErrors.Count > 0)
        {
            SaveValidationErrors = validationErrors;
            return false;
        }

        try
        {
            var result = await ProfileSectionsApi.SaveProfileVersionSectionAsync(
                profileVersionId, CurrentSection!.Id, answers, cancellationToken);
            FieldsSavedCount = result.FieldsSavedCount;

            // Re-load so the page reflects the newly-saved answers rather than the stale ones
            // already fetched by OnGetAsync above.
            await LoadQuestionsAsync(profileVersionId, CurrentSection, cancellationToken);

            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            Logger.ProfileSectionSaveFailed(exception, ProfileId, CurrentSection!.Id);
            HasError = true;

            return false;
        }
    }

    private async Task LoadQuestionsAsync(Guid profileVersionId, ProfileSectionMetadataDto section, CancellationToken cancellationToken)
    {
        var answers = await ProfileSectionsApi.GetProfileSectionAnswersAsync(profileVersionId, section.Id, cancellationToken);

        var namesByQuestion = answers.QuestionNames.ToDictionary(name => name.Id);
        var valuesByQuestion = answers.FieldValues.ToLookup(value => value.QuestionId);

        // "List"/"MultiValueList" fields render as real checkbox/radio options rather than plain
        // text, so fetch every distinct reference table used by this section once.
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
                await ProfileSectionsApi.GetReferenceValuesAsync(referenceTableId, cancellationToken);
        }

        Questions =
        [
            .. section.Questions.Select(question =>
            {
                // Only a standard (non-per-species, non-repeating) question's fields are
                // editable; editing those two question kinds is not yet supported.
                var isQuestionEditable = CanEditProfileSection && !question.IsPerSpecies && !question.IsRepeating;
                var questionValues = valuesByQuestion[question.Id].ToList();

                // Matches the legacy CollapsiblePanel's tick/cross icon: valid once every
                // mandatory, non-"Field Group" field has a recorded answer.
                var isQuestionValid = question.Fields
                    .Where(field => field.IsMandatory && !string.Equals(field.DataTypeName, "Field Group", StringComparison.OrdinalIgnoreCase))
                    .All(field => questionValues.Any(value => value.FieldNumber == field.FieldNumber && HasRecordedValue(value)));

                return new ProfileQuestionEditView
                {
                    Number = $"{section.SectionNumber}.{question.QuestionNumber}",
                    Text = namesByQuestion.TryGetValue(question.Id, out var name) ? name.Name : question.ShortName,
                    IsValid = isQuestionValid,
                    CanEditReferences = CanEditProfileSection,
                    Fields =
                    [
                        // "Field Group" entries are containers for a repeating question's nested
                        // fields, not values themselves, so they have nothing to render on their own.
                        .. question.Fields
                            .Where(field => !string.Equals(field.DataTypeName, "Field Group", StringComparison.OrdinalIgnoreCase))
                            .Select(field => BuildFieldEditView(
                                field,
                                isQuestionEditable,
                                questionValues.Where(value => value.FieldNumber == field.FieldNumber).ToList(),
                                referenceValuesByTable))
                    ]
                };
            })
        ];
    }

    /// <summary>Matches the legacy question-level validity check: a field counts as answered when
    /// any value property is populated.</summary>
    private static bool HasRecordedValue(ProfileFieldValueDto value) =>
        value.BooleanValue is not null
        || value.ListValue is not null
        || value.DecimalValue is not null
        || value.DateValue is not null
        || !string.IsNullOrWhiteSpace(value.TextValue);

    /// <summary>Builds one field's view: a real editable control matching its data type when
    /// <paramref name="isEditable"/>, otherwise the same static read-only rendering as before
    /// (checkbox group for "List"/"MultiValueList", rich/HTML content for "Long Text", or plain
    /// text).</summary>
    private static ProfileFieldEditView BuildFieldEditView(
        ProfileFieldMetadataDto field,
        bool isEditable,
        List<ProfileFieldValueDto> recordedValues,
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

            return new ProfileFieldEditView
            {
                FieldId = field.Id,
                Label = field.Name,
                DataTypeName = field.DataTypeName,
                IsMandatory = field.IsMandatory,
                IsEditable = isEditable,
                Options = [.. options.Select(option => new ProfileFieldOptionEditView(option.Id, option.Value, selectedValues.Contains(option.Id)))]
            };
        }

        var isHtml = string.Equals(field.DataTypeName, "Long Text", StringComparison.OrdinalIgnoreCase);
        var recordedValue = recordedValues.Count > 0 ? recordedValues[0] : null;

        return new ProfileFieldEditView
        {
            FieldId = field.Id,
            Label = field.Name,
            DataTypeName = field.DataTypeName,
            IsMandatory = field.IsMandatory,
            IsEditable = isEditable,
            ValueDisplay = FormatFieldValues(recordedValues),
            IsHtml = isHtml,
            BooleanValue = recordedValue?.BooleanValue,
            DecimalValue = recordedValue?.DecimalValue,
            DateValue = recordedValue?.DateValue,
            TextValue = recordedValue?.TextValue
        };
    }

    /// <summary>Reads the posted form values for every editable field in <paramref name="section"/>
    /// and builds the corresponding <see cref="ProfileFieldAnswerInputDto"/> entries, matching the
    /// legacy per-datatype controls' Save() logic. An invalid decimal or date is reported as a
    /// validation error rather than silently dropped or thrown.</summary>
    private (IReadOnlyList<ProfileFieldAnswerInputDto> Answers, IReadOnlyList<string> Errors) ParseSubmittedAnswers(
        ProfileSectionMetadataDto section)
    {
        var answers = new List<ProfileFieldAnswerInputDto>();
        var errors = new List<string>();

        foreach (var question in section.Questions)
        {
            if (question.IsPerSpecies || question.IsRepeating)
            {
                continue;
            }

            foreach (var field in question.Fields)
            {
                if (string.Equals(field.DataTypeName, "Field Group", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ParseSubmittedField(section, question, field, answers, errors);
            }
        }

        return (answers, errors);
    }

    private void ParseSubmittedField(
        ProfileSectionMetadataDto section,
        ProfileQuestionMetadataDto question,
        ProfileFieldMetadataDto field,
        List<ProfileFieldAnswerInputDto> answers,
        List<string> errors)
    {
        var formKey = $"field_{field.Id}";
        var questionDisplay = $"{section.SectionNumber}.{question.QuestionNumber} {field.Name}";

        if (string.Equals(field.DataTypeName, "Boolean", StringComparison.OrdinalIgnoreCase))
        {
            answers.Add(new ProfileFieldAnswerInputDto
            {
                ProfileFieldId = field.Id,
                BooleanValue = Request.Form[formKey].ToString() switch { "true" => true, "false" => false, _ => null }
            });
        }
        else if (string.Equals(field.DataTypeName, "List", StringComparison.OrdinalIgnoreCase))
        {
            answers.Add(new ProfileFieldAnswerInputDto
            {
                ProfileFieldId = field.Id,
                ListValue = Guid.TryParse(Request.Form[formKey].ToString(), out var listValue) ? listValue : null
            });
        }
        else if (string.Equals(field.DataTypeName, "MultiValueList", StringComparison.OrdinalIgnoreCase))
        {
            answers.Add(new ProfileFieldAnswerInputDto
            {
                ProfileFieldId = field.Id,
                MultiListValues =
                [
                    .. Request.Form[formKey]
                        .Select(value => Guid.TryParse(value, out var guid) ? guid : (Guid?)null)
                        .Where(guid => guid is not null)
                        .Select(guid => guid!.Value)
                ]
            });
        }
        else if (string.Equals(field.DataTypeName, "Decimal", StringComparison.OrdinalIgnoreCase))
        {
            ParseDecimalField(field.Id, questionDisplay, Request.Form[formKey].ToString(), answers, errors);
        }
        else if (string.Equals(field.DataTypeName, "Date", StringComparison.OrdinalIgnoreCase))
        {
            ParseDateField(field.Id, questionDisplay, formKey, answers, errors);
        }
        else
        {
            var rawValue = Request.Form[formKey].ToString();
            answers.Add(new ProfileFieldAnswerInputDto
            {
                ProfileFieldId = field.Id,
                TextValue = string.IsNullOrWhiteSpace(rawValue) ? null : rawValue.Trim()
            });
        }
    }

    private static void ParseDecimalField(
        Guid fieldId, string questionDisplay, string rawValue, List<ProfileFieldAnswerInputDto> answers, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            answers.Add(new ProfileFieldAnswerInputDto { ProfileFieldId = fieldId });
        }
        else if (decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
        {
            answers.Add(new ProfileFieldAnswerInputDto { ProfileFieldId = fieldId, DecimalValue = decimalValue });
        }
        else
        {
            errors.Add($"{questionDisplay}: Enter a valid decimal number");
        }
    }

    private void ParseDateField(
        Guid fieldId, string questionDisplay, string formKey, List<ProfileFieldAnswerInputDto> answers, List<string> errors)
    {
        var day = Request.Form[$"{formKey}_day"].ToString();
        var month = Request.Form[$"{formKey}_month"].ToString();
        var year = Request.Form[$"{formKey}_year"].ToString();

        if (string.IsNullOrWhiteSpace(day) && string.IsNullOrWhiteSpace(month) && string.IsNullOrWhiteSpace(year))
        {
            answers.Add(new ProfileFieldAnswerInputDto { ProfileFieldId = fieldId });
        }
        else if (int.TryParse(day, out var dayValue) && int.TryParse(month, out var monthValue) && int.TryParse(year, out var yearValue) &&
            TryCreateDate(dayValue, monthValue, yearValue, out var dateValue))
        {
            answers.Add(new ProfileFieldAnswerInputDto { ProfileFieldId = fieldId, DateValue = dateValue });
        }
        else
        {
            errors.Add($"{questionDisplay}: Enter a valid date");
        }
    }

    private static bool TryCreateDate(int day, int month, int year, out DateTime date)
    {
        try
        {
            date = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            date = default;
            return false;
        }
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
