using CDC.Api.Features.SpellCheck.Dtos;
using CDC.Api.Features.SpellCheck.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Api.Features.SpellCheck;

/// <summary>
/// The spell check's custom dictionary: the words that the spell check on the profile text fields accepts in addition to its
/// English dictionary, replacing the legacy <c>SpellCheckService.svc</c> WCF service.
/// </summary>
/// <param name="spellCheckService">Custom dictionary service.</param>
[ApiController]
[Route("api/spell-check")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class SpellCheckController(ISpellCheckService spellCheckService) : ControllerBase
{
    /// <summary>
    /// Gets every word in the custom dictionary.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The words were retrieved.</response>
    /// <response code="403">The request names no known user.</response>
    [HttpGet("custom-words")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetCustomWords(CancellationToken cancellationToken)
    {
        var words = await spellCheckService.GetCustomWordsAsync(cancellationToken);

        return Ok(words);
    }

    /// <summary>
    /// Adds a word to the custom dictionary, unless it is there already.
    /// </summary>
    /// <param name="request">The word to add.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The word is in the custom dictionary.</response>
    /// <response code="400">The word is empty, too long, or more than one word.</response>
    /// <response code="403">The current user is not a profile editor, contributor or reviewer.</response>
    [HttpPost("custom-words")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddCustomWord(
        [FromBody] AddCustomWordRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await spellCheckService.AddCustomWordAsync(request.Word, cancellationToken);

        return result.Outcome switch
        {
            AddCustomWordOutcome.Success => NoContent(),
            AddCustomWordOutcome.Forbidden => Problem(result.ErrorMessage, statusCode: StatusCodes.Status403Forbidden),
            _ => Problem(result.ErrorMessage, statusCode: StatusCodes.Status400BadRequest)
        };
    }
}
