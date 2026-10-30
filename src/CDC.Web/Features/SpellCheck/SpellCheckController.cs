using CDC.Web.Infrastructure.SpellCheck;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Features.SpellCheck;

/// <summary>
/// Backs the "Spell check" dialog (<c>wwwroot/js/spell-check.js</c>, <c>_SpellCheckDialog.cshtml</c>) that opens from the Spell
/// check button of a text field: finds the misspelt words, suggests corrections for one, and adds a word to the custom
/// dictionary.
/// </summary>
/// <remarks>
/// Every action is for a signed-in user, as the fallback authorization policy requires: the dictionary is the same for all of
/// them, and CDC.Api decides who may add to the custom dictionary.
/// </remarks>
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class SpellCheckController(ISpellCheckService spellCheck, IAntiforgery antiforgery) : Controller
{
    /// <summary>The most words one request may ask about: far more than the longest text a field holds.</summary>
    internal const int MaxWords = 5000;

    /// <summary>The largest request body that is read.</summary>
    internal const int MaxRequestBytes = 512 * 1024;

    /// <summary>The longest word that is given suggestions or added to the custom dictionary, which is as long as the table holds.</summary>
    internal const int MaxWordLength = 255;

    /// <summary>Finds which of the words of a text are misspelt.</summary>
    /// <param name="request">The words.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The misspelt words as JSON; 400 without a valid antiforgery token, or when the request is not a list of words.</returns>
    [HttpPost]
    [RequestSizeLimit(MaxRequestBytes)]
    public async Task<IActionResult> Check([FromBody] CheckWordsRequest? request, CancellationToken cancellationToken)
    {
        // Checked here, not by attribute: any antiforgery attribute makes CodeQL flag every [HttpPost] in the solution, CDC.Api's included.
        if (!await antiforgery.IsRequestValidAsync(HttpContext))
        {
            return BadRequest();
        }

        if (request?.Words is not { Count: <= MaxWords } words || words.Any(word => word is null))
        {
            return BadRequest();
        }

        var misspelt = await spellCheck.FindMisspeltWordsAsync(words, cancellationToken);

        return Json(new CheckWordsResponse(misspelt));
    }

    /// <summary>Suggests corrections for a misspelt word.</summary>
    /// <param name="request">The word.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The suggestions as JSON; 400 without a valid antiforgery token, or when there is no word.</returns>
    [HttpPost]
    public async Task<IActionResult> Suggest([FromBody] SuggestWordRequest? request, CancellationToken cancellationToken)
    {
        if (!await antiforgery.IsRequestValidAsync(HttpContext) || request?.Word is not { } word || !IsWord(word))
        {
            return BadRequest();
        }

        var suggestions = await spellCheck.SuggestAsync(word, cancellationToken);

        return Json(new SuggestWordResponse(suggestions));
    }

    /// <summary>Adds a word to the custom dictionary.</summary>
    /// <param name="request">The word.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// 204 once the word is in the dictionary; 400 without a valid antiforgery token, or for a word CDC.Api refuses; 403 for a user
    /// who may not add words; 502 when CDC.Api could not be asked.
    /// </returns>
    [HttpPost]
    public async Task<IActionResult> AddCustomWord([FromBody] AddCustomWordRequest? request, CancellationToken cancellationToken)
    {
        if (!await antiforgery.IsRequestValidAsync(HttpContext) || request?.Word is not { } word || !IsWord(word))
        {
            return BadRequest();
        }

        var outcome = await spellCheck.AddCustomWordAsync(word, cancellationToken);

        return outcome switch
        {
            AddCustomWordOutcome.Added => NoContent(),
            AddCustomWordOutcome.Forbidden => StatusCode(StatusCodes.Status403Forbidden),
            AddCustomWordOutcome.Invalid => BadRequest(),
            _ => StatusCode(StatusCodes.Status502BadGateway)
        };
    }

    private static bool IsWord(string word) => !string.IsNullOrWhiteSpace(word) && word.Length <= MaxWordLength;
}
