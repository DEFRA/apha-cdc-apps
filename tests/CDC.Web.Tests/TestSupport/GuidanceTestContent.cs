using CDC.Common.Contracts;
using CDC.Web.Infrastructure.Reports;

namespace CDC.Web.Tests.TestSupport;

/// <summary>Builds guidance content with every question the four guidance reports print, so a layout test can name any of them.</summary>
internal static class GuidanceTestContent
{
    /// <summary>The number of sections the content holds.</summary>
    public const int SectionCount = 16;

    /// <summary>The number of questions in each section.</summary>
    public const int QuestionsPerSection = 25;

    /// <summary>Gets the identifier of a question.</summary>
    public static Guid QuestionId(int section, int question) =>
        section == 2 && question == 2 ? SprGuidanceReportLayout.EndemicQuestionId : new Guid(section, (short)question, 0, [0, 0, 0, 0, 0, 0, 0, 1]);

    /// <summary>Builds the content: sections 1 to 16 listed out of order, each with questions 1 to 25, and two prioritisation categories.</summary>
    public static ProfileGuidanceContentDto Build(IReadOnlyList<PrioritisationGuidanceCategoryDto>? categories = null) => new()
    {
        Sections =
        [
            .. Enumerable.Range(1, SectionCount).Reverse().Select(section => new ProfileGuidanceSectionDto
            {
                Id = new Guid(section, 0, 0, [0, 0, 0, 0, 0, 0, 0, 2]),
                SectionNumber = section,
                Name = $"Section {section}",
                Questions =
                [
                    .. Enumerable.Range(1, QuestionsPerSection).Reverse().Select(question => new ProfileGuidanceQuestionDto
                    {
                        Id = QuestionId(section, question),
                        QuestionNumber = question,
                        Name = $"Q{section}.{question} <i>name</i>",
                        NonTechnicalName = $"Plain {section}.{question}",
                        UserGuidance = $"<p>Guidance {section}.{question}</p>"
                    })
                ]
            })
        ],
        PrioritisationCategories = categories ??
        [
            new PrioritisationGuidanceCategoryDto
            {
                Id = SprGuidanceReportLayout.EndemicOrExoticCategoryId,
                DisplayName = "Public health",
                Criteria =
                [
                    new PrioritisationGuidanceCriterionDto { ProfileQuestionId = QuestionId(6, 14), CriterionCode = "PH01", CriterionName = "Zoonosis" },
                    new PrioritisationGuidanceCriterionDto { ProfileQuestionId = SprGuidanceReportLayout.EndemicQuestionId, CriterionCode = "PH02", CriterionName = "<i>Endemic</i> or exotic" }
                ]
            },
            new PrioritisationGuidanceCategoryDto
            {
                Id = Guid.NewGuid(),
                DisplayName = "Post mitigation risk",
                Criteria = [new PrioritisationGuidanceCriterionDto { ProfileQuestionId = QuestionId(3, 20), CriterionCode = "PMR01", CriterionName = "Control" }]
            }
        ]
    };
}
