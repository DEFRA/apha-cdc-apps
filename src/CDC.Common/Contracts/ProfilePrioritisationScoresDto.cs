namespace CDC.Common.Contracts;

/// <summary>
/// The prioritisation scores the summary prioritisation report prints for one profile version: each
/// species' rank in every category, the criterion values behind them, and the prioritised species.
/// Returned by <c>GET /api/profile-reports/prioritisation/{profileVersionId}</c>.
/// </summary>
public sealed record ProfilePrioritisationScoresDto
{
    /// <summary>Gets each species' score and rank per prioritisation category.</summary>
    public IReadOnlyList<ProfileCategoryScoreDto> CategoryScores { get; init; } = [];

    /// <summary>Gets each species' value and score per prioritisation criterion.</summary>
    public IReadOnlyList<ProfileCriterionScoreDto> CriterionScores { get; init; } = [];

    /// <summary>Gets the species that were prioritised, in display order.</summary>
    public IReadOnlyList<ProfilePrioritisedSpeciesDto> Species { get; init; } = [];
}

/// <summary>One species' score in one prioritisation category.</summary>
public sealed record ProfileCategoryScoreDto
{
    /// <summary>Gets the category identifier.</summary>
    public Guid CategoryId { get; init; }

    /// <summary>Gets the category name, for example "Public health".</summary>
    public string CategoryName { get; init; } = string.Empty;

    /// <summary>Gets the species identifier.</summary>
    public Guid SpeciesId { get; init; }

    /// <summary>Gets the species name; may contain HTML.</summary>
    public string SpeciesName { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the data needed to score the species is incomplete.</summary>
    public bool DataIncomplete { get; init; }

    /// <summary>Gets the species' score in the category.</summary>
    public int Score { get; init; }

    /// <summary>Gets the species' rank in the category.</summary>
    public int Rank { get; init; }

    /// <summary>Gets a value indicating whether the species is one the profile profiles, rather than only affected.</summary>
    public bool IsProfiledSpecies { get; init; }
}

/// <summary>One species' value for one prioritisation criterion.</summary>
public sealed record ProfileCriterionScoreDto
{
    /// <summary>Gets the category identifier.</summary>
    public Guid CategoryId { get; init; }

    /// <summary>Gets the category name, for example "Welfare".</summary>
    public string CategoryName { get; init; } = string.Empty;

    /// <summary>Gets the category's display name; may contain HTML.</summary>
    public string CategoryDisplayName { get; init; } = string.Empty;

    /// <summary>Gets the criterion identifier.</summary>
    public Guid CriterionId { get; init; }

    /// <summary>Gets the criterion code.</summary>
    public string CriterionCode { get; init; } = string.Empty;

    /// <summary>Gets the criterion name; may contain HTML.</summary>
    public string CriterionName { get; init; } = string.Empty;

    /// <summary>Gets the species identifier.</summary>
    public Guid SpeciesId { get; init; }

    /// <summary>Gets the species name; may contain HTML.</summary>
    public string SpeciesName { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the data behind the value is incomplete.</summary>
    public bool DataIncomplete { get; init; }

    /// <summary>Gets a value indicating whether the criterion's category is irrelevant for the species.</summary>
    public bool IrrelevantCategory { get; init; }

    /// <summary>Gets the criterion value shown in the report; may contain HTML.</summary>
    public string CriterionValue { get; init; } = string.Empty;

    /// <summary>Gets the criterion score.</summary>
    public int Score { get; init; }
}

/// <summary>A species that was prioritised.</summary>
public sealed record ProfilePrioritisedSpeciesDto
{
    /// <summary>Gets the species identifier.</summary>
    public Guid SpeciesId { get; init; }

    /// <summary>Gets the species name; may contain HTML.</summary>
    public string Name { get; init; } = string.Empty;
}
