namespace CDC.Web.Models;

/// <summary>How the search text is matched against a profile. Mirrors the API's
/// <c>SearchForType</c> enum, and the legacy <c>SearchForType</c> enum before it.</summary>
public enum SearchForType
{
    /// <summary>Match the whole search text as one word or phrase.</summary>
    ExactWordOrPhrase = 0,

    /// <summary>Match every space-separated word in the search text, in any order.</summary>
    AllWords = 1
}
