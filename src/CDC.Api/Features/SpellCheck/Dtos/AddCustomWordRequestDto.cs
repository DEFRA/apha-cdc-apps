namespace CDC.Api.Features.SpellCheck.Dtos;

/// <summary>
/// Request body for adding a word to the spell check's custom dictionary.
/// </summary>
public sealed record AddCustomWordRequestDto
{
    /// <summary>Gets the word to add. It is trimmed, and must be a single word of no more than 255 characters.</summary>
    public string Word { get; init; } = string.Empty;
}

/// <summary>Outcome of <see cref="Interfaces.ISpellCheckService.AddCustomWordAsync"/>.</summary>
public enum AddCustomWordOutcome
{
    /// <summary>The word is in the custom dictionary: it was added, or was there already.</summary>
    Success,

    /// <summary>The caller is not permitted to add words to the custom dictionary.</summary>
    Forbidden,

    /// <summary>The word is empty, too long, or is more than one word.</summary>
    ValidationFailed
}

/// <summary>Result of attempting to add a word to the custom dictionary.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="ErrorMessage">Why the word was refused, when it was.</param>
public sealed record AddCustomWordResult(AddCustomWordOutcome Outcome, string? ErrorMessage);
