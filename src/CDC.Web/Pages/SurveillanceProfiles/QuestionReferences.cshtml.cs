using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// The "Add/Select a reference to question" dialog of the Questions tab, which the legacy <c>EditProfileQuestions.aspx</c> opened from
/// the edit references icon of a question. A user who may edit the section adds a new reference of a type, or selects one of the
/// profile version's existing references, and applies it to the question and any others of the section. It has no page of its own:
/// the Questions tab's dialog reads the references of a type and posts the choice here, and is answered in JSON.
/// </summary>
/// <param name="apiClient">Typed client for the profile management endpoints on CDC.Api.</param>
/// <param name="profileSectionsApiService">Typed client for the profile sections endpoints on CDC.Api.</param>
/// <param name="logger">Structured logger.</param>
public class QuestionReferencesModel(
    IApiClient apiClient,
    IProfileSectionsApiService profileSectionsApiService,
    ILogger<QuestionReferencesModel> logger) : ProfileSectionTabPageModel(apiClient, profileSectionsApiService, logger)
{
    /// <summary>The action chosen to add a new reference.</summary>
    public const string AddMode = "add";

    /// <summary>The action chosen to select an existing reference.</summary>
    public const string SelectMode = "select";

    /// <summary>What the user is told when the reference was saved, as the legacy page worded it.</summary>
    public const string SavedMessage = "Reference was saved successfully";

    /// <summary>The start of what the user is told when the reference was not saved, as the legacy dialog worded it; the reason follows.</summary>
    public const string SaveFailedMessage = "Saving reference was unsuccessful: ";

    /// <summary>What is wrong when no action was chosen.</summary>
    public const string ActionRequiredMessage = "Select an action";

    /// <summary>What is wrong when no type of reference was chosen.</summary>
    public const string NoteTypeRequiredMessage = "Select a reference type";

    /// <summary>What is wrong when a new reference has no text.</summary>
    public const string NoteTextRequiredMessage = "Enter the reference text";

    /// <summary>What is wrong when no existing reference was selected.</summary>
    public const string NoteRequiredMessage = "Select a reference";

    /// <summary>The reason given when CDC.Api could not be asked, or did not say why it refused.</summary>
    public const string RequestFailedReason = "We could not complete the request. Try again later.";

    /// <summary>The reason given when the question is not one of the section's.</summary>
    public const string QuestionNotFoundReason = "The question could not be found. Close this window and try again.";

    /// <summary>The reason given when the selected reference is no longer there, or is not identified well enough to change.</summary>
    public const string NoteNotFoundReason = "The reference could not be found. Close this window and try again.";

    /// <inheritdoc />
    public override ProfileSectionTab ActiveTab => ProfileSectionTab.Questions;

    /// <summary>Gets or sets what the dialog posted.</summary>
    [BindProperty]
    public QuestionReferenceInput Input { get; set; } = new();

    /// <summary>Reads the profile version's references of one type, for the dialog's "Select a reference" list.</summary>
    /// <param name="noteTypeId">The type of reference, from the query string.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The references in alphabetical order as JSON; or the status that says why they cannot be read.</returns>
    public async Task<IActionResult> OnGetNotesAsync(Guid noteTypeId, CancellationToken cancellationToken)
    {
        var refused = await RefuseAsync(cancellationToken);

        if (refused is not null)
        {
            return refused;
        }

        try
        {
            var notes = await ProfileSectionsApi.GetProfileNotesByVersionAsync(CurrentProfileVersionId, noteTypeId, cancellationToken);
            var sectionId = CurrentSection!.Id;

            return new JsonResult(
                notes
                    .OrderBy(note => ProfileTitleHtmlFormatter.ToPlainText(note.NoteText), StringComparer.OrdinalIgnoreCase)
                    .Select(note => new QuestionReferenceNoteOption(
                        note.Id,
                        ProfileNoteDisplayHelper.ToSlimText(note.NoteText),
                        ReviewCommentHtml.Sanitize(note.NoteText),
                        Convert.ToBase64String(note.LastUpdated),
                        [.. note.QuestionReferences.Where(reference => reference.ProfileSectionId == sectionId).Select(reference => reference.ProfileQuestionId)]))
                    .ToList());
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            Logger.ReferenceNotesLoadFailed(exception, ProfileId, noteTypeId);

            return StatusCode(StatusCodes.Status502BadGateway);
        }
    }

    /// <summary>
    /// Saves the reference the dialog posted, as the legacy <c>btnSaveNote_Click</c> did: a new reference, or the selected existing
    /// one, is applied to the question and the others picked, and an existing reference loses the questions of the section that were not.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Whether it was saved, and what to tell the user, as JSON; or the status that says why it cannot be.</returns>
    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        var refused = await RefuseAsync(cancellationToken);

        if (refused is not null)
        {
            return refused;
        }

        var section = CurrentSection!;
        Input.NoteText = ReviewCommentHtml.Sanitize(Input.NoteText);

        var errors = Validate();

        if (errors.Count > 0)
        {
            return Failed(errors);
        }

        if (section.Questions.All(question => question.Id != Input.QuestionId))
        {
            return Failed([SaveFailedMessage + QuestionNotFoundReason]);
        }

        // The legacy dialog always applied the reference to the question it was opened for, as well as those picked.
        var applied = Input.QuestionIds
            .Where(questionId => section.Questions.Any(question => question.Id == questionId))
            .Append(Input.QuestionId)
            .Distinct()
            .ToList();
        var noteTypeId = Input.NoteTypeId!.Value;

        try
        {
            var result = Input.Mode == AddMode
                ? await AddAsync(noteTypeId, section.Id, applied, cancellationToken)
                : await SelectAsync(noteTypeId, section.Id, applied, cancellationToken);

            if (result.Outcome == ProfileNoteChangeOutcome.Success)
            {
                return new JsonResult(new QuestionReferenceSaveResponse(true, SavedMessage, []));
            }

            if (result.Outcome != ProfileNoteChangeOutcome.Error)
            {
                Logger.ReferenceSaveRefused(ProfileId, result.Outcome);
            }

            return Failed([SaveFailedMessage + (result.ErrorMessage ?? RequestFailedReason)]);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            Logger.ReferenceSaveFailed(exception, ProfileId, noteTypeId);

            return Failed([SaveFailedMessage + RequestFailedReason]);
        }
    }

    private static JsonResult Failed(IEnumerable<string> errors) => new(new QuestionReferenceSaveResponse(false, null, [.. errors]));

    private static ProfileNoteChangeResult Refused(string reason) =>
        new() { Outcome = ProfileNoteChangeOutcome.ValidationFailed, ErrorMessage = reason };

    /// <summary>Loads the profile and says why the request cannot be answered, when it cannot: JSON is wanted, so a profile the user
    /// may not read is a 403 rather than the Application error page the other tabs send them to.</summary>
    private async Task<IActionResult?> RefuseAsync(CancellationToken cancellationToken)
    {
        var refused = await LoadProfileContextAsync(cancellationToken);

        if (refused is NotFoundResult)
        {
            return refused;
        }

        if (refused is not null)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        if (HasError)
        {
            return StatusCode(StatusCodes.Status502BadGateway);
        }

        if (!IsReady)
        {
            return NotFound();
        }

        return CanEditProfileSection ? null : StatusCode(StatusCodes.Status403Forbidden);
    }

    /// <summary>Applies the legacy dialog's rules, which only asked about a field that its action made visible and its earlier choices
    /// had revealed: with no action nothing else is asked, a new reference needs text once its type is chosen, and an existing one
    /// needs selecting.</summary>
    private List<string> Validate()
    {
        var isAdd = Input.Mode == AddMode;

        if (!isAdd && Input.Mode != SelectMode)
        {
            return [ActionRequiredMessage];
        }

        var errors = new List<string>();
        var hasType = Input.NoteTypeId is { } noteTypeId && noteTypeId != Guid.Empty;

        if (!hasType)
        {
            errors.Add(NoteTypeRequiredMessage);
        }

        if (isAdd)
        {
            if (hasType && string.IsNullOrWhiteSpace(ProfileTitleHtmlFormatter.ToPlainText(Input.NoteText)))
            {
                errors.Add(NoteTextRequiredMessage);
            }
        }
        else if (Input.NoteId is null || Input.NoteId == Guid.Empty)
        {
            errors.Add(NoteRequiredMessage);
        }

        return errors;
    }

    private async Task<ProfileNoteChangeResult> AddAsync(Guid noteTypeId, Guid sectionId, IReadOnlyList<Guid> questionIds, CancellationToken cancellationToken)
    {
        var insert = ProfileNoteChangeBuilder.BuildInsert(Guid.NewGuid(), Input.NoteText, sectionId, questionIds);

        return await ProfileSectionsApi.UpdateProfileNotesAsync(CurrentProfileVersionId, noteTypeId, [insert], [], cancellationToken);
    }

    private async Task<ProfileNoteChangeResult> SelectAsync(Guid noteTypeId, Guid sectionId, IReadOnlyList<Guid> questionIds, CancellationToken cancellationToken)
    {
        var notes = await ProfileSectionsApi.GetProfileNotesByVersionAsync(CurrentProfileVersionId, noteTypeId, cancellationToken);
        var note = notes.FirstOrDefault(candidate => candidate.Id == Input.NoteId);
        var rowVersion = ProfileNoteChangeBuilder.DecodeRowVersion(Input.LastUpdated);

        if (note is null || rowVersion is null)
        {
            return Refused(NoteNotFoundReason);
        }

        // Nothing to save when the reference already has exactly these links, as the legacy list had no dirty note then.
        var update = ProfileNoteChangeBuilder.BuildUpdate(note, rowVersion, sectionId, questionIds);

        return update is null
            ? new ProfileNoteChangeResult { Outcome = ProfileNoteChangeOutcome.Success }
            : await ProfileSectionsApi.UpdateProfileNotesAsync(CurrentProfileVersionId, noteTypeId, [], [update], cancellationToken);
    }
}
