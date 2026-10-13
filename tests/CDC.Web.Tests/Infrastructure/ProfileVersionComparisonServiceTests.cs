using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.Pages.SurveillanceProfiles;

namespace CDC.Web.Tests.Infrastructure;

public class ProfileVersionComparisonServiceTests
{
    private static readonly Guid SectionId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid SecondSectionId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid QuestionId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid TableId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid OptionA = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
    private static readonly Guid OptionB = Guid.Parse("dddddddd-0000-0000-0000-000000000002");
    private static readonly Guid LeftId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid RightId = Guid.Parse("11111111-0000-0000-0000-000000000002");

    private static readonly ProfileVersionOption Left = new(LeftId, "Anthrax - Current situation - Version 1.0");
    private static readonly ProfileVersionOption Right = new(RightId, "Anthrax - Current situation - Version 2.0");

    private static ProfileQuestionnaireMetadataDto Metadata(params ProfileFieldMetadataDto[] fields) => new()
    {
        Sections =
        [
            new ProfileSectionMetadataDto
            {
                Id = SectionId,
                Name = "Epidemiology",
                SectionNumber = 1,
                Questions = [new ProfileQuestionMetadataDto { Id = QuestionId, SectionId = SectionId, ShortName = "Q", QuestionNumber = 1, Fields = fields }]
            },
            new ProfileSectionMetadataDto { Id = SecondSectionId, Name = "Control", SectionNumber = 2 }
        ]
    };

    private static ProfileFieldMetadataDto Field(int number, string type, Guid? table = null) => new()
    {
        Id = Guid.NewGuid(),
        QuestionId = QuestionId,
        Name = $"Field {number}",
        FieldNumber = number,
        DataTypeName = type,
        ReferenceTableId = table ?? Guid.Empty
    };

    private static ProfileSectionAnswersDto Answers(params ProfileFieldValueDto[] values) => new()
    {
        QuestionNames = [new ProfileQuestionNameDto { Id = QuestionId, Name = "Clinical signs" }],
        FieldValues = values
    };

    private static ProfileFieldValueDto Value(int field, string? text = null, bool? boolean = null, Guid? list = null, decimal? number = null, DateTime? date = null) => new()
    {
        QuestionId = QuestionId,
        FieldNumber = field,
        TextValue = text,
        BooleanValue = boolean,
        ListValue = list,
        DecimalValue = number,
        DateValue = date
    };

    private static ProfileVersionComparisonService Service(
        ProfileQuestionnaireMetadataDto metadata,
        ProfileSectionAnswersDto left,
        ProfileSectionAnswersDto right,
        IReadOnlyDictionary<Guid, IReadOnlyList<ReferenceValueDto>>? references = null) =>
        new(new FakeProfileSectionsApiService(
            metadata: metadata,
            answersByVersion: new Dictionary<Guid, ProfileSectionAnswersDto> { [LeftId] = left, [RightId] = right },
            referenceValuesByTable: references));

    [Fact]
    public async Task BuildReportAsync_ReportsOnlyDifferingFields()
    {
        var service = Service(
            Metadata(Field(1, "Text"), Field(2, "Text")),
            Answers(Value(1, "same"), Value(2, "old")),
            Answers(Value(1, "same"), Value(2, "new")));

        var report = await service.BuildReportAsync(Left, Right);

        var section = report.Sections[0];
        var question = Assert.Single(section.Questions);
        var field = Assert.Single(question.Fields);
        Assert.Equal("1. Epidemiology", section.Heading);
        Assert.Equal("1.1", question.Number);
        Assert.Equal("Clinical signs", question.Text);
        Assert.Equal(("Field 2", "old", "new"), (field.Label, field.Left, field.Right));
        Assert.Equal(Left.FullLabel, report.LeftTitle);
        Assert.Equal(Right.FullLabel, report.RightTitle);
    }

    [Fact]
    public async Task BuildReportAsync_MarksSectionIdenticalWhenNothingDiffers()
    {
        var service = Service(Metadata(Field(1, "Text")), Answers(Value(1, "same")), Answers(Value(1, "same")));

        var report = await service.BuildReportAsync(Left, Right);

        Assert.All(report.Sections, section => Assert.True(section.IsIdentical));
    }

    [Fact]
    public async Task BuildReportAsync_FormatsEachDataType()
    {
        var service = Service(
            Metadata(Field(1, "Boolean"), Field(2, "Decimal"), Field(3, "Date"), Field(4, "Text")),
            Answers(Value(1, boolean: true), Value(2, number: 1.50m), Value(3, date: new DateTime(2024, 2, 3))),
            Answers(Value(1, boolean: false), Value(2, number: 2m), Value(3, date: new DateTime(2025, 12, 31)), Value(4, "added")));

        var fields = (await service.BuildReportAsync(Left, Right)).Sections[0].Questions[0].Fields;

        Assert.Equal(("Yes", "No"), (fields[0].Left, fields[0].Right));
        Assert.Equal(("1.5", "2"), (fields[1].Left, fields[1].Right));
        Assert.Equal(("03/02/2024", "31/12/2025"), (fields[2].Left, fields[2].Right));
        Assert.Equal(("Not answered", "added"), (fields[3].Left, fields[3].Right));
    }

    [Fact]
    public async Task BuildReportAsync_ResolvesListValuesAndIgnoresSelectionOrder()
    {
        IReadOnlyDictionary<Guid, IReadOnlyList<ReferenceValueDto>> references = new Dictionary<Guid, IReadOnlyList<ReferenceValueDto>>
        {
            [TableId] = [new ReferenceValueDto { Id = OptionA, Value = "Alpha" }, new ReferenceValueDto { Id = OptionB, Value = "Beta" }]
        };
        var service = Service(
            Metadata(Field(1, "MultiValueList", TableId), Field(2, "List", TableId)),
            Answers(Value(1, list: OptionA), Value(1, list: OptionB), Value(2, list: OptionA)),
            Answers(Value(1, list: OptionB), Value(1, list: OptionA), Value(2, list: OptionB)),
            references);

        var fields = (await service.BuildReportAsync(Left, Right)).Sections[0].Questions[0].Fields;

        var field = Assert.Single(fields);
        Assert.Equal(("Field 2", "Alpha", "Beta"), (field.Label, field.Left, field.Right));
    }

    [Fact]
    public async Task BuildReportAsync_SanitisesLongTextHtml()
    {
        var service = Service(
            Metadata(Field(1, "Long Text")),
            Answers(Value(1, "<p>old</p>")),
            Answers(Value(1, "<p>new</p><script>alert(1)</script>")));

        var field = Assert.Single((await service.BuildReportAsync(Left, Right)).Sections[0].Questions[0].Fields);

        Assert.True(field.IsHtml);
        Assert.Equal("<p>new</p>", field.Right);
    }

    [Fact]
    public async Task BuildReportAsync_SkipsFieldGroupsAndFallsBackToShortName()
    {
        var service = Service(
            Metadata(Field(1, "Field Group"), Field(2, "Text")),
            new ProfileSectionAnswersDto { FieldValues = [Value(2, "a")] },
            new ProfileSectionAnswersDto { FieldValues = [Value(2, "b")] });

        var question = Assert.Single((await service.BuildReportAsync(Left, Right)).Sections[0].Questions);

        Assert.Equal("Q", question.Text);
        Assert.Single(question.Fields);
    }
}
