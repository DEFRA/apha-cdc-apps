using CDC.Common.Contracts;

namespace CDC.Web.Models;

/// <summary>Full detail for one disease ranking filter, as returned by CDC.Api.</summary>
public sealed record DiseaseRankingFilterDetailDto : DiseaseRankingFilterDetailContract; // NOSONAR

/// <summary>Wire contract for creating or updating a disease ranking filter on CDC.Api.</summary>
public sealed record SaveDiseaseRankingFilterRequestDto : SaveDiseaseRankingFilterRequestContract; // NOSONAR

/// <summary>Outcome of a call to save a disease ranking filter, distinguishing failure kinds the page reacts to differently.</summary>
public enum DiseaseRankingFilterSaveOutcome
{
    /// <summary>The filter was saved.</summary>
    Success,

    /// <summary>The request failed validation.</summary>
    ValidationFailed,

    /// <summary>A filter with this name already exists.</summary>
    DuplicateName,

    /// <summary>The call to CDC.Api failed unexpectedly.</summary>
    Error
}

/// <summary>Result of a call to create or update a disease ranking filter.</summary>
public sealed record SaveDiseaseRankingFilterResult
{
    /// <summary>Gets the outcome classification.</summary>
    public required DiseaseRankingFilterSaveOutcome Outcome { get; init; }

    /// <summary>Gets a message to show the user when <see cref="Outcome"/> is not <see cref="DiseaseRankingFilterSaveOutcome.Success"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the saved filter's number on success.</summary>
    public long FilterNumber { get; init; }
}
