namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Names of the Surveillance Profiles stored procedures backing the spell check's custom dictionary (the
/// <c>SpellCustomWord</c> table). The legacy <c>spuSpellCustomWord</c> and <c>spdSpellCustomWord</c> procedures are not used:
/// the dialog's "Add Custom" button only adds words.
/// </summary>
public static class SpellCheckStoredProcedures
{
    /// <summary>Reads every word in the custom dictionary: <c>Id</c>, <c>Word</c>, <c>LastUpdated</c>.</summary>
    public const string GetCustomWords = "spgaSpellCustomWord";

    /// <summary>Adds a word to the custom dictionary and outputs its row version.</summary>
    public const string InsertCustomWord = "spiSpellCustomWord";
}
