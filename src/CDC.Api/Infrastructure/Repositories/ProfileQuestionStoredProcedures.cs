namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Names of the Surveillance Profiles stored procedures backing the profile questions feature.
/// </summary>
public static class ProfileQuestionStoredProcedures
{
    /// <summary>Reads one question.</summary>
    public const string GetProfileQuestion = "spgProfileQuestion";

    /// <summary>Reads the questions within one profile section.</summary>
    public const string GetProfileQuestionBySectionId = "spgProfileQuestionBySectionId";

    /// <summary>
    /// Updates a question's guidance text and display names. Checks <c>@LastUpdated</c> for
    /// optimistic concurrency and outputs the new row version. Does not accept
    /// <c>ShortName</c> or <c>QuestionNumber</c> - neither did the legacy caller.
    /// </summary>
    public const string UpdateProfileQuestion = "spuProfileQuestion";

    /// <summary>
    /// Reads the prioritisation categories shown on the summary prioritisation report (first result set) and the
    /// criteria scored within them, with the question that explains each (second result set).
    /// </summary>
    public const string GetPrioritisationGuidanceData = "spgaPrioritisationGuidanceData";
}
