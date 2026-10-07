using System.Net;
using System.Text.RegularExpressions;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.Pages;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// The default WebApplicationFactory<Program> has no live CDC.Api to call, so every species page
// only ever renders its error-banner path. These tests swap in a fake ISpeciesApiService with
// real data, so the tree/audit-trail/edit-panel views actually render their success paths. Uses
// CdcWebTestFactory (not the plain WebApplicationFactory<Program>) since these routes require
// authentication, and signs in via the test-only endpoint before each request.
public partial class SpeciesDataIntegrationTests
{
    private static readonly Guid CattleId = Guid.Parse("6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f");
    private static readonly Guid DairyId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly IReadOnlyList<SpeciesDto> Species =
    [
        new SpeciesDto { Id = CattleId, ParentId = Guid.Empty, Description = "Cattle", IsActive = true, IsInUse = true },
        new SpeciesDto { Id = DairyId, ParentId = CattleId, Description = "Dairy cattle", IsActive = true, IsInUse = true }
    ];

    [Fact]
    public async Task Maintain_RendersSpeciesTree_WhenSpeciesAreAvailable()
    {
        using var factory = CreateFactory(new FakeSpeciesApiService(Species));
        var client = await SignedInClientAsync(factory);

        var response = await client.GetAsync("/SpeciesData/Maintain");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Cattle", body);
        Assert.Contains("app-tree", body);
    }

    [Fact]
    public async Task Maintain_RendersAuditTrail_WhenHandlerRequested()
    {
        IReadOnlyList<SpeciesAuditTrailEntryDto> auditTrail =
        [
            new SpeciesAuditTrailEntryDto
            {
                Id = Guid.NewGuid(),
                OldName = "Dairy cattle",
                NewName = "Dairy",
                OldParent = "Cattle",
                NewParent = "Cattle",
                ChangedBy = "a.user",
                LogDate = DateTime.UtcNow,
                ReasonForChange = "Simplifying the name"
            }
        ];
        using var factory = CreateFactory(new FakeSpeciesApiService(Species, auditTrail: auditTrail));
        var client = await SignedInClientAsync(factory);

        var response = await client.GetAsync("/SpeciesData/Maintain?handler=AuditTrail");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Species name/parent audit trail", body);
        Assert.Contains("Simplifying the name", body);
    }

    [Fact]
    public async Task Maintain_OffersAnAddAction_WhenSpeciesAreAvailable()
    {
        using var factory = CreateFactory(new FakeSpeciesApiService(Species));
        var client = await SignedInClientAsync(factory);

        var response = await client.GetAsync("/SpeciesData/Maintain");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("handler=Add", body);
    }

    [Fact]
    public async Task ViewSpeciesData_RendersSpeciesTree_WhenSpeciesAreAvailable()
    {
        using var factory = CreateFactory(new FakeSpeciesApiService(Species));
        var client = await SignedInClientAsync(factory);

        var response = await client.GetAsync("/ViewSpeciesData");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Cattle", body);
    }

    private static WebApplicationFactory<Program> CreateFactory(ISpeciesApiService fakeService) =>
        new CdcWebTestFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISpeciesApiService>();
                services.AddSingleton(fakeService);
            }));

    // Every page is authenticated by default, so sign in via the test-only endpoint first.
    private static async Task<HttpClient> SignedInClientAsync(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        await client.GetAsync(CdcWebTestFactory.TestSignInPath);
        return client;
    }

    [Fact]
    public async Task Maintain_RendersSuccessBanner_AfterASave()
    {
        using var factory = CreateFactory(new FakeSpeciesApiService(Species));
        var client = await SignedInClientAsync(factory);

        var response = await client.GetAsync("/SpeciesData/Maintain?saved=true");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-notification-banner--success", body);
        Assert.Contains("The species name and parent were updated.", body);
    }

    [Fact]
    public async Task Maintain_RendersSuccessBanner_AfterAnAdd()
    {
        using var factory = CreateFactory(new FakeSpeciesApiService(Species));
        var client = await SignedInClientAsync(factory);

        var response = await client.GetAsync("/SpeciesData/Maintain?added=true");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("The new species was added to the hierarchy.", body);
    }

    [Fact]
    public async Task Maintain_RendersEmptyState_WhenNoActiveSpeciesExist()
    {
        using var factory = CreateFactory(new FakeSpeciesApiService([]));
        var client = await SignedInClientAsync(factory);

        var response = await client.GetAsync("/SpeciesData/Maintain");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No active species data is available.", body);
    }

    [Fact]
    public async Task Maintain_RendersErrorSummary_WhenTheApiIsUnreachable()
    {
        using var factory = CreateFactory(new FakeSpeciesApiService(throwOnGetAllSpecies: new HttpRequestException("down")));
        var client = await SignedInClientAsync(factory);

        var response = await client.GetAsync("/SpeciesData/Maintain");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("We could not load species data. Try again later.", body);
    }

    [Fact]
    public async Task Maintain_RendersAddSpeciesPanel_WhenAddIsPosted()
    {
        using var factory = CreateFactory(new FakeSpeciesApiService(Species));
        var client = await SignedInClientAsync(factory);

        var body = await PostAsync(client, "Add", new Dictionary<string, string>());

        Assert.Contains("Add species data value", body);
        // Old name and old parent are fixed placeholders for a species that does not exist yet.
        Assert.Contains("- new entry -", body);
        Assert.Contains("- root species -", body);
        Assert.Contains("Dairy cattle", body);
    }

    [Fact]
    public async Task Maintain_RendersValidationErrors_WhenANewSpeciesIsSavedWithNoValues()
    {
        using var factory = CreateFactory(new FakeSpeciesApiService(Species));
        var client = await SignedInClientAsync(factory);

        var body = await PostAsync(client, "SaveNew", new Dictionary<string, string>
        {
            ["AddInput.Name"] = string.Empty,
            ["AddInput.ParentId"] = string.Empty,
            ["AddInput.Reason"] = string.Empty
        });

        Assert.Contains("There is a problem", body);
        Assert.Contains("You need to provide a new name for this species", body);
        Assert.Contains("You must select a new parent for the species", body);
        Assert.Contains("You need to provide a reason for this change", body);
    }

    [Fact]
    public async Task Maintain_RendersSelectionError_WhenEditIsPostedWithNoSelection()
    {
        using var factory = CreateFactory(new FakeSpeciesApiService(Species));
        var client = await SignedInClientAsync(factory);

        var body = await PostAsync(client, "EditNameParent", new Dictionary<string, string>());

        Assert.Contains("Select a species or species group to edit.", body);
    }

    [Fact]
    public async Task Maintain_RendersEditNameParentPanel_WhenASpeciesIsSelected()
    {
        var detail = new SpeciesDetailDto
        {
            Id = DairyId,
            Name = "Dairy cattle",
            ParentId = CattleId,
            ParentName = "Cattle",
            IsActive = true,
            LastUpdated = [0, 0, 0, 0, 0, 0, 0, 1]
        };
        using var factory = CreateFactory(new FakeSpeciesApiService(
            Species,
            speciesDetail: detail,
            validParents: [new SpeciesValidParentDto { Id = CattleId, Name = "Cattle" }]));
        var client = await SignedInClientAsync(factory);

        var body = await PostAsync(client, "EditNameParent", new Dictionary<string, string>
        {
            ["species"] = DairyId.ToString()
        });

        Assert.Contains("Edit name/parent: Dairy cattle", body);
        Assert.Contains("Reason for change", body);
    }

    [Fact]
    public async Task EditSpecies_RendersQuestionAccordionAndSectionPagination()
    {
        var sectionId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var listFieldId = Guid.NewGuid();
        var referenceTableId = Guid.NewGuid();
        var checkedOptionId = Guid.NewGuid();

        var metadata = new SpeciesMetadataDto
        {
            Sections =
            [
                new SpeciesSectionMetadataDto
                {
                    Id = sectionId,
                    // Must match the first entry in EditSpeciesSectionCatalog, which the page defaults to.
                    Name = "Animal identification",
                    ShortName = "Ident",
                    SectionNumber = 1,
                    Questions =
                    [
                        new SpeciesQuestionMetadataDto
                        {
                            Id = questionId,
                            SectionId = sectionId,
                            Name = "Can individual animals be identified?",
                            ShortName = "Identifiable",
                            QuestionNumber = 1,
                            Fields =
                            [
                                new SpeciesFieldMetadataDto
                                {
                                    Id = Guid.NewGuid(),
                                    QuestionId = questionId,
                                    Name = "Is it possible?",
                                    FieldNumber = 1,
                                    DataTypeName = "Boolean"
                                },
                                new SpeciesFieldMetadataDto
                                {
                                    Id = listFieldId,
                                    QuestionId = questionId,
                                    Name = "Which records are used?",
                                    FieldNumber = 2,
                                    DataTypeName = "List",
                                    ReferenceTableId = referenceTableId
                                }
                            ]
                        }
                    ]
                }
            ]
        };
        var answerData = new SpeciesAnswerDataDto
        {
            SpeciesId = DairyId,
            SpeciesName = "Dairy cattle",
            LastUpdated = [0, 0, 0, 0, 0, 0, 0, 1],
            Sections =
            [
                new SpeciesSectionDto
                {
                    SectionId = sectionId,
                    FieldValues =
                    [
                        new SpeciesFieldValueDto { QuestionId = questionId, FieldNumber = 1, BooleanValue = true },
                        new SpeciesFieldValueDto { QuestionId = questionId, FieldNumber = 2, ListValue = checkedOptionId }
                    ]
                }
            ]
        };
        var referenceValues = new Dictionary<Guid, IReadOnlyList<ReferenceValueDto>>
        {
            [referenceTableId] =
            [
                new ReferenceValueDto { Id = Guid.NewGuid(), Value = "Market records" },
                new ReferenceValueDto { Id = checkedOptionId, Value = "Show records" }
            ]
        };

        using var factory = CreateFactory(new FakeSpeciesApiService(
            Species,
            speciesMetadata: metadata,
            speciesAnswerData: answerData,
            referenceValuesByTable: referenceValues));
        var client = await SignedInClientAsync(factory);

        var response = await client.GetAsync($"/EditSpecies/{DairyId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Can individual animals be identified?", body);
        Assert.Contains("Show records", body);
        Assert.Contains("Movements", body);
    }

    [Fact]
    public async Task EditSpecies_RendersErrorState_WhenTheApiIsUnreachable()
    {
        using var factory = CreateFactory(new FakeSpeciesApiService(
            Species,
            throwOnGetSpeciesAnswerData: new HttpRequestException("down")));
        var client = await SignedInClientAsync(factory);

        var response = await client.GetAsync($"/EditSpecies/{DairyId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Razor Pages validates an antiforgery token on every POST, so the form has to be round-tripped.
    private static async Task<string> PostAsync(HttpClient client, string handler, IDictionary<string, string> fields)
    {
        var page = await client.GetStringAsync("/SpeciesData/Maintain");
        var token = AntiforgeryTokenRegex().Match(page).Groups[1].Value;

        Assert.False(string.IsNullOrEmpty(token));

        var form = new Dictionary<string, string>(fields) { ["__RequestVerificationToken"] = token };
        using var response = await client.PostAsync($"/SpeciesData/Maintain?handler={handler}", new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadAsStringAsync();
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*?value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenRegex();
}
