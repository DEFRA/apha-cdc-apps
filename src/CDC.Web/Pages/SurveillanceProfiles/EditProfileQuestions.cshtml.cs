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

    /// <summary>Gets the current version's display summary (for example, "Version 13.0 (Published)
    /// Valid from 11/09/2026"), shown centred beneath the title. <see langword="null"/> when the
    /// profile has no current version.</summary>
    public string? VersionInfo { get; private set; }

    /// <summary>Gets a value indicating whether the "Print full profile" link is shown, matching
    /// the legacy <c>ProfileVersionInfo.CanGetPrintVersion()</c> rule.</summary>
    public bool CanPrintProfileSection { get; private set; }

    /// <summary>Gets a value indicating whether the "Summary profile report" and "Summary
    /// prioritisation report" links are shown, matching the legacy
    /// <c>ProfileVersionInfo.CanGetSummaryReport()</c> rule.</summary>
    public bool CanViewProfileReports { get; private set; }

    /// <summary>Gets a value indicating whether the "Validate" button is shown, matching the
    /// legacy <c>ProfileVersionSection.CanEditProfileVersionSection()</c> rule.</summary>
    public bool CanEditProfileSection { get; private set; }

    /// <summary>Gets a value indicating whether the "Clear" button is shown, matching the legacy
    /// <c>ProfileVersionSection.CanClearProfileVersionSection()</c> rule.</summary>
    public bool CanClearProfileSection { get; private set; }

    /// <summary>Gets a value indicating whether the "Review comments" tab is shown, matching the
    /// legacy <c>ReviewCommentList.CanGetReviewCommentList()</c> rule.</summary>
    public bool CanViewReviewComments { get; private set; }

    /// <summary>Gets a value indicating whether the "Contributions" tab is shown, matching the
    /// legacy <c>ProfileContributionInfoList.CanGetProfileContributionInfoList()</c> rule.</summary>
    public bool CanViewContributions { get; private set; }

    /// <summary>Gets a value indicating whether the "All Revision dates" tab is shown, matching
    /// the legacy <c>ProfileVersionSection.CanGetRevisionInformation()</c> rule.</summary>
    public bool CanViewAllRevisionDates { get; private set; }

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

    /// <summary>Gets the profile's current version id, posted back by the Validate/Clear forms.
    /// <see cref="Guid.Empty"/> when the profile has no current version.</summary>
    public Guid CurrentProfileVersionId { get; private set; }

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

    /// <summary>Gets each section's validation status (section id to whether it passed
    /// validation), used to render the green tick/red cross against each left-nav entry.
    /// Sections absent from this dictionary (for example, after a load failure) show no icon.</summary>
    public IReadOnlyDictionary<Guid, bool> SectionValidationStatus { get; private set; } = new Dictionary<Guid, bool>();

    /// <summary>Gets the real questions and recorded answers for <see cref="CurrentSection"/>,
    /// each field either rendered as a real editable control or, for a per-species/repeating
    /// question, as a static read-only value.</summary>
    public IReadOnlyList<ProfileQuestionEditView> Questions { get; private set; } = [];

    /// <summary>Gets the current section's scientific paper references, for the References tab.</summary>
    public ProfileNoteGroupView ScientificPaperReferences { get; private set; } = EmptyNoteGroup("Scientific paper references");

    /// <summary>Gets the current section's legislative references, for the References tab.</summary>
    public ProfileNoteGroupView LegislativeReferences { get; private set; } = EmptyNoteGroup("Legislative references");

    /// <summary>Gets the current section's sources of further information, for the Further
    /// information tab.</summary>
    public ProfileNoteGroupView FurtherInformation { get; private set; } = EmptyNoteGroup("Sources of further information");

    /// <summary>Gets the current section's logged contributions, for the Contributions tab.</summary>
    public IReadOnlyList<ProfileSectionContributionView> Contributions { get; private set; } = [];

    /// <summary>Gets the current section's review comments, for the Review comments tab.</summary>
    public IReadOnlyList<ProfileReviewCommentView> ReviewComments { get; private set; } = [];

    /// <summary>Gets every section's revision dates, for the All Revision dates tab.</summary>
    public IReadOnlyList<ProfileSectionRevisionDatesView> AllRevisionDates { get; private set; } = [];

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

            ApplyProfileFlags(profile);

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
            CurrentProfileVersionId = profileVersionId;
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
            await LoadNotesResilientlyAsync(profileVersionId, CurrentSection, cancellationToken);
        }

        // A separate, independent load: one section's validation status must not depend on, or
        // block, the currently selected section's questions/notes, and vice versa.
        if (!HasError && !HasNoVersion)
        {
            await LoadValidationStatusAsync(profileVersionId, cancellationToken);
        }

        // Each Draft-only tab's data is loaded independently and only when its tab is visible,
        // matching the legacy tabStrip.Tabs.Item(x).Visible gating of each tab's data fetch.
        if (!HasError && !HasNoVersion && CurrentSection is not null)
        {
            await LoadDraftOnlyTabsAsync(profileVersionId, CurrentSection, cancellationToken);
        }

        return Page();
    }

    private void ApplyProfileFlags(ManageProfileViewModel profile)
    {
        ProfileTitle = profile.FullTitle;
        VersionInfo = profile.CurrentVersionDisplay;
        CanPrintProfileSection = profile.CanPrintProfileSection;
        CanViewProfileReports = profile.CanViewProfileReports;
        CanEditProfileSection = profile.CanEditProfileSection;
        CanClearProfileSection = profile.CanClearProfileSection;
        CanViewReviewComments = profile.CanViewReviewComments;
        CanViewContributions = profile.CanViewContributions;
        CanViewAllRevisionDates = profile.CanViewAllRevisionDates;
    }

    private async Task LoadNotesResilientlyAsync(Guid profileVersionId, ProfileSectionMetadataDto section, CancellationToken cancellationToken)
    {
        try
        {
            await LoadNotesAsync(profileVersionId, section, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.ProfileNotesLoadFailed(exception, ProfileId);
        }
    }

    private async Task LoadDraftOnlyTabsAsync(Guid profileVersionId, ProfileSectionMetadataDto section, CancellationToken cancellationToken)
    {
        if (CanViewContributions)
        {
            await LoadContributionsAsync(profileVersionId, section, cancellationToken);
        }

        if (CanViewReviewComments)
        {
            await LoadReviewCommentsAsync(profileVersionId, section, cancellationToken);
        }

        if (CanViewAllRevisionDates)
        {
            await LoadAllRevisionDatesAsync(profileVersionId, cancellationToken);
        }
    }

    /// <summary>Builds the querystring URL for a left-nav/pagination link to a different section of this profile.</summary>
    public string? BuildSectionUrl(Guid sectionId) =>
        Url.Page("/SurveillanceProfiles/EditProfileQuestions", new { profileId = ProfileId, section = sectionId });

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

        if (HasError || HasNoVersion || CurrentSection is null)
        {
            return Page();
        }

        try
        {
            var result = await profileSectionsApiService.ValidateProfileVersionSectionAsync(
                profileVersionId, CurrentSection.Id, cancellationToken);
            UnansweredQuestions = result.UnansweredQuestions;
            HasValidated = true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.ProfileSectionValidateFailed(exception, ProfileId, CurrentSection.Id);
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

        if (HasError || HasNoVersion || CurrentSection is null)
        {
            return Page();
        }

        try
        {
            var result = await profileSectionsApiService.ClearProfileVersionSectionAsync(
                profileVersionId, CurrentSection.Id, cancellationToken);
            FieldsClearedCount = result.FieldsClearedCount;

            // Re-load so the page reflects the now-cleared answers rather than the stale ones
            // already fetched by OnGetAsync above.
            await LoadQuestionsAsync(profileVersionId, CurrentSection, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.ProfileSectionClearFailed(exception, ProfileId, CurrentSection.Id);
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

        if (HasError || HasNoVersion || CurrentSection is null)
        {
            return Page();
        }

        var (answers, validationErrors) = ParseSubmittedAnswers(CurrentSection);

        if (validationErrors.Count > 0)
        {
            SaveValidationErrors = validationErrors;
            return Page();
        }

        try
        {
            var result = await profileSectionsApiService.SaveProfileVersionSectionAsync(
                profileVersionId, CurrentSection.Id, answers, cancellationToken);
            FieldsSavedCount = result.FieldsSavedCount;

            // Re-load so the page reflects the newly-saved answers rather than the stale ones
            // already fetched by OnGetAsync above.
            await LoadQuestionsAsync(profileVersionId, CurrentSection, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.ProfileSectionSaveFailed(exception, ProfileId, CurrentSection.Id);
            HasError = true;
        }

        return Page();
    }

    /// <summary>Gets one section's validation status for the left-nav tick/cross icon, or
    /// <see langword="null"/> when it is not known (for example, after a load failure).</summary>
    public bool? GetSectionValidationStatus(Guid sectionId) =>
        SectionValidationStatus.TryGetValue(sectionId, out var isValid) ? isValid : null;

    /// <summary>Loads the current section's References and Further information tab content.
    /// Resolves each note type by name rather than a hardcoded id, since note types are reference
    /// data (<c>GET /api/profile-notes/types</c>); a missing type degrades to an empty group
    /// rather than failing the whole page.</summary>
    /// <summary>Loads every section's validation status, resiliently: a failure here degrades to
    /// no icons being shown rather than failing the whole page.</summary>
    private async Task LoadValidationStatusAsync(Guid profileVersionId, CancellationToken cancellationToken)
    {
        try
        {
            var validationStatus = await profileSectionsApiService.GetProfileVersionValidationStatusAsync(profileVersionId, cancellationToken);
            SectionValidationStatus = validationStatus.Sections.ToDictionary(section => section.ProfileSectionId, section => section.IsValid);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.ProfileSectionValidationStatusLoadFailed(exception, ProfileId);
        }
    }

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

    /// <summary>Loads the Contributions tab's content, resiliently: a failure here degrades to an
    /// empty table rather than failing the whole page.</summary>
    private async Task LoadContributionsAsync(Guid profileVersionId, ProfileSectionMetadataDto section, CancellationToken cancellationToken)
    {
        try
        {
            var contributions = await profileSectionsApiService.GetProfileVersionSectionContributionsAsync(profileVersionId, section.Id, cancellationToken);

            Contributions =
            [
                .. contributions.Select(contribution => new ProfileSectionContributionView(
                    contribution.FullName,
                    contribution.UserName,
                    contribution.OrganisationName,
                    contribution.LastContributionDate.ToString(DateDisplayFormat, CultureInfo.InvariantCulture)))
            ];
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.ProfileSectionContributionsLoadFailed(exception, ProfileId);
        }
    }

    /// <summary>Loads the Review comments tab's content, resiliently: a failure here degrades to
    /// an empty list rather than failing the whole page.</summary>
    private async Task LoadReviewCommentsAsync(Guid profileVersionId, ProfileSectionMetadataDto section, CancellationToken cancellationToken)
    {
        try
        {
            var comments = await profileSectionsApiService.GetProfileSectionReviewCommentsAsync(profileVersionId, section.Id, cancellationToken);

            ReviewComments =
            [
                .. comments.Select(comment => new ProfileReviewCommentView(
                    IndentSubject(comment.Level) + comment.CommentSubject,
                    comment.CommentReference,
                    comment.CommentText,
                    comment.UserFullName,
                    comment.CommentDate.ToString(DateDisplayFormat, CultureInfo.InvariantCulture)))
            ];
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.ProfileReviewCommentsLoadFailed(exception, ProfileId);
        }
    }

    /// <summary>Indents a reply's subject by its thread depth, matching the legacy
    /// <c>EditProfileReviewComments.aspx.vb</c>'s <c>IndentSubject()</c>.</summary>
    private static string IndentSubject(int level) =>
        level <= 1 ? string.Empty : new string('\u00A0', (level - 1) * 5);

    /// <summary>Loads the All Revision dates tab's content, resiliently: a failure here degrades
    /// to an empty table rather than failing the whole page.</summary>
    private async Task LoadAllRevisionDatesAsync(Guid profileVersionId, CancellationToken cancellationToken)
    {
        try
        {
            var revisionDates = await profileSectionsApiService.GetProfileVersionAllRevisionDatesAsync(profileVersionId, cancellationToken);

            AllRevisionDates =
            [
                .. revisionDates.Sections.Select(revisionDate => new ProfileSectionRevisionDatesView(
                    $"{revisionDate.SectionNumber} {revisionDate.SectionName}",
                    FormatReviewFrequency(revisionDate.TechnicalReviewFrequencyMonths),
                    FormatReviewDate(revisionDate.NextTechnicalReview),
                    FormatReviewFrequency(revisionDate.PolicyReviewFrequencyMonths),
                    FormatReviewDate(revisionDate.NextPolicyReview)))
            ];
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.ProfileAllRevisionDatesLoadFailed(exception, ProfileId);
        }
    }

    private static string FormatReviewFrequency(int? months)
    {
        if (months is not { } value)
        {
            return "Not set";
        }

        return value == 1 ? "1 month" : $"{value} months";
    }

    private static string FormatReviewDate(DateTime? date) =>
        date?.ToString(DateDisplayFormat, CultureInfo.InvariantCulture) ?? "Not set";

    private const string DateDisplayFormat = "dd/MM/yyyy";

    private async Task LoadQuestionsAsync(Guid profileVersionId, ProfileSectionMetadataDto section, CancellationToken cancellationToken)
    {
        var answers = await profileSectionsApiService.GetProfileSectionAnswersAsync(profileVersionId, section.Id, cancellationToken);

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
                await profileSectionsApiService.GetReferenceValuesAsync(referenceTableId, cancellationToken);
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
        ProfileFieldValueDto? recordedValue = recordedValues.Count > 0 ? recordedValues[0] : null;

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

/// <summary>Source-generated structured log messages for <see cref="EditProfileQuestionsModel"/>.</summary>
internal static partial class EditProfileQuestionsLog
{
    [LoggerMessage(EventId = 2300, Level = LogLevel.Error, Message = "Failed to load profile '{ProfileId}' questions from CDC.Api")]
    public static partial void ProfileQuestionsLoadFailed(this ILogger logger, Exception exception, Guid profileId);

    [LoggerMessage(EventId = 2301, Level = LogLevel.Error, Message = "Failed to load profile '{ProfileId}' references/further information from CDC.Api")]
    public static partial void ProfileNotesLoadFailed(this ILogger logger, Exception exception, Guid profileId);

    [LoggerMessage(EventId = 2302, Level = LogLevel.Error, Message = "Failed to load profile '{ProfileId}' section validation status from CDC.Api")]
    public static partial void ProfileSectionValidationStatusLoadFailed(this ILogger logger, Exception exception, Guid profileId);

    [LoggerMessage(EventId = 2303, Level = LogLevel.Error, Message = "Failed to validate profile '{ProfileId}' section '{ProfileSectionId}' via CDC.Api")]
    public static partial void ProfileSectionValidateFailed(this ILogger logger, Exception exception, Guid profileId, Guid profileSectionId);

    [LoggerMessage(EventId = 2304, Level = LogLevel.Error, Message = "Failed to clear profile '{ProfileId}' section '{ProfileSectionId}' via CDC.Api")]
    public static partial void ProfileSectionClearFailed(this ILogger logger, Exception exception, Guid profileId, Guid profileSectionId);

    [LoggerMessage(EventId = 2305, Level = LogLevel.Error, Message = "Failed to save profile '{ProfileId}' section '{ProfileSectionId}' via CDC.Api")]
    public static partial void ProfileSectionSaveFailed(this ILogger logger, Exception exception, Guid profileId, Guid profileSectionId);

    [LoggerMessage(EventId = 2306, Level = LogLevel.Error, Message = "Failed to load profile '{ProfileId}' contributions from CDC.Api")]
    public static partial void ProfileSectionContributionsLoadFailed(this ILogger logger, Exception exception, Guid profileId);

    [LoggerMessage(EventId = 2307, Level = LogLevel.Error, Message = "Failed to load profile '{ProfileId}' review comments from CDC.Api")]
    public static partial void ProfileReviewCommentsLoadFailed(this ILogger logger, Exception exception, Guid profileId);

    [LoggerMessage(EventId = 2308, Level = LogLevel.Error, Message = "Failed to load profile '{ProfileId}' all revision dates from CDC.Api")]
    public static partial void ProfileAllRevisionDatesLoadFailed(this ILogger logger, Exception exception, Guid profileId);
}
