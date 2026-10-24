using System.Globalization;
using CDC.Web.Authorization.Constants;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// Edits the user guidance and titles of the profile questions, section by section, and starts the four guidance
/// reports. Replaces the legacy <c>EditUserGuidance.aspx</c>, which edited a question in place inside an update panel;
/// here each question opens from a link and is saved with an ordinary form post. Reached from the "Questions and
/// Guidance report" link in the header menu.
/// </summary>
/// <param name="sectionsApiService">Typed client for the profile section endpoints on CDC.Api.</param>
/// <param name="questionsApiService">Typed client for the profile question endpoints on CDC.Api.</param>
/// <param name="logger">Structured logger.</param>
[Authorize(Policy = AuthorizationPolicies.CanEditUserGuidance)]
public class EditUserGuidanceModel(
    IProfileSectionsApiService sectionsApiService,
    IProfileQuestionsApiService questionsApiService,
    ILogger<EditUserGuidanceModel> logger) : BreadcrumbPageModelBase("Edit questions and guidance")
{
    /// <summary>The prefix of the message shown when a save fails.</summary>
    public const string SaveFailedPrefix = "User guidance save failed: ";

    /// <summary>The message shown when a save succeeds.</summary>
    public const string SaveSucceededMessage = "User guidance save succeeded";

    /// <summary>The message shown when the sections could not be listed.</summary>
    public const string SectionsLoadFailureMessage = "We could not load the profile sections. Try again later.";

    /// <summary>The message shown when the questions of a section could not be listed.</summary>
    public const string QuestionsLoadFailureMessage = "We could not load the questions for this section. Try again later.";

    /// <summary>The message shown when a question could not be opened for editing.</summary>
    public const string QuestionLoadFailureMessage = "We could not load this question. Try again later.";

    /// <summary>The message shown when the question to edit is not in the selected section.</summary>
    public const string QuestionNotFoundMessage = "The question could not be found. Select it from the list again.";

    /// <summary>The model state key of the question title, which is also the order the fields appear in.</summary>
    public const string NameKey = "Input.Name";

    /// <summary>The model state key of the non-technical title.</summary>
    public const string NonTechnicalNameKey = "Input.NonTechnicalName";

    /// <summary>The model state key of the guidance.</summary>
    public const string UserGuidanceKey = "Input.UserGuidance";

    /// <summary>The error for an empty question title.</summary>
    public const string NameRequiredMessage = "Enter the question text";

    /// <summary>The error for an empty non-technical title.</summary>
    public const string NonTechnicalNameRequiredMessage = "Enter the non-technical question text";

    /// <summary>The error for an empty guidance.</summary>
    public const string GuidanceRequiredMessage = "Enter the user guidance text";

    /// <summary>The error for a question title that is too long.</summary>
    public static readonly string NameTooLongMessage =
        $"The question text must be {ProfileQuestionHtml.MaxTitleTextLength.ToString(CultureInfo.InvariantCulture)} characters or fewer";

    /// <summary>The error for a non-technical title that is too long.</summary>
    public static readonly string NonTechnicalNameTooLongMessage =
        $"The non-technical question text must be {ProfileQuestionHtml.MaxTitleTextLength.ToString(CultureInfo.InvariantCulture)} characters or fewer";

    /// <summary>Gets or sets the profile section whose questions are listed; <see langword="null"/> until one is selected.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid? Section { get; set; }

    /// <summary>Gets or sets the question being edited; <see langword="null"/> when no question is open.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid? QuestionId { get; set; }

    /// <summary>Gets or sets what the user typed for the open question.</summary>
    [BindProperty]
    public EditUserGuidanceInput Input { get; set; } = new();

    /// <summary>Gets or sets the message shown after a save succeeded.</summary>
    [TempData]
    public string? SuccessMessage { get; set; }

    /// <summary>Gets or sets the message shown after a save failed.</summary>
    [TempData]
    public string? FailureMessage { get; set; }

    /// <summary>Gets the sections to choose from, in section-number order.</summary>
    public IReadOnlyList<GuidanceSectionChoice> Sections { get; private set; } = [];

    /// <summary>Gets the questions of the selected section.</summary>
    public IReadOnlyList<ProfileQuestionInfoDto> Questions { get; private set; } = [];

    /// <summary>Gets the number of the question being edited, shown in the panel heading.</summary>
    public int? EditingQuestionNumber { get; private set; }

    /// <summary>Gets a value indicating whether a section is selected.</summary>
    public bool HasSection => Section.HasValue;

    /// <summary>Gets a value indicating whether the edit panel is open.</summary>
    public bool IsEditing => EditingQuestionNumber.HasValue;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadSectionsAsync(cancellationToken))
        {
            return Page();
        }

        if (!await LoadQuestionsAsync(cancellationToken))
        {
            return Page();
        }

        if (QuestionId.HasValue)
        {
            await OpenQuestionAsync(QuestionId.Value, cancellationToken);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        // Every field is checked below, so the binder's own messages for them are not wanted.
        ModelState.Clear();

        if (!await LoadSectionsAsync(cancellationToken) || !await LoadQuestionsAsync(cancellationToken))
        {
            return Page();
        }

        var question = QuestionId.HasValue ? Questions.FirstOrDefault(candidate => candidate.Id == QuestionId.Value) : null;

        if (question is null)
        {
            ModelState.AddModelError(string.Empty, QuestionNotFoundMessage);

            return Page();
        }

        EditingQuestionNumber = question.QuestionNumber;

        var name = ProfileQuestionHtml.CleanTitle(Input.Name);
        var nonTechnicalName = ProfileQuestionHtml.CleanTitle(Input.NonTechnicalName);
        var guidance = ProfileQuestionHtml.CleanGuidance(Input.UserGuidance);

        ValidateTitle(name, NameKey, NameRequiredMessage, NameTooLongMessage);
        ValidateTitle(nonTechnicalName, NonTechnicalNameKey, NonTechnicalNameRequiredMessage, NonTechnicalNameTooLongMessage);

        if (!ProfileQuestionHtml.HasGuidanceContent(guidance))
        {
            ModelState.AddModelError(UserGuidanceKey, GuidanceRequiredMessage);
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await SaveAsync(question.Id, new UpdateProfileQuestionRequest(name, nonTechnicalName, guidance, ReadRowVersion()), cancellationToken);

        // As in legacy, the outcome is reported above the list and the edit panel closes either way.
        if (result.Outcome == ProfileQuestionUpdateOutcome.Success)
        {
            SuccessMessage = SaveSucceededMessage;
        }
        else
        {
            logger.QuestionSaveRejected(question.Id, result.Outcome);
            FailureMessage = SaveFailedPrefix + result.ErrorMessage;
        }

        return RedirectToPage(new { section = Section });
    }

    private void ValidateTitle(string title, string field, string requiredMessage, string tooLongMessage)
    {
        var length = ProfileQuestionHtml.TitleTextLength(title);

        if (length == 0)
        {
            ModelState.AddModelError(field, requiredMessage);
        }
        else if (length > ProfileQuestionHtml.MaxTitleTextLength)
        {
            ModelState.AddModelError(field, tooLongMessage);
        }
    }

    private byte[] ReadRowVersion()
    {
        try
        {
            return Convert.FromBase64String(Input.RowVersion ?? string.Empty);
        }
        catch (FormatException)
        {
            // CDC.Api refuses an empty row version, which is reported as a rejected save.
            return [];
        }
    }

    private async Task<ProfileQuestionUpdateResult> SaveAsync(Guid questionId, UpdateProfileQuestionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await questionsApiService.UpdateProfileQuestionAsync(questionId, request, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.QuestionSaveFailed(exception, questionId);

            return new ProfileQuestionUpdateResult
            {
                Outcome = ProfileQuestionUpdateOutcome.Error,
                ErrorMessage = ProfileQuestionsApiService.ErrorMessage
            };
        }
    }

    private async Task OpenQuestionAsync(Guid questionId, CancellationToken cancellationToken)
    {
        var listed = Questions.FirstOrDefault(candidate => candidate.Id == questionId);

        if (listed is null)
        {
            ModelState.AddModelError(string.Empty, QuestionNotFoundMessage);

            return;
        }

        try
        {
            var question = await questionsApiService.GetProfileQuestionAsync(questionId, cancellationToken);

            if (question is null)
            {
                ModelState.AddModelError(string.Empty, QuestionNotFoundMessage);

                return;
            }

            EditingQuestionNumber = question.QuestionNumber;
            Input = new EditUserGuidanceInput
            {
                Name = question.Name,
                NonTechnicalName = question.NonTechnicalName,
                UserGuidance = ProfileQuestionHtml.ToEditorHtml(question.UserGuidance),
                RowVersion = Convert.ToBase64String(question.LastUpdated)
            };
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.QuestionLoadFailed(exception, questionId);
            ModelState.AddModelError(string.Empty, QuestionLoadFailureMessage);
        }
    }

    private async Task<bool> LoadSectionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var metadata = await sectionsApiService.GetProfileQuestionnaireMetadataAsync(cancellationToken);

            Sections =
            [
                .. metadata.Sections
                    .OrderBy(section => section.SectionNumber)
                    .Select(section => new GuidanceSectionChoice(
                        section.Id,
                        $"{section.SectionNumber.ToString(CultureInfo.InvariantCulture)}: {ProfileSectionTabs.FormatShortName(section.ShortName)}"))
            ];

            // A section that is not one of the profile's is treated as none selected.
            if (Section.HasValue && Sections.All(choice => choice.Id != Section.Value))
            {
                Section = null;
                QuestionId = null;
            }

            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.SectionsLoadFailed(exception);
            ModelState.AddModelError(string.Empty, SectionsLoadFailureMessage);

            return false;
        }
    }

    private async Task<bool> LoadQuestionsAsync(CancellationToken cancellationToken)
    {
        if (!Section.HasValue)
        {
            return true;
        }

        try
        {
            Questions = (await sectionsApiService.GetProfileQuestionInfoListAsync(Section.Value, cancellationToken))
                .OrderBy(question => question.QuestionNumber)
                .ToList();

            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.QuestionsLoadFailed(exception, Section.Value);
            ModelState.AddModelError(string.Empty, QuestionsLoadFailureMessage);

            return false;
        }
    }
}

/// <summary>A profile section the questions can be listed for.</summary>
/// <param name="Id">The section identifier.</param>
/// <param name="Text">The legacy option text, for example <c>1: Disease overview</c>.</param>
public sealed record GuidanceSectionChoice(Guid Id, string Text);

/// <summary>What the user typed for one question.</summary>
public sealed class EditUserGuidanceInput
{
    /// <summary>Gets or sets the question's title, which may hold italic markup.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the question's non-technical title, which may hold italic markup.</summary>
    public string? NonTechnicalName { get; set; }

    /// <summary>Gets or sets the question's guidance, which may hold italics, lists and links.</summary>
    public string? UserGuidance { get; set; }

    /// <summary>Gets or sets the row version of the question as it was opened, as base 64.</summary>
    public string? RowVersion { get; set; }
}
