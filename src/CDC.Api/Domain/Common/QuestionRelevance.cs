using CDC.Api.Domain.Entities;

namespace CDC.Api.Domain.Common;

/// <summary>
/// Decides whether a profile question applies, replicating how the legacy <c>SectionBase.CalculateRelevance</c> did:
/// the answer to the question's controlling field decides, and an unanswered controlling field makes the question
/// irrelevant. Shared by the report builder and the section validation, which both need the same answer.
/// </summary>
internal static class QuestionRelevance
{
    /// <summary>Works out whether <paramref name="question"/> is relevant, optionally for one species.</summary>
    /// <param name="question">The question to test.</param>
    /// <param name="speciesId">The species a per-species question is being answered for, or <see langword="null"/>.</param>
    /// <param name="valuesByField">Every recorded value in the profile version, across all sections, by field, because a
    /// field in one section can control the relevance of a question in another.</param>
    /// <returns><see langword="true"/> when the question applies.</returns>
    public static bool IsRelevant(ReportQuestion question, Guid? speciesId, ILookup<Guid, ReportFieldValue> valuesByField)
    {
        if (question.IrrelevanceFieldId == Guid.Empty)
        {
            return true;
        }

        var controlling = valuesByField[question.IrrelevanceFieldId].ToList();
        var answer = speciesId is { } id
            ? controlling.FirstOrDefault(value => value.SpeciesId == id) ?? controlling.FirstOrDefault(value => value.SpeciesId is null)
            : controlling.FirstOrDefault(value => value.SpeciesId is null) ?? controlling.FirstOrDefault();

        if (answer is null)
        {
            return false;
        }

        if (question.IrrelevanceValueId != Guid.Empty)
        {
            if (answer.ListValue is not { } selected || selected == Guid.Empty)
            {
                return false;
            }

            return question.IrrelevanceFlag ? selected == question.IrrelevanceValueId : selected != question.IrrelevanceValueId;
        }

        return answer.BooleanValue is { } flag && flag != question.IrrelevanceFlag;
    }
}
