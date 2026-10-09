using CDC.Web.Models;
using CDC.Web.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;

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
        IReadOnlyDictionary<Guid, IReadOnlyList<ReferenceValueDto>>? referenceValuesByTable = null,
        UpdateSpeciesAnswerDataResult? updateAnswerDataResult = null,
        FakeSpeciesApiService? speciesApiService = null,
        IDictionary<string, StringValues>? formValues = null)
    {
        var modelMetadataProvider = new EmptyModelMetadataProvider();
        var httpContext = new DefaultHttpContext();
        if (formValues is not null)
        {
            httpContext.Request.Form = new FormCollection(new Dictionary<string, StringValues>(formValues));
        }

        var pageModel = new EditSpeciesModel(
            speciesApiService ?? new FakeSpeciesApiService(
                speciesAnswerData: speciesAnswerData,
                throwOnGetSpeciesAnswerData: throwOnGetSpeciesAnswerData,
                speciesMetadata: speciesMetadata,
                referenceValuesByTable: referenceValuesByTable,
                updateAnswerDataResult: updateAnswerDataResult),
            new AlwaysEnabledLogger<EditSpeciesModel>())
        {
            SpeciesId = SpeciesId,
            Section = section,
            PageContext = new PageContext
            {
                HttpContext = httpContext,
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

    [Fact]
    public async Task OnPostSaveAsync_ReturnsPageWithError_WhenTheLastUpdatedTokenIsNotValidBase64()
    {
        var pageModel = CreatePageModel(AnswerData(), speciesMetadata: new SpeciesMetadataDto());
        pageModel.LastUpdatedBase64 = "not valid base64 !!";

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("The species data could not be saved. Reload and try again.", pageModel.SaveErrorMessage);
    }

    [Fact]
    public async Task OnPostSaveAsync_ReturnsPage_WhenNoMetadataSectionMatchesTheCurrentSection()
    {
        var fake = new FakeSpeciesApiService(speciesAnswerData: AnswerData(), speciesMetadata: new SpeciesMetadataDto());
        var pageModel = CreatePageModel(speciesApiService: fake, section: "movements");
        pageModel.LastUpdatedBase64 = Convert.ToBase64String([1, 2, 3]);

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Null(fake.LastUpdateAnswerDataRequest);
    }

    [Fact]
    public async Task OnPostSaveAsync_SavesEveryFieldKind_AndRedirectsOnSuccess()
    {
        var multiValueFieldId = Guid.NewGuid();
        var listFieldId = Guid.NewGuid();
        var booleanFieldId = Guid.NewGuid();
        var textFieldId = Guid.NewGuid();
        var referenceTableId = Guid.NewGuid();
        var selectedOptionId = Guid.NewGuid();
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
                            Name = "Movements",
                            QuestionNumber = 1,
                            Fields =
                            [
                                new SpeciesFieldMetadataDto { Id = multiValueFieldId, QuestionId = QuestionId, Name = "Multi", FieldNumber = 1, DataTypeName = "MultiValueList", ReferenceTableId = referenceTableId },
                                new SpeciesFieldMetadataDto { Id = listFieldId, QuestionId = QuestionId, Name = "List", FieldNumber = 2, DataTypeName = "List" },
                                new SpeciesFieldMetadataDto { Id = booleanFieldId, QuestionId = QuestionId, Name = "Boolean", FieldNumber = 3, DataTypeName = "Boolean" },
                                new SpeciesFieldMetadataDto { Id = textFieldId, QuestionId = QuestionId, Name = "Text", FieldNumber = 4, DataTypeName = "Text" }
                            ]
                        }
                    ]
                }
            ]
        };
        var fake = new FakeSpeciesApiService(
            speciesAnswerData: AnswerData(),
            speciesMetadata: metadata,
            updateAnswerDataResult: new UpdateSpeciesAnswerDataResult { Outcome = SpeciesUpdateOutcome.Success });
        var formValues = new Dictionary<string, StringValues>
        {
            [$"field_{multiValueFieldId}"] = new([selectedOptionId.ToString()]),
            [$"field_{listFieldId}"] = string.Empty,
            [$"field_{booleanFieldId}"] = "true",
            [$"field_{textFieldId}"] = "Endemic in GB"
        };
        var pageModel = CreatePageModel(speciesApiService: fake, section: "movements", formValues: formValues);
        pageModel.LastUpdatedBase64 = Convert.ToBase64String([1, 2, 3]);

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.True((bool)redirect.RouteValues!["saved"]!);
        Assert.NotNull(fake.LastUpdateAnswerDataRequest);
        var changes = fake.LastUpdateAnswerDataRequest!.Changes;
        Assert.Equal(4, changes.Count);
        Assert.Equal(SpeciesFieldValueKind.MultiValue, changes[0].Kind);
        Assert.Equal(selectedOptionId, Assert.Single(changes[0].MultiValues));
        Assert.Equal(SpeciesFieldValueKind.None, changes[1].Kind);
        Assert.Equal(SpeciesFieldValueKind.Boolean, changes[2].Kind);
        Assert.True(changes[2].BooleanValue);
        Assert.Equal(SpeciesFieldValueKind.Text, changes[3].Kind);
        Assert.Equal("Endemic in GB", changes[3].TextValue);
    }

    [Fact]
    public async Task OnPostSaveAsync_ReturnsPageWithError_WhenTheSaveFails()
    {
        var fieldId = Guid.NewGuid();
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
                            Name = "Movements",
                            QuestionNumber = 1,
                            Fields = [new SpeciesFieldMetadataDto { Id = fieldId, QuestionId = QuestionId, Name = "Text", FieldNumber = 1, DataTypeName = "Text" }]
                        }
                    ]
                }
            ]
        };
        var fake = new FakeSpeciesApiService(
            speciesAnswerData: AnswerData(),
            speciesMetadata: metadata,
            updateAnswerDataResult: new UpdateSpeciesAnswerDataResult
            {
                Outcome = SpeciesUpdateOutcome.Conflict,
                ErrorMessage = "Another user has changed this species since it was opened. Reload and try again."
            });
        var formValues = new Dictionary<string, StringValues> { [$"field_{fieldId}"] = "Some text" };
        var pageModel = CreatePageModel(speciesApiService: fake, section: "movements", formValues: formValues);
        pageModel.LastUpdatedBase64 = Convert.ToBase64String([1, 2, 3]);

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Another user has changed this species since it was opened. Reload and try again.", pageModel.SaveErrorMessage);
    }
}
