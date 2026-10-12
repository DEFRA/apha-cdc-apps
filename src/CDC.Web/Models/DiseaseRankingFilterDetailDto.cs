namespace CDC.Web.Models;

/// <summary>Full detail for one disease ranking filter, as returned by CDC.Api.</summary>
public sealed record DiseaseRankingFilterDetailDto
{
    /// <summary>Gets the filter's identifying number.</summary>
    public long FilterNumber { get; init; }

    /// <summary>Gets the filter's display name.</summary>
    public string DiseaseName { get; init; } = string.Empty;

    /// <summary>Gets the endemic/exotic/both criterion.</summary>
    public string Disease1 { get; init; } = string.Empty;

    /// <summary>Gets the zoonotic/non-zoonotic/both criterion.</summary>
    public string Disease2 { get; init; } = string.Empty;

    /// <summary>Gets the notifiable/non-notifiable/both criterion.</summary>
    public string Disease3 { get; init; } = string.Empty;

    /// <summary>Gets the infectious/non-infectious/both criterion.</summary>
    public string Disease4 { get; init; } = string.Empty;

    /// <summary>Gets the selected transmissible agent types.</summary>
    public IReadOnlyList<string> DiseaseTypes { get; init; } = [];

    /// <summary>Gets every species selected for this filter.</summary>
    public IReadOnlyList<Guid> SpeciesIds { get; init; } = [];
}

/// <summary>Wire contract for creating or updating a disease ranking filter on CDC.Api.</summary>
public sealed record SaveDiseaseRankingFilterRequestDto
{
    /// <summary>Gets the filter's display name.</summary>
    public string DiseaseName { get; init; } = string.Empty;

    /// <summary>Gets the endemic/exotic/both criterion.</summary>
    public string Disease1 { get; init; } = string.Empty;

    /// <summary>Gets the zoonotic/non-zoonotic/both criterion.</summary>
    public string Disease2 { get; init; } = string.Empty;

    /// <summary>Gets the notifiable/non-notifiable/both criterion.</summary>
    public string Disease3 { get; init; } = string.Empty;

    /// <summary>Gets the infectious/non-infectious/both criterion.</summary>
    public string Disease4 { get; init; } = string.Empty;

    /// <summary>Gets the selected transmissible agent types.</summary>
    public IReadOnlyList<string> DiseaseTypes { get; init; } = [];

    /// <summary>Gets every species selected for this filter. Must contain at least one id.</summary>
    public IReadOnlyList<Guid> SpeciesIds { get; init; } = [];
}

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
