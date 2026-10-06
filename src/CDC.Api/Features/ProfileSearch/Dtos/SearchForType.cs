namespace CDC.Api.Features.ProfileSearch.Dtos;

/// <summary>How the search text is matched against a profile. Mirrors the legacy
/// <c>SearchForType</c> enum in <c>ProfilesLibrary/ProfileSearching/SearchForTypeEnum.vb</c>.</summary>
public enum SearchForType
{
    /// <summary>Match the whole search text as one word or phrase.</summary>
    ExactWordOrPhrase = 0,

    /// <summary>Match every space-separated word in the search text, in any order.</summary>
    AllWords = 1
}
