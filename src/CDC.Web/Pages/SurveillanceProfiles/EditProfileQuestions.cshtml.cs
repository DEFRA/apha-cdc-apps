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
    /// <summary>The prefix of the questions accordion's element ids, which the validation error links jump to.</summary>
    public const string AccordionIdPrefix = "profile-questions-accordion";

    /// <summary>The message shown when the answers were saved, as the legacy <c>lblErrorDesc</c> worded it.</summary>
    public const string SaveSucceededMessage = "Profile section save succeeded";

    /// <summary>The start of the message shown when the answers could not be saved; the reason follows it.</summary>
    public const string SaveFailedMessage = "Profile section save failed: ";

    /// <summary>The message shown when the section was cleared.</summary>
    public const string ClearSucceededMessage = "Clear section succeeded";

    /// <summary>The start of the message shown when the section could not be cleared; the reason follows it.</summary>
    public const string ClearFailedMessage = "Clear section failed: ";

    private const string RequestFailedReason = "We could not complete the request. Try again later.";

    // The start of the message shown when the answers were cleared but a later step could not be; what failed and the reason follow it.
    private const string ClearFailedWhileClearingMessage = "Clear section failed while trying to clear the ";

    // The notes a cleared section is unlinked from, in the order the legacy Clear button did it, with how its failure names them.
    private static readonly (string NoteType, string Description)[] ClearedNoteTypes =
    [
        (ProfileNoteDisplayHelper.ScientificPaperReferenceType, "scientific paper references"),
        (ProfileNoteDisplayHelper.LegislativeReferenceType, "legislative references"),
        (ProfileNoteDisplayHelper.SourceOfFurtherInformationType, "sources of further information")
    ];

    /// <inheritdoc />
    public override ProfileSectionTab ActiveTab => ProfileSectionTab.Questions;

    /// <summary>Gets every validation error found by the most recent "Validate" button submission, in the order the
    /// legacy page listed them: the revision-date errors, then each question's errors. Empty until Validate has been
    /// used, and when the section is valid.</summary>
    public IReadOnlyList<ProfileSectionInvalidReasonDto> InvalidReasons { get; private set; } = [];

    /// <summary>Gets a value indicating whether "Validate" has been run for the current section
    /// during this request.</summary>
    public bool HasValidated { get; private set; }

    /// <summary>Gets the outcome of the most recent "Save" or "Clear" button submission in the legacy wording (for example
    /// "Profile section save succeeded" or "Clear section failed: ..."), or <see langword="null"/> until one has been used.</summary>
    public string? ActionMessage { get; private set; }

    /// <summary>Gets a value indicating whether <see cref="ActionMessage"/> reports a failure.</summary>
    public bool ActionMessageIsError { get; private set; }

    /// <summary>Gets the section's row version, Base64 encoded, as it was when the answers were loaded. The form posts it
    /// back so a save or clear is refused, rather than overwriting, when someone else has saved the section since.</summary>
    public string SectionLastUpdated { get; private set; } = string.Empty;

    /// <summary>Gets the field-level validation errors (for example, an invalid date or decimal)
    /// from the most recent "Save" button submission. Empty when Save succeeded or has not been
    /// used; when non-empty, the submitted answers were not sent to CDC.Api.</summary>
    public IReadOnlyList<string> SaveValidationErrors { get; private set; } = [];

    /// <summary>Gets the real questions and recorded answers for the selected section, each field either
    /// rendered as a real editable control or, for a per-species/repeating question, as a static read-only value.</summary>
    public IReadOnlyList<ProfileQuestionEditView> Questions { get; private set; } = [];

    /// <inheritdoc />
    public override bool HasFormErrors => SaveValidationErrors.Count > 0 || InvalidReasons.Count > 0 || ActionMessageIsError;

    /// <summary>Builds the text of a validation error as the legacy <c>InvalidReasonList</c> printed it: the question
    /// number, then the description without its HTML markup.</summary>
    /// <param name="reason">The validation error.</param>
    /// <returns>The plain text of the error.</returns>
    public static string BuildValidationReasonText(ProfileSectionInvalidReasonDto reason)
    {
        ArgumentNullException.ThrowIfNull(reason);

        return $"{reason.SectionNumber}.{reason.QuestionNumber} {ProfileTitleHtmlFormatter.ToPlainText(reason.Description)}";
    }

    /// <summary>Builds the link of a validation error: the question's heading on this page, or for a revision-date
    /// error the All Revision dates tab, where those dates are set.</summary>
    /// <param name="reason">The validation error.</param>
    /// <returns>The link target, or <see langword="null"/> when a revision-date error has no tab to open for this user.</returns>
    public string? BuildValidationReasonUrl(ProfileSectionInvalidReasonDto reason)
    {
        ArgumentNullException.ThrowIfNull(reason);

        if (reason.QuestionId != Guid.Empty)
        {
            return "#" + QuestionEditAccordionViewModel.HeadingId(AccordionIdPrefix, $"{reason.SectionNumber}.{reason.QuestionNumber}");
        }

        return CanViewAllRevisionDates && CurrentSection is { } section
            ? Url.Page(ProfileSectionTabs.PagePath(ProfileSectionTab.AllRevisionDates), new { profileId = ProfileId, section = section.Id })
            : null;
    }

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
    /// Handles the "Validate" button as the legacy <c>btnValidateSection_Click</c> did: saves the posted answers, then runs
    /// <c>ProfileVersionSection.Validate()</c> and re-renders the page with the list of validation errors.
    /// </summary>
    /// <param name="profileVersionId">The profile version to validate, posted from a hidden field.</param>
    /// <param name="lastUpdated">The section's Base64 row version, read when the page was shown.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The page, with the validation result populated; without one when the answers could not be saved.</returns>
    public async Task<IActionResult> OnPostValidateSectionAsync(Guid profileVersionId, string? lastUpdated, CancellationToken cancellationToken)
    {
        await OnGetAsync(cancellationToken);

        if (!IsReady || (CanEditProfileSection && !await TrySaveSectionAsync(profileVersionId, lastUpdated, cancellationToken)))
        {
            return Page();
        }

        try
        {
            var result = await ProfileSectionsApi.ValidateProfileVersionSectionAsync(
                profileVersionId, CurrentSection!.Id, cancellationToken);
            InvalidReasons = result.InvalidReasons;
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
    /// Handles the "Clear" button as the legacy <c>btnClearSection_Click</c> did: clears the section's answers and revision
    /// data, then unlinks the section's scientific paper references, legislative references and sources of further
    /// information from its questions, then deletes its review comments, stopping at the first step that fails. The page is
    /// then shown with the section's refreshed (now empty) answers.
    /// </summary>
    /// <param name="profileVersionId">The profile version to clear, posted from a hidden field.</param>
    /// <param name="lastUpdated">The section's Base64 row version, read when the page was shown.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The page, with the outcome in <see cref="ActionMessage"/>.</returns>
    public async Task<IActionResult> OnPostClearSectionAsync(Guid profileVersionId, string? lastUpdated, CancellationToken cancellationToken)
    {
        await OnGetAsync(cancellationToken);

        if (!IsReady || !CanClearProfileSection)
        {
            return Page();
        }

        var sectionId = CurrentSection!.Id;
        ProfileSectionWriteResult cleared;

        try
        {
            cleared = await ProfileSectionsApi.ClearProfileVersionSectionAsync(
                profileVersionId, sectionId, ProfileNoteChangeBuilder.DecodeRowVersion(lastUpdated), cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            Logger.ProfileSectionClearFailed(exception, ProfileId, sectionId);
            SetAction(ClearFailedMessage + RequestFailedReason, isError: true);

            return Page();
        }

        if (cleared.Outcome != ProfileSectionWriteOutcome.Success)
        {
            Logger.ProfileSectionWriteRefused("clear", ProfileId, sectionId, cleared.Outcome);
            SetAction(ClearFailedMessage + cleared.ErrorMessage, isError: true);

            return Page();
        }

        SetAction(ClearSucceededMessage, isError: false);

        // Re-load so the page reflects the now-cleared answers and their validation icons rather
        // than the stale ones already fetched by OnGetAsync above.
        if (!await TryReloadQuestionsAsync(profileVersionId, cancellationToken))
        {
            return Page();
        }

        var failure = await ClearReferencesAndReviewCommentsAsync(profileVersionId, sectionId, cancellationToken);

        if (failure is not null)
        {
            SetAction(failure, isError: true);
        }

        return Page();
    }

    /// <summary>
    /// Handles the "Save" button: writes the answers submitted for the current section's standard
    /// (non-per-species, non-repeating) questions that changed, matching the legacy save, then re-renders the page with the
    /// saved answers. Fields with an invalid decimal or date are reported back without calling CDC.Api.
    /// </summary>
    /// <param name="profileVersionId">The profile version being answered, posted from a hidden field.</param>
    /// <param name="lastUpdated">The section's Base64 row version, read when the page was shown.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The page, with the outcome in <see cref="ActionMessage"/> or the submission's validation errors.</returns>
    public async Task<IActionResult> OnPostSaveSectionAsync(Guid profileVersionId, string? lastUpdated, CancellationToken cancellationToken)
    {
        await OnGetAsync(cancellationToken);

        if (IsReady && CanEditProfileSection)
        {
            await TrySaveSectionAsync(profileVersionId, lastUpdated, cancellationToken);
        }

        return Page();
    }

    /// <summary>
    /// Handles a click on a tab. Legacy <c>tabStrip_TabClick</c> persists the section's answers before opening the
    /// tab's page and stays put when they are invalid, so an edit is never lost by switching tab.
    /// </summary>
    /// <param name="profileVersionId">The profile version being answered, posted from a hidden field.</param>
    /// <param name="tab">The name of the tab clicked.</param>
    /// <param name="lastUpdated">The section's Base64 row version, read when the page was shown.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A redirect to the tab's page, or this page again when the answers could not be saved.</returns>
    public async Task<IActionResult> OnPostNavigateTabAsync(Guid profileVersionId, string? tab, string? lastUpdated, CancellationToken cancellationToken)
    {
        await OnGetAsync(cancellationToken);

        if (!IsReady
            || !Enum.TryParse<ProfileSectionTab>(tab, ignoreCase: true, out var target)
            || !ProfileSectionTabs.VisibleTabs(CanViewReviewComments, CanViewAllRevisionDates, CanViewContributions).Contains(target))
        {
            return Page();
        }

        if (CanEditProfileSection && !await TrySaveSectionAsync(profileVersionId, lastUpdated, cancellationToken))
        {
            return Page();
        }

        return RedirectToPage(ProfileSectionTabs.PagePath(target), new { profileId = ProfileId, section = CurrentSection!.Id });
    }

    /// <summary>Saves the submitted answers and reloads the page's answers; reports a rejected value, a concurrent edit or a
    /// CDC.Api failure on the page, which stays usable, as the legacy page did.</summary>
    /// <returns><see langword="true"/> when the answers were saved.</returns>
    private async Task<bool> TrySaveSectionAsync(Guid profileVersionId, string? lastUpdated, CancellationToken cancellationToken)
    {
        var (answers, validationErrors) = ParseSubmittedAnswers(CurrentSection!);

        if (validationErrors.Count > 0)
        {
            SaveValidationErrors = validationErrors;
            return false;
        }

        ProfileSectionWriteResult saved;

        try
        {
            saved = await ProfileSectionsApi.SaveProfileVersionSectionAsync(
                profileVersionId, CurrentSection!.Id, answers, ProfileNoteChangeBuilder.DecodeRowVersion(lastUpdated), cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            Logger.ProfileSectionSaveFailed(exception, ProfileId, CurrentSection!.Id);
            SetAction(SaveFailedMessage + RequestFailedReason, isError: true);

            return false;
        }

        if (saved.Outcome != ProfileSectionWriteOutcome.Success)
        {
            Logger.ProfileSectionWriteRefused("save", ProfileId, CurrentSection!.Id, saved.Outcome);
            SetAction(SaveFailedMessage + saved.ErrorMessage, isError: true);

            return false;
        }

        SetAction(SaveSucceededMessage, isError: false);

        // Re-load so the page reflects the newly-saved answers, their validation icons and the section's new row version
        // rather than the stale ones already fetched by OnGetAsync above.
        return await TryReloadQuestionsAsync(profileVersionId, cancellationToken);
    }

    private void SetAction(string message, bool isError)
    {
        ActionMessage = message;
        ActionMessageIsError = isError;
    }

    private async Task<bool> TryReloadQuestionsAsync(Guid profileVersionId, CancellationToken cancellationToken)
    {
        try
        {
            await RefreshValidationStatusAsync(cancellationToken);
            await LoadQuestionsAsync(profileVersionId, CurrentSection!, cancellationToken);

            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            Logger.ProfileQuestionsLoadFailed(exception, ProfileId);
            HasError = true;

            return false;
        }
    }

    /// <summary>The steps of the legacy Clear button that follow clearing the answers. Each failure ends the sequence and is
    /// reported in the legacy wording, naming what could not be cleared.</summary>
    /// <returns>The message to show when a step failed, otherwise <see langword="null"/>.</returns>
    private async Task<string?> ClearReferencesAndReviewCommentsAsync(Guid profileVersionId, Guid sectionId, CancellationToken cancellationToken)
    {
        try
        {
            var noteTypes = await ProfileSectionsApi.GetProfileNoteTypesAsync(cancellationToken);

            foreach (var (normalisedName, description) in ClearedNoteTypes)
            {
                var noteType = noteTypes.FirstOrDefault(type => ProfileNoteDisplayHelper.NormaliseNoteTypeName(type.Name) == normalisedName);

                if (noteType is null)
                {
                    continue;
                }

                var error = await UnlinkNotesFromSectionAsync(profileVersionId, sectionId, noteType.Id, cancellationToken);

                if (error is not null)
                {
                    return $"{ClearFailedWhileClearingMessage}{description}: {error}";
                }
            }

            var comments = await ProfileSectionsApi.DeleteAllProfileReviewCommentsAsync(profileVersionId, sectionId, cancellationToken);

            return comments.Outcome == ProfileSectionWriteOutcome.Success
                ? null
                : $"{ClearFailedWhileClearingMessage}review comments: {comments.ErrorMessage}";
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            Logger.ProfileSectionClearFailed(exception, ProfileId, sectionId);

            return ClearFailedMessage + RequestFailedReason;
        }
    }

    /// <summary>Unlinks every note of one type from every question of the section, the legacy
    /// <c>NoteList.RemoveQuestionReferenceByProfileSectionId</c> followed by <c>Save()</c>. The notes stay in the master list.</summary>
    /// <returns>The reason when the change was refused, otherwise <see langword="null"/>.</returns>
    private async Task<string?> UnlinkNotesFromSectionAsync(Guid profileVersionId, Guid sectionId, Guid noteTypeId, CancellationToken cancellationToken)
    {
        var notes = await ProfileSectionsApi.GetProfileNotesBySectionAsync(profileVersionId, sectionId, noteTypeId, cancellationToken);
        var updates = notes
            .Select(note => ProfileNoteChangeBuilder.BuildRemoval(note, note.LastUpdated, sectionId))
            .OfType<ProfileNoteUpdate>()
            .ToList();

        if (updates.Count == 0)
        {
            return null;
        }

        var result = await ProfileSectionsApi.UpdateProfileNotesAsync(profileVersionId, noteTypeId, [], updates, cancellationToken);

        return result.Outcome == ProfileNoteChangeOutcome.Success ? null : result.ErrorMessage;
    }

    private async Task LoadQuestionsAsync(Guid profileVersionId, ProfileSectionMetadataDto section, CancellationToken cancellationToken)
    {
        var answers = await ProfileSectionsApi.GetProfileSectionAnswersAsync(profileVersionId, section.Id, cancellationToken);
        SectionLastUpdated = Convert.ToBase64String(answers.LastUpdated);

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

                return new ProfileQuestionEditView
                {
                    Number = $"{section.SectionNumber}.{question.QuestionNumber}",
                    Text = namesByQuestion.TryGetValue(question.Id, out var name) ? name.Name : question.ShortName,
                    IsValid = GetQuestionValidationStatus(question.Id),
                    IsHidden = IsQuestionIrrelevant(question.Id),
                    CanEditReferences = CanEditProfileSection,
                    Fields =
                    [
                        // "Field Group" entries are containers for a repeating question's nested
                        // fields, not values themselves, so they have nothing to render on their own.
                        .. question.Fields
                            .Where(field => !string.Equals(field.DataTypeName, "Field Group", StringComparison.OrdinalIgnoreCase))
                            .Select(field => BuildFieldEditView(
                                field,
                                isQuestionEditable && !field.IsReadOnlyFor(IsWhatIfScenario),
                                questionValues.Where(value => value.FieldNumber == field.FieldNumber).ToList(),
                                referenceValuesByTable))
                    ]
                };
            })
        ];
    }

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
    /// legacy per-datatype controls' Save() logic. A field that is read-only for the kind of profile has no
    /// control, so it is not read. An invalid decimal or date is reported as a
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
                if (string.Equals(field.DataTypeName, "Field Group", StringComparison.OrdinalIgnoreCase)
                    || field.IsReadOnlyFor(IsWhatIfScenario))
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
