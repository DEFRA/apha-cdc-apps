using CDC.Common.Contracts;

namespace CDC.Web.Tests.Infrastructure;

/// <summary>Builders for profile report content used by the report layout and rendering tests.</summary>
internal static class ReportContent
{
    public static ProfileReportContentDto Create(
        IReadOnlyList<ProfileReportSectionDto> sections,
        bool isPublished = false,
        bool showDraftWatermark = true,
        DateTime? effectiveDateTo = null,
        string profileStatusName = "") => new()
        {
            ProfileVersionId = Guid.NewGuid(),
            ProfileId = Guid.NewGuid(),
            FullTitle = "Anthrax <em>(What if)</em>",
            VersionLabel = "1.2",
            IsPublished = isPublished,
            EffectiveDateFrom = new DateTime(2026, 3, 4),
            EffectiveDateTo = effectiveDateTo,
            ProfileStatusName = profileStatusName,
            ShowDraftWatermark = showDraftWatermark,
            Sections = sections
        };

    public static ProfileReportSectionDto Section(int number, string name, string note = "", params ProfileReportQuestionDto[] questions) => new()
    {
        Id = Guid.NewGuid(),
        SectionNumber = number,
        Name = name,
        ShortName = name,
        Note = note,
        Questions = questions
    };

    public static ProfileReportQuestionDto Question(
        int number,
        string name,
        string nonTechnicalName = "",
        ProfileReportQuestionKind kind = ProfileReportQuestionKind.Standard,
        bool isRelevant = true,
        params ProfileReportAnswerGroupDto[] groups) => new()
        {
            Id = Guid.NewGuid(),
            QuestionNumber = number,
            Name = name,
            NonTechnicalName = nonTechnicalName,
            Kind = kind,
            IsRelevant = isRelevant,
            Groups = groups
        };

    public static ProfileReportAnswerGroupDto Group(string? heading = null, bool isRelevant = true, params ProfileReportFieldDto[] fields) => new()
    {
        Heading = heading,
        IsRelevant = isRelevant,
        Fields = fields
    };

    public static ProfileReportFieldDto Text(string label, string text) => new() { Label = label, Kind = ProfileReportFieldKind.Text, Text = text };

    public static ProfileReportFieldDto Options(string label, params ProfileReportOptionDto[] options) =>
        new() { Label = label, Kind = ProfileReportFieldKind.Options, Options = options };

    /// <summary>A single-field question answered with the given text, for tests that only care which questions appear.</summary>
    public static ProfileReportQuestionDto Answered(int number, string name, string answer, string nonTechnicalName = "") =>
        Question(number, name, nonTechnicalName, groups: Group(fields: Text("Answer", answer)));

    /// <summary>A yes/no question answered Yes or No.</summary>
    public static ProfileReportQuestionDto YesNo(int number, string name, bool yes) =>
        Question(number, name, groups: Group(fields: Options("Is it?", new ProfileReportOptionDto("Yes", yes), new ProfileReportOptionDto("No", !yes))));
}
