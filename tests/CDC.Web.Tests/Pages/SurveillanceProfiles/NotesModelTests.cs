using CDC.Common.Contracts;
using CDC.Web.Models;
using CDC.Web.Pages.SurveillanceProfiles;
using CDC.Web.Tests.Features.Landing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

public class NotesModelTests
{
    private static readonly Guid ProfileId = Guid.NewGuid();
    private static readonly Guid ProfileVersionId = Guid.NewGuid();
    private static readonly Guid ScientificPaperReferenceId = Guid.NewGuid();
    private static readonly Guid LegislativeReferenceId = Guid.NewGuid();
    private static readonly Guid FurtherInformationId = Guid.NewGuid();
    private static readonly Guid SummarySectionId = Guid.NewGuid();
    private static readonly Guid QuestionId = Guid.NewGuid();

    private static NotesModel CreatePageModel(
        ManageProfileViewModel? manageProfile = null,
        Exception? throwOnGetManageProfile = null,
        ProfileQuestionnaireMetadataDto? metadata = null,
        IReadOnlyList<ProfileNoteTypeDto>? noteTypes = null,
        IReadOnlyDictionary<Guid, IReadOnlyList<ProfileNoteDto>>? notesByVersionAndNoteType = null,
        Exception? throwOnGetProfileNotesByVersion = null)
    {
        var modelMetadataProvider = new EmptyModelMetadataProvider();

        return new NotesModel(
            new FakeApiClient(manageProfile: manageProfile, throwOnGetManageProfile: throwOnGetManageProfile),
            new FakeProfileSectionsApiService(
                metadata: metadata,
                noteTypes: noteTypes,
                notesByVersionAndNoteType: notesByVersionAndNoteType,
                throwOnGetProfileNotesByVersion: throwOnGetProfileNotesByVersion),
            new AlwaysEnabledLogger<NotesModel>())
        {
            ProfileId = ProfileId,
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext(),
                ViewData = new ViewDataDictionary(modelMetadataProvider, new ModelStateDictionary())
            },
            MetadataProvider = modelMetadataProvider
        };
    }

    private static ManageProfileViewModel Profile(Guid? currentProfileVersionId = null) => new()
    {
        ProfileId = ProfileId,
        ProfileTitle = "African Swine Fever",
        CurrentProfileVersionId = currentProfileVersionId ?? ProfileVersionId
    };

    private static ProfileQuestionnaireMetadataDto OneSectionMetadata() => new()
    {
        Sections =
        [
            new ProfileSectionMetadataDto
            {
                Id = SummarySectionId,
                Name = "Summary",
                ShortName = "Summ",
                SectionNumber = 1,
                Questions =
                [
                    new ProfileQuestionMetadataDto
                    {
                        Id = QuestionId,
                        SectionId = SummarySectionId,
                        ShortName = "Overview",
                        QuestionNumber = 4,
                        IsPerSpecies = false,
                        IsRepeating = false,
                        Fields = []
                    }
                ]
            }
        ]
    };

    private static IReadOnlyList<ProfileNoteTypeDto> ThreeNoteTypes() =>
    [
        new ProfileNoteTypeDto { Id = ScientificPaperReferenceId, Name = "Scientific paper reference", PluralName = "Scientific paper references" },
        new ProfileNoteTypeDto { Id = LegislativeReferenceId, Name = "Legislative reference", PluralName = "Legislative references" },
        new ProfileNoteTypeDto { Id = FurtherInformationId, Name = "SourceOfFurtherInformation", PluralName = "Sources of further information" }
    ];

    private static ProfileNoteDto Note(string text, int? questionNumber = null) => new()
    {
        Id = Guid.NewGuid(),
        NoteText = text,
        QuestionReferences = questionNumber is null
            ? []
            : [new QuestionReferenceDto { ProfileSectionId = SummarySectionId, ProfileQuestionId = QuestionId }]
    };

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenTheProfileDoesNotExist()
    {
        var pageModel = CreatePageModel(manageProfile: null);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnGetAsync_SetsHasNoVersion_WhenTheProfileHasNoCurrentVersion()
    {
        var pageModel = CreatePageModel(Profile(currentProfileVersionId: Guid.Empty));

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.True(pageModel.HasNoVersion);
        Assert.False(pageModel.HasError);
    }

    [Fact]
    public async Task OnGetAsync_SetsHasError_WhenApiThrows()
    {
        var pageModel = CreatePageModel(Profile(), throwOnGetManageProfile: new HttpRequestException("boom"));

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.True(pageModel.HasError);
    }

    [Fact]
    public async Task OnGetAsync_SelectsNothing_WhenNoReferenceTypeIsRequested()
    {
        var pageModel = CreatePageModel(Profile(), metadata: OneSectionMetadata(), noteTypes: ThreeNoteTypes());

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Null(pageModel.SelectedReferenceTypeId);
        Assert.Equal(string.Empty, pageModel.NotesHeading);
        Assert.Empty(pageModel.Rows);
        Assert.Equal(0, pageModel.TotalRecords);
    }

    [Fact]
    public async Task OnGetAsync_UsesTheRequestedReferenceType_WhenItExists()
    {
        var pageModel = CreatePageModel(Profile(), metadata: OneSectionMetadata(), noteTypes: ThreeNoteTypes());
        pageModel.ReferenceTypeId = LegislativeReferenceId;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(LegislativeReferenceId, pageModel.SelectedReferenceTypeId);
        Assert.Equal("Legislative references", pageModel.NotesHeading);
    }

    [Fact]
    public async Task OnGetAsync_FiltersNotesBySelectedReferenceType()
    {
        var pageModel = CreatePageModel(
            Profile(),
            metadata: OneSectionMetadata(),
            noteTypes: ThreeNoteTypes(),
            notesByVersionAndNoteType: new Dictionary<Guid, IReadOnlyList<ProfileNoteDto>>
            {
                [FurtherInformationId] = [Note("A useful website")],
                [LegislativeReferenceId] = [Note("Council Directive 2009/156/EC")]
            });
        pageModel.ReferenceTypeId = FurtherInformationId;

        await pageModel.OnGetAsync(CancellationToken.None);

        var row = Assert.Single(pageModel.Rows);
        Assert.Equal("A useful website", row.ReferenceHtml);
        Assert.Equal(1, pageModel.TotalRecords);
    }

    [Fact]
    public async Task OnGetAsync_ShowsNoNotes_WhenTheSelectedReferenceTypeHasNone()
    {
        var pageModel = CreatePageModel(Profile(), metadata: OneSectionMetadata(), noteTypes: ThreeNoteTypes());
        pageModel.ReferenceTypeId = FurtherInformationId;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Empty(pageModel.Rows);
        Assert.Equal(0, pageModel.TotalRecords);
        Assert.Equal(1, pageModel.TotalPages);
    }

    [Fact]
    public async Task OnGetAsync_BuildsTheQuestionReferenceDisplay_FromTheSectionAndQuestionNumber()
    {
        var pageModel = CreatePageModel(
            Profile(),
            metadata: OneSectionMetadata(),
            noteTypes: ThreeNoteTypes(),
            notesByVersionAndNoteType: new Dictionary<Guid, IReadOnlyList<ProfileNoteDto>>
            {
                [FurtherInformationId] = [Note("A paper", questionNumber: 4)]
            });
        pageModel.ReferenceTypeId = FurtherInformationId;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal("1.4", Assert.Single(pageModel.Rows).QuestionReferenceDisplay);
    }

    [Fact]
    public async Task OnGetAsync_SortsByReferenceAscending_ByDefault()
    {
        var pageModel = CreatePageModel(
            Profile(),
            metadata: OneSectionMetadata(),
            noteTypes: ThreeNoteTypes(),
            notesByVersionAndNoteType: new Dictionary<Guid, IReadOnlyList<ProfileNoteDto>>
            {
                [FurtherInformationId] = [Note("Zebra reference"), Note("Avian reference")]
            });
        pageModel.ReferenceTypeId = FurtherInformationId;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(["Avian reference", "Zebra reference"], pageModel.Rows.Select(row => row.ReferenceHtml));
    }

    [Fact]
    public async Task OnGetAsync_SortsDescending_WhenRequested()
    {
        var pageModel = CreatePageModel(
            Profile(),
            metadata: OneSectionMetadata(),
            noteTypes: ThreeNoteTypes(),
            notesByVersionAndNoteType: new Dictionary<Guid, IReadOnlyList<ProfileNoteDto>>
            {
                [FurtherInformationId] = [Note("Zebra reference"), Note("Avian reference")]
            });
        pageModel.ReferenceTypeId = FurtherInformationId;
        pageModel.SortDirection = "Descending";

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(["Zebra reference", "Avian reference"], pageModel.Rows.Select(row => row.ReferenceHtml));
    }

    [Fact]
    public async Task OnGetAsync_SortsByQuestionReference_WhenRequested()
    {
        var pageModel = CreatePageModel(
            Profile(),
            metadata: OneSectionMetadata(),
            noteTypes: ThreeNoteTypes(),
            notesByVersionAndNoteType: new Dictionary<Guid, IReadOnlyList<ProfileNoteDto>>
            {
                [FurtherInformationId] = [Note("With a reference", questionNumber: 4), Note("Without a reference")]
            });
        pageModel.ReferenceTypeId = FurtherInformationId;
        pageModel.SortColumn = "QuestionReference";

        await pageModel.OnGetAsync(CancellationToken.None);

        // Ascending: empty question reference sorts before "1.4".
        Assert.Equal(["Without a reference", "With a reference"], pageModel.Rows.Select(row => row.ReferenceHtml));
    }

    [Fact]
    public async Task OnGetAsync_PaginatesResults_UsingTheSelectedPageSize()
    {
        var notes = Enumerable.Range(1, 12).Select(index => Note($"Reference {index:00}")).ToList();
        var pageModel = CreatePageModel(
            Profile(),
            metadata: OneSectionMetadata(),
            noteTypes: ThreeNoteTypes(),
            notesByVersionAndNoteType: new Dictionary<Guid, IReadOnlyList<ProfileNoteDto>> { [FurtherInformationId] = notes });
        pageModel.ReferenceTypeId = FurtherInformationId;
        pageModel.PageSize = "10";

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(10, pageModel.Rows.Count);
        Assert.Equal(12, pageModel.TotalRecords);
        Assert.Equal(2, pageModel.TotalPages);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsTheSecondPage_WhenRequested()
    {
        var notes = Enumerable.Range(1, 12).Select(index => Note($"Reference {index:00}")).ToList();
        var pageModel = CreatePageModel(
            Profile(),
            metadata: OneSectionMetadata(),
            noteTypes: ThreeNoteTypes(),
            notesByVersionAndNoteType: new Dictionary<Guid, IReadOnlyList<ProfileNoteDto>> { [FurtherInformationId] = notes });
        pageModel.ReferenceTypeId = FurtherInformationId;
        pageModel.PageSize = "10";
        pageModel.PageNumber = 2;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(2, pageModel.Rows.Count);
    }

    [Fact]
    public async Task OnGetAsync_ClampsPageNumber_WhenRequestedPageIsBeyondTheLastPage()
    {
        var pageModel = CreatePageModel(
            Profile(),
            metadata: OneSectionMetadata(),
            noteTypes: ThreeNoteTypes(),
            notesByVersionAndNoteType: new Dictionary<Guid, IReadOnlyList<ProfileNoteDto>> { [FurtherInformationId] = [Note("Only reference")] });
        pageModel.ReferenceTypeId = FurtherInformationId;
        pageModel.PageNumber = 99;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(1, pageModel.PageNumber);
        Assert.Single(pageModel.Rows);
    }

    [Fact]
    public void BuildPageUrl_IncludesEveryFilterAndTheRequestedPage()
    {
        var pageModel = CreatePageModel();
        pageModel.SortColumn = "QuestionReference";
        pageModel.SortDirection = "Descending";
        pageModel.PageSize = "25";
        pageModel.Url = CreateUrlHelper();

        var url = pageModel.BuildPageUrl(3);

        Assert.NotNull(url);
        Assert.Contains("PageNumber=3", url);
        Assert.Contains("PageSize=25", url);
        Assert.Contains("SortColumn=QuestionReference", url);
        Assert.Contains("SortDirection=Descending", url);
    }

    [Fact]
    public void BuildSortUrl_TogglesDirection_WhenTheSameColumnIsRequestedAgain()
    {
        var pageModel = CreatePageModel();
        pageModel.SortColumn = "Reference";
        pageModel.SortDirection = "Ascending";
        pageModel.Url = CreateUrlHelper();

        var url = pageModel.BuildSortUrl("Reference");

        Assert.NotNull(url);
        Assert.Contains("SortDirection=Descending", url);
        Assert.Contains("PageNumber=1", url);
    }

    [Fact]
    public void BuildSortUrl_ResetsToAscending_WhenADifferentColumnIsRequested()
    {
        var pageModel = CreatePageModel();
        pageModel.SortColumn = "Reference";
        pageModel.SortDirection = "Descending";
        pageModel.Url = CreateUrlHelper();

        var url = pageModel.BuildSortUrl("QuestionReference");

        Assert.NotNull(url);
        Assert.Contains("SortColumn=QuestionReference", url);
        Assert.Contains("SortDirection=Ascending", url);
    }

    private static FakeUrlHelper CreateUrlHelper() => new(values =>
    {
        var query = string.Join("&", values.Select(pair => $"{pair.Key}={pair.Value}"));

        return $"/SurveillanceProfiles/Notes?{query}";
    });

    /// <summary>Minimal <see cref="IUrlHelper"/> double: only <see cref="RouteUrl"/> is used by <see cref="NotesModel"/>.</summary>
    private sealed class FakeUrlHelper(Func<RouteValueDictionary, string?> routeUrl) : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new(
            new DefaultHttpContext(),
            new RouteData(),
            new PageActionDescriptor());

        public string? Action(UrlActionContext actionContext) => throw new NotSupportedException();

        public string? Content(string? contentPath) => throw new NotSupportedException();

        public bool IsLocalUrl(string? url) => throw new NotSupportedException();

        public string? Link(string? routeName, object? values) => throw new NotSupportedException();

        public string? RouteUrl(UrlRouteContext routeContext) => routeUrl(new RouteValueDictionary(routeContext.Values));
    }
}
