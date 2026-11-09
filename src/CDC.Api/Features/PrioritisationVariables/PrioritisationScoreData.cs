namespace CDC.Api.Features.PrioritisationVariables;

/// <summary>A common question shown on every score data export (result set 1 of <c>spgaPrioritisationScoreData</c>).</summary>
/// <param name="Id">The profile question identifier.</param>
/// <param name="DisplayQuestionNumber">The question number as <c>section.question</c>.</param>
/// <param name="Name">The question name.</param>
public sealed record ScoreDataCommonQuestion(Guid Id, string DisplayQuestionNumber, string Name);

/// <summary>A prioritisation category (result set 2).</summary>
/// <param name="Id">The category identifier.</param>
/// <param name="Name">The category name.</param>
public sealed record ScoreDataCategory(Guid Id, string Name);

/// <summary>A profile question mapped to a criterion in a category (result set 3).</summary>
/// <param name="Id">The profile question identifier.</param>
/// <param name="CategoryId">The category the criterion belongs to.</param>
/// <param name="DisplayQuestionNumber">The question number as <c>section.question</c>.</param>
/// <param name="Name">The question name.</param>
/// <param name="IsPerSpecies">Whether the question is answered once per species.</param>
public sealed record ScoreDataPrioritisationQuestion(Guid Id, Guid CategoryId, string DisplayQuestionNumber, string Name, bool IsPerSpecies);

/// <summary>A root profile (result set 4).</summary>
/// <param name="Id">The profile identifier.</param>
/// <param name="Title">The profile title.</param>
/// <param name="StatusName">The profile status name.</param>
public sealed record ScoreDataProfile(Guid Id, string Title, string StatusName);

/// <summary>A current profile version (result set 5). Scenario versions carry their parent profile's id.</summary>
/// <param name="Id">The profile version identifier.</param>
/// <param name="ProfileId">The root profile identifier.</param>
/// <param name="ScenarioTitle">The scenario title, or "Current situation".</param>
/// <param name="DisplayVersionNumber">The version number as <c>vMajor.Minor</c>.</param>
/// <param name="State">The version state, Draft or Published.</param>
/// <param name="IsScenario">Whether the version belongs to a scenario.</param>
public sealed record ScoreDataProfileVersion(Guid Id, Guid ProfileId, string ScenarioTitle, string DisplayVersionNumber, string State, bool IsScenario);

/// <summary>A profiled species of a profile version (result set 6).</summary>
/// <param name="SpeciesId">The species identifier.</param>
/// <param name="ProfileVersionId">The profile version identifier.</param>
/// <param name="Name">The species name.</param>
public sealed record ScoreDataProfiledSpecies(Guid SpeciesId, Guid ProfileVersionId, string Name);

/// <summary>An answer to a common question (result set 7).</summary>
/// <param name="QuestionId">The profile question identifier.</param>
/// <param name="ProfileVersionId">The profile version identifier.</param>
/// <param name="Answer">The answer text.</param>
public sealed record ScoreDataCommonAnswer(Guid QuestionId, Guid ProfileVersionId, string Answer);

/// <summary>An answer to a prioritisation question with its score (result set 8).</summary>
/// <param name="QuestionId">The profile question identifier.</param>
/// <param name="ProfileVersionId">The profile version identifier.</param>
/// <param name="SpeciesId">The species, or <see cref="Guid.Empty"/> when the question is not per species.</param>
/// <param name="Answer">The answer text.</param>
/// <param name="Score">The criterion value score.</param>
/// <param name="Weighting">The criterion weighting.</param>
public sealed record ScoreDataPrioritisationAnswer(Guid QuestionId, Guid ProfileVersionId, Guid SpeciesId, string Answer, int Score, int Weighting)
{
    /// <summary>Gets the score multiplied by the weighting.</summary>
    public int WeightedScore => Score * Weighting;
}

/// <summary>A normalised prioritisation score (result set 9).</summary>
/// <param name="ProfileVersionId">The profile version identifier.</param>
/// <param name="SpeciesId">The species identifier.</param>
/// <param name="CategoryId">The category identifier.</param>
/// <param name="Score">The raw score.</param>
/// <param name="Rank">The normalised score.</param>
public sealed record ScoreDataNormalisedScore(Guid ProfileVersionId, Guid SpeciesId, Guid CategoryId, int Score, int Rank);

/// <summary>
/// Everything <c>spgaPrioritisationScoreData</c> returns, in procedure order. Replaces the legacy <c>ScoreData</c> object graph.
/// </summary>
public sealed record PrioritisationScoreData
{
    /// <summary>Gets the common questions, in section/question order.</summary>
    public IReadOnlyList<ScoreDataCommonQuestion> CommonQuestions { get; init; } = [];

    /// <summary>Gets the categories, in sequence order.</summary>
    public IReadOnlyList<ScoreDataCategory> Categories { get; init; } = [];

    /// <summary>Gets the prioritisation questions, in section/question order.</summary>
    public IReadOnlyList<ScoreDataPrioritisationQuestion> PrioritisationQuestions { get; init; } = [];

    /// <summary>Gets the root profiles, in title order.</summary>
    public IReadOnlyList<ScoreDataProfile> Profiles { get; init; } = [];

    /// <summary>Gets the current profile versions.</summary>
    public IReadOnlyList<ScoreDataProfileVersion> ProfileVersions { get; init; } = [];

    /// <summary>Gets the profiled species of every current version, in species name order.</summary>
    public IReadOnlyList<ScoreDataProfiledSpecies> ProfiledSpecies { get; init; } = [];

    /// <summary>Gets the common question answers.</summary>
    public IReadOnlyList<ScoreDataCommonAnswer> CommonAnswers { get; init; } = [];

    /// <summary>Gets the prioritisation question answers.</summary>
    public IReadOnlyList<ScoreDataPrioritisationAnswer> PrioritisationAnswers { get; init; } = [];

    /// <summary>Gets the normalised scores.</summary>
    public IReadOnlyList<ScoreDataNormalisedScore> NormalisedScores { get; init; } = [];
}

/// <summary>A CSV file built from the score data.</summary>
/// <param name="FileName">The download file name.</param>
/// <param name="Content">The UTF-8 encoded CSV, with a byte order mark so Excel detects the encoding.</param>
public sealed record PrioritisationCsvExport(string FileName, byte[] Content);
