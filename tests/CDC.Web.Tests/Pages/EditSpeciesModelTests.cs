using CDC.Web.Models;
using CDC.Web.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDC.Web.Tests.Pages;

public class EditSpeciesModelTests
{
    private static readonly Guid SpeciesId = Guid.NewGuid();
    private static readonly Guid MovementsSectionId = Guid.NewGuid();
    private static readonly Guid QuestionId = Guid.NewGuid();

    private static EditSpeciesModel CreatePageModel(
        SpeciesAnswerDataDto? speciesAnswerData = null,
        Exception? throwOnGetSpeciesAnswerData = null,
        SpeciesMetadataDto? speciesMetadata = null,
        string? section = null,
        IReadOnlyDictionary<Guid, IReadOnlyList<ReferenceValueDto>>? referenceValuesByTable = null)
    {
        var modelMetadataProvider = new EmptyModelMetadataProvider();
        var pageModel = new EditSpeciesModel(
            new FakeSpeciesApiService(
                speciesAnswerData: speciesAnswerData,
                throwOnGetSpeciesAnswerData: throwOnGetSpeciesAnswerData,
                speciesMetadata: speciesMetadata,
                referenceValuesByTable: referenceValuesByTable),
            new AlwaysEnabledLogger<EditSpeciesModel>())
        {
            SpeciesId = SpeciesId,
            Section = section,
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext(),
                ViewData = new ViewDataDictionary(modelMetadataProvider, new ModelStateDictionary())
            },
            MetadataProvider = modelMetadataProvider
        };

        return pageModel;
    }

    private static SpeciesAnswerDataDto AnswerData(IReadOnlyList<SpeciesSectionDto>? sections = null) => new()
    {
        SpeciesId = SpeciesId,
        SpeciesName = "Ruminants",
        LastUpdated = [1, 2, 3],
        Sections = sections ?? []
    };

    private static SpeciesMetadataDto MetadataWithMovementsSection() => new()
    {
        Sections =
        [
            new SpeciesSectionMetadataDto
            {
                Id = MovementsSectionId,
                Name = "Movements",
                ShortName = "Move",
                SectionNumber = 2,
                Questions =
                [
                    new SpeciesQuestionMetadataDto
                    {
                        Id = QuestionId,
                        SectionId = MovementsSectionId,
                        Name = "How easy is it to investigate the movement of a specific animal?",
                        ShortName = "Movement investigation",
                        QuestionNumber = 1,
                        Fields =
                        [
                            new SpeciesFieldMetadataDto
                            {
                                Id = Guid.NewGuid(),
                                QuestionId = QuestionId,
                                Name = "Is it easy?",
                                FieldNumber = 1,
                                DataTypeName = "Boolean"
                            }
                        ]
                    }
                ]
            }
        ]
    };

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenTheSpeciesDoesNotExist()
    {
        var pageModel = CreatePageModel(speciesAnswerData: null);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Null(pageModel.SpeciesName);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsPageWithHasError_WhenLoadingTheSpeciesFails()
    {
        var pageModel = CreatePageModel(throwOnGetSpeciesAnswerData: new HttpRequestException("connection refused"));

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.HasError);
        Assert.Null(pageModel.SpeciesName);
    }

    [Fact]
    public async Task OnGetAsync_PopulatesSpeciesName_WhenTheSpeciesExists()
    {
        var pageModel = CreatePageModel(AnswerData(), speciesMetadata: new SpeciesMetadataDto());

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Ruminants", pageModel.SpeciesName);
        Assert.False(pageModel.HasError);
    }

    [Fact]
    public async Task OnGetAsync_DefaultsToTheFirstSection_WhenNoSectionIsRequested()
    {
        var pageModel = CreatePageModel(AnswerData(), speciesMetadata: new SpeciesMetadataDto());

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(EditSpeciesSectionCatalog.Sections[0].Key, pageModel.CurrentSection.Key);
        Assert.Null(pageModel.PreviousSection);
        Assert.Equal(EditSpeciesSectionCatalog.Sections[1].Key, pageModel.NextSection!.Key);
    }

    [Fact]
    public async Task OnGetAsync_SelectsTheRequestedSection_AndComputesItsNeighbours()
    {
        var pageModel = CreatePageModel(AnswerData(), speciesMetadata: new SpeciesMetadataDto(), section: "movements");

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal("movements", pageModel.CurrentSection.Key);
        Assert.Equal(EditSpeciesSectionCatalog.Sections[0].Key, pageModel.PreviousSection!.Key);
        Assert.Equal(EditSpeciesSectionCatalog.Sections[2].Key, pageModel.NextSection!.Key);
    }

    [Fact]
    public async Task OnGetAsync_FallsBackToTheFirstSection_WhenAnUnknownSectionIsRequested()
    {
        var pageModel = CreatePageModel(AnswerData(), speciesMetadata: new SpeciesMetadataDto(), section: "not-a-real-section");

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(EditSpeciesSectionCatalog.Sections[0].Key, pageModel.CurrentSection.Key);
    }

    [Fact]
    public async Task OnGetAsync_IsEmpty_WhenNoMetadataSectionMatchesTheCurrentSection()
    {
        var pageModel = CreatePageModel(AnswerData(), speciesMetadata: new SpeciesMetadataDto(), section: "movements");

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Empty(pageModel.Questions);
    }

    [Fact]
    public async Task OnGetAsync_LoadsRealQuestions_ForTheMatchedSection()
    {
        var pageModel = CreatePageModel(AnswerData(), speciesMetadata: MetadataWithMovementsSection(), section: "movements");

        await pageModel.OnGetAsync(CancellationToken.None);

        var question = Assert.Single(pageModel.Questions);
        Assert.Equal("2.1", question.Number);
        Assert.Equal("How easy is it to investigate the movement of a specific animal?", question.Text);
        var field = Assert.Single(question.Fields);
        Assert.Equal("Is it easy?", field.Label);
        Assert.Equal("Not answered", field.ValueDisplay);
    }

    [Fact]
    public async Task OnGetAsync_FormatsRecordedAnswers_ForTheMatchedQuestionFields()
    {
        var answerData = AnswerData(
        [
            new SpeciesSectionDto
            {
                SectionId = MovementsSectionId,
                FieldValues =
                [
                    new SpeciesFieldValueDto { QuestionId = QuestionId, FieldNumber = 1, BooleanValue = true }
                ]
            }
        ]);
        var pageModel = CreatePageModel(answerData, speciesMetadata: MetadataWithMovementsSection(), section: "movements");

        await pageModel.OnGetAsync(CancellationToken.None);

        var field = Assert.Single(Assert.Single(pageModel.Questions).Fields);
        Assert.Equal("Yes", field.ValueDisplay);
    }

    [Fact]
    public async Task OnGetAsync_RendersListFieldAsCheckboxOptions_FromItsReferenceTable()
    {
        var referenceTableId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        var optionAId = Guid.NewGuid();
        var optionBId = Guid.NewGuid();
        var metadata = new SpeciesMetadataDto
        {
            Sections =
            [
                new SpeciesSectionMetadataDto
                {
                    Id = MovementsSectionId,
                    Name = "Movements",
                    SectionNumber = 2,
                    Questions =
                    [
                        new SpeciesQuestionMetadataDto
                        {
                            Id = QuestionId,
                            SectionId = MovementsSectionId,
                            Name = "Tick all that apply",
                            QuestionNumber = 2,
                            Fields =
                            [
                                new SpeciesFieldMetadataDto
                                {
                                    Id = fieldId,
                                    QuestionId = QuestionId,
                                    Name = "Tick all that apply",
                                    FieldNumber = 1,
                                    DataTypeName = "List",
                                    ReferenceTableId = referenceTableId
                                }
                            ]
                        }
                    ]
                }
            ]
        };
        var answerData = AnswerData(
        [
            new SpeciesSectionDto
            {
                SectionId = MovementsSectionId,
                FieldValues = [new SpeciesFieldValueDto { QuestionId = QuestionId, FieldNumber = 1, ListValue = optionBId }]
            }
        ]);
        var referenceValuesByTable = new Dictionary<Guid, IReadOnlyList<ReferenceValueDto>>
        {
            [referenceTableId] =
            [
                new ReferenceValueDto { Id = optionAId, Value = "Market records" },
                new ReferenceValueDto { Id = optionBId, Value = "Show records" }
            ]
        };
        var pageModel = CreatePageModel(
            answerData,
            speciesMetadata: metadata,
            section: "movements",
            referenceValuesByTable: referenceValuesByTable);

        await pageModel.OnGetAsync(CancellationToken.None);

        var field = Assert.Single(Assert.Single(pageModel.Questions).Fields);
        Assert.Null(field.ValueDisplay);
        Assert.Equal(2, field.Options.Count);
        Assert.False(field.Options[0].IsChecked);
        Assert.Equal("Market records", field.Options[0].Text);
        Assert.True(field.Options[1].IsChecked);
        Assert.Equal("Show records", field.Options[1].Text);
    }
}
