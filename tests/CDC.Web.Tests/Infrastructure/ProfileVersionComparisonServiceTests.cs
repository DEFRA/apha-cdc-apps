using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.Pages;
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
    private static readonly Guid Cattle = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
    private static readonly Guid Pigs = Guid.Parse("eeeeeeee-0000-0000-0000-000000000002");
    private static readonly Guid Sheep = Guid.Parse("eeeeeeee-0000-0000-0000-000000000003");

    private static readonly ProfileVersionOption Left = new(LeftId, "Anthrax - Current situation - Version 1.0");
    private static readonly ProfileVersionOption Right = new(RightId, "Anthrax - Current situation - Version 2.0");

    private static ProfileQuestionnaireMetadataDto Metadata(bool perSpecies, bool repeating, params ProfileFieldMetadataDto[] fields) => new()
    {
        Sections =
        [
            new ProfileSectionMetadataDto
            {
                Id = SectionId,
                Name = "Epidemiology",
                SectionNumber = 1,
                Questions =
                [
                    new ProfileQuestionMetadataDto
                    {
                        Id = QuestionId,
                        SectionId = SectionId,
                        ShortName = "Q",
                        QuestionNumber = 1,
                        IsPerSpecies = perSpecies,
                        IsRepeating = repeating,
                        Fields = fields
                    }
                ]
            },
            new ProfileSectionMetadataDto { Id = SecondSectionId, Name = "Control", SectionNumber = 2 }
        ]
    };

    private static ProfileQuestionnaireMetadataDto Metadata(params ProfileFieldMetadataDto[] fields) => Metadata(false, false, fields);

    private static ProfileFieldMetadataDto Field(int number, string type, Guid? table = null) => new()
    {
        Id = Guid.NewGuid(),
        QuestionId = QuestionId,
        Name = $"Field {number}",
        FieldNumber = number,
        DataTypeName = type,
        ReferenceTableId = table ?? Guid.Empty
    };

    private static ProfileSectionAnswersDto Answers(params ProfileFieldValueDto[] values) => Answers(values, []);

    private static ProfileSectionAnswersDto Answers(ProfileFieldValueDto[] values, ProfileQuestionRowDto[] rows) => new()
    {
        QuestionNames = [new ProfileQuestionNameDto { Id = QuestionId, Name = "Clinical signs" }],
        FieldValues = values,
        QuestionRows = rows
    };

    private static ProfileFieldValueDto Value(
        int field,
        string? text = null,
        bool? boolean = null,
        Guid? list = null,
        decimal? number = null,
        DateTime? date = null,
        Guid? species = null,
        Guid? row = null) => new()
        {
            QuestionId = QuestionId,
            FieldNumber = field,
            TextValue = text,
            BooleanValue = boolean,
            ListValue = list,
            DecimalValue = number,
            DateValue = date,
            SpeciesId = species,
            ProfileVersionQuestionRowId = row
        };

    private static ProfileVersionComparisonService Service(
        ProfileQuestionnaireMetadataDto metadata,
        ProfileSectionAnswersDto left,
        ProfileSectionAnswersDto right,
        IReadOnlyDictionary<Guid, IReadOnlyList<ReferenceValueDto>>? references = null) =>
        new(
            new FakeProfileSectionsApiService(
                metadata: metadata,
                answersByVersion: new Dictionary<Guid, ProfileSectionAnswersDto> { [LeftId] = left, [RightId] = right },
                referenceValuesByTable: references),
            new FakeSpeciesApiService(
            [
                new SpeciesDto { Id = Cattle, Description = "Cattle", IsActive = true },
                new SpeciesDto { Id = Pigs, Description = "Pigs", IsActive = true },
                new SpeciesDto { Id = Sheep, Description = "Sheep", IsActive = true }
            ]));

    private static string Text(IReadOnlyList<ComparisonSegment> segments) => string.Concat(segments.Select(segment => segment.Text));

    private static string Marked(IReadOnlyList<ComparisonSegment> segments, ComparisonChange change) =>
        string.Concat(segments.Where(segment => segment.Change == change).Select(segment => segment.Text));

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
        var group = Assert.Single(question.Groups);
        var field = Assert.Single(group.Fields);
        Assert.Equal("1. Epidemiology", section.Heading);
        Assert.Equal("1.1", question.Number);
        Assert.Equal("Clinical signs", question.Text);
        Assert.Null(group.Heading);
        Assert.Equal("Field 2", field.Label);
        Assert.Equal("old", Marked(field.Left, ComparisonChange.Removed));
        Assert.Equal("new", Marked(field.Right, ComparisonChange.Added));
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

        var fields = (await service.BuildReportAsync(Left, Right)).Sections[0].Questions[0].Groups[0].Fields;

        Assert.Equal(("Yes", "No"), (Text(fields[0].Left), Text(fields[0].Right)));
        Assert.Equal(("1.5", "2"), (Text(fields[1].Left), Text(fields[1].Right)));
        Assert.Equal(("03/02/2024", "31/12/2025"), (Text(fields[2].Left), Text(fields[2].Right)));
        Assert.Equal("Not answered", Text(fields[3].Left));
        Assert.Equal("added", Marked(fields[3].Right, ComparisonChange.Added));
    }

    [Fact]
    public async Task BuildReportAsync_ShowsRemovedAnswerAgainstNotAnswered()
    {
        var service = Service(Metadata(Field(1, "Text")), Answers(Value(1, "gone")), Answers());

        var field = (await service.BuildReportAsync(Left, Right)).Sections[0].Questions[0].Groups[0].Fields[0];

        Assert.Equal("gone", Marked(field.Left, ComparisonChange.Removed));
        Assert.Equal("Not answered", Text(field.Right));
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

        var fields = (await service.BuildReportAsync(Left, Right)).Sections[0].Questions[0].Groups[0].Fields;

        var field = Assert.Single(fields);
        Assert.Equal("Field 2", field.Label);
        Assert.Equal(("Alpha", "Beta"), (Text(field.Left), Text(field.Right)));
    }

    [Fact]
    public async Task BuildReportAsync_ComparesLongTextAsPlainTextWithoutMarkup()
    {
        var service = Service(
            Metadata(Field(1, "Long Text")),
            Answers(Value(1, "<p>First</p><p>Second</p>")),
            Answers(Value(1, "<p>First</p><p>Third &amp; more</p><script>x()</script>")));

        var field = (await service.BuildReportAsync(Left, Right)).Sections[0].Questions[0].Groups[0].Fields[0];

        Assert.Equal("First\nSecond", Text(field.Left));
        Assert.DoesNotContain("<", Text(field.Right), StringComparison.Ordinal);
        Assert.Contains("Third & more", Marked(field.Right, ComparisonChange.Added), StringComparison.Ordinal);
        Assert.Equal("Second", Marked(field.Left, ComparisonChange.Removed));
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
        Assert.Single(question.Groups[0].Fields);
    }

    [Fact]
    public async Task BuildReportAsync_ComparesPerSpeciesAnswersSpeciesBySpecies()
    {
        var service = Service(
            Metadata(true, false, Field(1, "Text")),
            Answers(Value(1, "x", species: Cattle), Value(1, "same", species: Sheep)),
            Answers(Value(1, "y", species: Cattle), Value(1, "same", species: Sheep), Value(1, "z", species: Pigs)));

        var groups = Assert.Single((await service.BuildReportAsync(Left, Right)).Sections[0].Questions).Groups;

        Assert.Equal(["Cattle", "Pigs"], groups.Select(group => group.Heading));
        Assert.Equal("x", Marked(groups[0].Fields[0].Left, ComparisonChange.Removed));
        Assert.Equal("Not answered", Text(groups[1].Fields[0].Left));
        Assert.Equal("z", Marked(groups[1].Fields[0].Right, ComparisonChange.Added));
    }

    [Fact]
    public async Task BuildReportAsync_ComparesRepeatingAnswersRowByRowByPosition()
    {
        Guid l1 = Guid.NewGuid(), l2 = Guid.NewGuid(), r1 = Guid.NewGuid(), r2 = Guid.NewGuid(), r3 = Guid.NewGuid();
        var service = Service(
            Metadata(false, true, Field(1, "Text")),
            Answers(
                [Value(1, "a", row: l1), Value(1, "b", row: l2)],
                [new ProfileQuestionRowDto { Id = l2, QuestionId = QuestionId, SequenceNumber = 2 }, new ProfileQuestionRowDto { Id = l1, QuestionId = QuestionId, SequenceNumber = 1 }]),
            Answers(
                [Value(1, "a", row: r1), Value(1, "c", row: r2), Value(1, "d", row: r3)],
                [
                    new ProfileQuestionRowDto { Id = r1, QuestionId = QuestionId, SequenceNumber = 1 },
                    new ProfileQuestionRowDto { Id = r2, QuestionId = QuestionId, SequenceNumber = 2 },
                    new ProfileQuestionRowDto { Id = r3, QuestionId = QuestionId, SequenceNumber = 3 }
                ]));

        var groups = Assert.Single((await service.BuildReportAsync(Left, Right)).Sections[0].Questions).Groups;

        Assert.Equal(["Clinical signs 2", "Clinical signs 3"], groups.Select(group => group.Heading));
        Assert.Equal(("b", "c"), (Marked(groups[0].Fields[0].Left, ComparisonChange.Removed), Marked(groups[0].Fields[0].Right, ComparisonChange.Added)));
        Assert.Equal("d", Marked(groups[1].Fields[0].Right, ComparisonChange.Added));
    }
}
