using CDC.Common.Contracts;
using CDC.Web.Models;
using CDC.Web.Pages.SurveillanceProfiles;
using CDC.Web.Tests.Features.Landing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static CDC.Web.Tests.Pages.SurveillanceProfiles.ProfileTabTestData;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

public class EditProfileContributionsModelTests
{
    private static EditProfileContributionsModel CreatePageModel(
        ManageProfileViewModel? manageProfile,
        IReadOnlyList<ProfileSectionContributionDto>? contributions = null,
        Exception? throwOnGetProfileVersionSectionContributions = null) =>
        Configure(new EditProfileContributionsModel(
            new FakeApiClient(manageProfile: manageProfile),
            new FakeProfileSectionsApiService(
                metadata: TwoSectionMetadata(),
                contributions: contributions,
                throwOnGetProfileVersionSectionContributions: throwOnGetProfileVersionSectionContributions),
            new AlwaysEnabledLogger<EditProfileContributionsModel>()));

    private static ProfileSectionContributionDto Contribution(string userName, string fullName, string organisation, DateTime lastContributionDate) => new()
    {
        UserName = userName,
        FullName = fullName,
        OrganisationName = organisation,
        LastContributionDate = lastContributionDate
    };

    // Three contributors whose order differs by every column.
    private static List<ProfileSectionContributionDto> ThreeContributions() =>
    [
        Contribution("bsmith", "Zoe Adams", "Defra", new DateTime(2026, 9, 11, 9, 0, 0, DateTimeKind.Utc)),
        Contribution("asmith", "Mia Brown", "APHA", new DateTime(2026, 9, 13, 9, 0, 0, DateTimeKind.Utc)),
        Contribution("csmith", "Abe Clark", "Cefas", new DateTime(2026, 9, 12, 9, 0, 0, DateTimeKind.Utc))
    ];

    [Fact]
    public void ActiveTab_IsContributions() =>
        Assert.Equal(ProfileSectionTab.Contributions, CreatePageModel(Profile()).ActiveTab);

    [Fact]
    public async Task OnGetAsync_LoadsContributions_WithTheDateAndTimeInTheLegacyFormat()
    {
        var contributions = new List<ProfileSectionContributionDto>
        {
            Contribution("jdoe", "Jane Doe", "APHA", new DateTime(2026, 9, 11, 13, 14, 15, DateTimeKind.Utc))
        };

        var pageModel = CreatePageModel(DraftProfile(), contributions);

        await pageModel.OnGetAsync(CancellationToken.None);

        var contribution = Assert.Single(pageModel.Contributions);
        Assert.Equal("Jane Doe", contribution.FullName);
        Assert.Equal("jdoe", contribution.UserName);
        Assert.Equal("APHA", contribution.OrganisationName);
        Assert.Equal("11/09/26 13:14:15", contribution.LastContributionDate);
        Assert.Equal(1, pageModel.TotalRecords);
        Assert.Equal(1, pageModel.TotalPages);
    }

    [Fact]
    public async Task OnGetAsync_ShowsTheNewestContributionFirst_ByDefault()
    {
        var pageModel = CreatePageModel(DraftProfile(), ThreeContributions());

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(["asmith", "csmith", "bsmith"], pageModel.Contributions.Select(contribution => contribution.UserName));
        Assert.Equal(EditProfileContributionsModel.ContributionDateSortColumn, pageModel.SortColumn);
        Assert.Equal(EditProfileContributionsModel.DescendingDirection, pageModel.SortDirection);
    }

    [Theory]
    [InlineData("UserName", "Ascending", new[] { "asmith", "bsmith", "csmith" })]
    [InlineData("UserName", "Descending", new[] { "csmith", "bsmith", "asmith" })]
    [InlineData("FullName", "Ascending", new[] { "csmith", "asmith", "bsmith" })]
    [InlineData("FullName", "Descending", new[] { "bsmith", "asmith", "csmith" })]
    [InlineData("OrganisationName", "Ascending", new[] { "asmith", "csmith", "bsmith" })]
    [InlineData("OrganisationName", "Descending", new[] { "bsmith", "csmith", "asmith" })]
    [InlineData("LastContributionDate", "Ascending", new[] { "bsmith", "csmith", "asmith" })]
    [InlineData("LastContributionDate", "Descending", new[] { "asmith", "csmith", "bsmith" })]
    [InlineData("username", "ascending", new[] { "asmith", "bsmith", "csmith" })]
    public async Task OnGetAsync_SortsByTheChosenColumnAndDirection(string column, string direction, string[] expectedUserNames)
    {
        var pageModel = CreatePageModel(DraftProfile(), ThreeContributions());
        pageModel.SortColumn = column;
        pageModel.SortDirection = direction;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(expectedUserNames, pageModel.Contributions.Select(contribution => contribution.UserName));
    }

    [Theory]
    [InlineData("Password", "Sideways")]
    [InlineData(null, null)]
    public async Task OnGetAsync_FallsBackToTheNewestFirst_ForAnUnknownColumnOrDirection(string? column, string? direction)
    {
        var pageModel = CreatePageModel(DraftProfile(), ThreeContributions());
        pageModel.SortColumn = column!;
        pageModel.SortDirection = direction!;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(["asmith", "csmith", "bsmith"], pageModel.Contributions.Select(contribution => contribution.UserName));
        Assert.Equal(EditProfileContributionsModel.ContributionDateSortColumn, pageModel.SortColumn);
        Assert.Equal(EditProfileContributionsModel.DescendingDirection, pageModel.SortDirection);
    }

    [Fact]
    public async Task OnGetAsync_PagesTheContributions_TenToAPageByDefault()
    {
        var contributions = Enumerable.Range(1, 12)
            .Select(index => Contribution($"user{index:00}", $"User {index:00}", "APHA", new DateTime(2026, 9, index, 0, 0, 0, DateTimeKind.Utc)))
            .ToList();
        var pageModel = CreatePageModel(DraftProfile(), contributions);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(10, pageModel.Contributions.Count);
        Assert.Equal("user12", pageModel.Contributions[0].UserName);
        Assert.Equal(12, pageModel.TotalRecords);
        Assert.Equal(2, pageModel.TotalPages);
        Assert.Equal(1, pageModel.ListPageNumber);
        Assert.Equal(2, pageModel.ListTotalPages);
    }

    [Theory]
    [InlineData(2, 2, "user02")]
    [InlineData(9, 2, "user02")]
    [InlineData(0, 1, "user12")]
    [InlineData(-3, 1, "user12")]
    public async Task OnGetAsync_ShowsTheRequestedPage_KeepingItWithinTheAvailablePages(int requested, int expectedPage, string expectedFirstUserName)
    {
        var contributions = Enumerable.Range(1, 12)
            .Select(index => Contribution($"user{index:00}", $"User {index:00}", "APHA", new DateTime(2026, 9, index, 0, 0, 0, DateTimeKind.Utc)))
            .ToList();
        var pageModel = CreatePageModel(DraftProfile(), contributions);
        pageModel.PageNumber = requested;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(expectedPage, pageModel.PageNumber);
        Assert.Equal(expectedFirstUserName, pageModel.Contributions[0].UserName);
    }

    [Theory]
    [InlineData("25", "25", 12)]
    [InlineData("abc", "10", 10)]
    [InlineData("7", "10", 10)]
    [InlineData(null, "10", 10)]
    public async Task OnGetAsync_UsesTheChosenPageSize_AndFallsBackToTheFirstOptionForAnUnknownOne(string? requested, string expectedSize, int expectedRows)
    {
        var contributions = Enumerable.Range(1, 12)
            .Select(index => Contribution($"user{index:00}", $"User {index:00}", "APHA", new DateTime(2026, 9, index, 0, 0, 0, DateTimeKind.Utc)))
            .ToList();
        var pageModel = CreatePageModel(DraftProfile(), contributions);
        pageModel.PageSize = requested!;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(expectedSize, pageModel.PageSize);
        Assert.Equal(expectedRows, pageModel.Contributions.Count);
    }

    [Fact]
    public async Task BuildSortUrl_SortsAnotherColumnAscendingFirst_AndTogglesTheColumnTheTableIsSortedBy()
    {
        var pageModel = CreatePageModel(DraftProfile(), ThreeContributions());
        await pageModel.OnGetAsync(CancellationToken.None);
        var page = $"/SurveillanceProfiles/EditProfileContributions?profileId={ProfileId}&section={SummarySectionId}";

        Assert.Equal($"{page}&pageNumber=1&pageSize=10&sortColumn=UserName&sortDirection=Ascending", pageModel.BuildSortUrl(EditProfileContributionsModel.UserNameSortColumn));
        Assert.Equal($"{page}&pageNumber=1&pageSize=10&sortColumn=LastContributionDate&sortDirection=Ascending", pageModel.BuildSortUrl(EditProfileContributionsModel.ContributionDateSortColumn));

        pageModel.SortColumn = EditProfileContributionsModel.FullNameSortColumn;
        pageModel.SortDirection = EditProfileContributionsModel.AscendingDirection;

        Assert.Equal($"{page}&pageNumber=1&pageSize=10&sortColumn=FullName&sortDirection=Descending", pageModel.BuildSortUrl(EditProfileContributionsModel.FullNameSortColumn));
    }

    [Fact]
    public async Task GetAriaSort_ReportsTheDirectionOfTheSortedColumnOnly()
    {
        var pageModel = CreatePageModel(DraftProfile(), ThreeContributions());
        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal("descending", pageModel.GetAriaSort(EditProfileContributionsModel.ContributionDateSortColumn));
        Assert.Equal("none", pageModel.GetAriaSort(EditProfileContributionsModel.UserNameSortColumn));

        pageModel.SortDirection = EditProfileContributionsModel.AscendingDirection;

        Assert.Equal("ascending", pageModel.GetAriaSort(EditProfileContributionsModel.ContributionDateSortColumn));
    }

    [Fact]
    public async Task BuildListUrl_KeepsTheSortAndThePageSize_ForTheCurrentOrAnotherPage()
    {
        var pageModel = CreatePageModel(DraftProfile(), ThreeContributions());
        pageModel.SortColumn = EditProfileContributionsModel.OrganisationSortColumn;
        pageModel.SortDirection = EditProfileContributionsModel.AscendingDirection;
        pageModel.PageSize = "25";
        await pageModel.OnGetAsync(CancellationToken.None);
        var page = $"/SurveillanceProfiles/EditProfileContributions?profileId={ProfileId}&section={SummarySectionId}";

        Assert.Equal($"{page}&pageNumber=1&pageSize=25&sortColumn=OrganisationName&sortDirection=Ascending", pageModel.BuildListUrl());
        Assert.Equal($"{page}&pageNumber=3&pageSize=25&sortColumn=OrganisationName&sortDirection=Ascending", pageModel.BuildListUrl(3));
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenTheUserCannotViewContributions()
    {
        var contributions = new List<ProfileSectionContributionDto> { new() { FullName = "Jane Doe" } };

        var pageModel = CreatePageModel(Profile(), contributions);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(pageModel.Contributions);
    }

    [Fact]
    public async Task OnGetAsync_DoesNotFail_WhenLoadingContributionsThrows()
    {
        var pageModel = CreatePageModel(DraftProfile(), throwOnGetProfileVersionSectionContributions: new HttpRequestException("boom"));

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.HasError);
        Assert.Empty(pageModel.Contributions);
        Assert.Equal(0, pageModel.TotalRecords);
        Assert.Equal(1, pageModel.TotalPages);
    }
}
