using CDC.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// The guidance for one profile question, as the legacy <c>ProfileQuestionHelp.aspx</c> showed it in a pop-up window: the question's
/// name, then its guidance. It is the fragment the "User guidance" dialog of the Questions tab loads when a question's help icon is used,
/// so it has no page around it.
/// </summary>
/// <param name="questionsApiService">Typed client for the profile question endpoints on CDC.Api.</param>
/// <param name="logger">Structured logger.</param>
public class ProfileQuestionHelpModel(
    IProfileQuestionsApiService questionsApiService,
    ILogger<ProfileQuestionHelpModel> logger) : PageModel
{
    /// <summary>Gets the question's name, which may hold italic markup.</summary>
    public string QuestionName { get; private set; } = string.Empty;

    /// <summary>Gets the question's guidance as HTML that is safe to show: text formatting and safe links only.</summary>
    public string GuidanceHtml { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether the question has any guidance to show.</summary>
    public bool HasGuidance => ProfileQuestionHtml.HasGuidanceContent(GuidanceHtml);

    /// <summary>Reads the question and its guidance from CDC.Api.</summary>
    /// <param name="profileQuestionId">The question to show the guidance of, from the query string.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The fragment; not found when there is no such question; or a bad gateway when CDC.Api cannot be reached.</returns>
    public async Task<IActionResult> OnGetAsync(Guid profileQuestionId, CancellationToken cancellationToken)
    {
        try
        {
            var question = await questionsApiService.GetProfileQuestionAsync(profileQuestionId, cancellationToken);

            if (question is null)
            {
                return NotFound();
            }

            QuestionName = question.Name;
            GuidanceHtml = ReviewCommentHtml.ToDisplayHtml(question.UserGuidance);

            return Page();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.QuestionHelpLoadFailed(exception, profileQuestionId);

            return StatusCode(StatusCodes.Status502BadGateway);
        }
    }
}
