using CDC.Api.Application;
using CDC.Api.Domain.Entities;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ProfileReports.Interfaces;
using FluentAssertions;
using Moq;

namespace CDC.Api.Tests.Application;

public class ProfileVersionGuardTests
{
    private static readonly Guid VersionId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid ProfileId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private readonly Mock<IProfileReportRepository> repository = new();
    private readonly Mock<IUserContext> userContext = new();

    private ProfileVersionGuard CreateGuard() => new(repository.Object, userContext.Object);

    private void GivenVersion(bool isPublished, DateTime? effectiveDateTo = null) =>
        repository
            .Setup(repo => repo.GetProfileVersionHeaderAsync(VersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileVersionHeader
            {
                Id = VersionId,
                ProfileId = ProfileId,
                IsPublished = isPublished,
                EffectiveDateFrom = new DateTime(2026, 1, 1),
                EffectiveDateTo = effectiveDateTo
            });

    private void GivenAnExternalUserWithARoleOnTheProfile() =>
        userContext
            .Setup(context => context.GetProfileRole(ProfileId))
            .Returns(new ProfileRoleAssignment(ProfileId, Guid.NewGuid(), "VA reviewer", false));

    [Fact]
    public async Task FindAsync_ReadsTheFactsOfTheVersion()
    {
        GivenVersion(isPublished: false);

        var facts = await CreateGuard().FindAsync(VersionId, CancellationToken.None);

        facts.Should().Be(new ProfileVersionFacts(ProfileId, IsPublished: false, IsCurrent: true));
    }

    [Fact]
    public async Task FindAsync_ReturnsNull_WhenTheVersionDoesNotExist()
    {
        repository
            .Setup(repo => repo.GetProfileVersionHeaderAsync(VersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileVersionHeader?)null);

        var facts = await CreateGuard().FindAsync(VersionId, CancellationToken.None);

        facts.Should().BeNull();
    }

    [Fact]
    public async Task RequireAsync_Throws_WhenTheVersionDoesNotExist()
    {
        repository
            .Setup(repo => repo.GetProfileVersionHeaderAsync(VersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileVersionHeader?)null);

        var act = () => CreateGuard().RequireAsync(VersionId, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RequireAsync_ReturnsTheFactsOfASupersededPublishedVersion()
    {
        GivenVersion(isPublished: true, effectiveDateTo: new DateTime(2026, 6, 1));

        var facts = await CreateGuard().RequireAsync(VersionId, CancellationToken.None);

        facts.IsPublished.Should().BeTrue();
        facts.IsCurrent.Should().BeFalse();
        facts.IsCurrentDraft.Should().BeFalse();
    }

    [Fact]
    public async Task EnsureCanViewAsync_AllowsAPublishedVersion_ToAUserWithNoRoleOnTheProfile()
    {
        GivenVersion(isPublished: true);

        var act = () => CreateGuard().EnsureCanViewAsync(VersionId, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureCanViewAsync_Throws_ForADraftOfAProfileTheUserHasNoRoleOn()
    {
        GivenVersion(isPublished: false);

        var act = () => CreateGuard().EnsureCanViewAsync(VersionId, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task EnsureCanViewAsync_AllowsADraft_ToAnExternalUserWhoHoldsARoleOnTheProfile()
    {
        GivenVersion(isPublished: false);
        GivenAnExternalUserWithARoleOnTheProfile();

        var act = () => CreateGuard().EnsureCanViewAsync(VersionId, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureCanViewAsync_AllowsADraft_ToAProfileEditor()
    {
        GivenVersion(isPublished: false);
        userContext.SetupGet(context => context.IsProfileEditor).Returns(true);

        var act = () => CreateGuard().EnsureCanViewAsync(VersionId, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureCanViewAsync_AllowsAVersionThatDoesNotExist_BecauseThereIsNothingToProtect()
    {
        repository
            .Setup(repo => repo.GetProfileVersionHeaderAsync(VersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileVersionHeader?)null);

        var act = () => CreateGuard().EnsureCanViewAsync(VersionId, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureCanViewNotesAsync_Throws_ForAPublishedVersionToo_WhenTheUserHasNoRoleOnTheProfile()
    {
        GivenVersion(isPublished: true);

        var act = () => CreateGuard().EnsureCanViewNotesAsync(VersionId, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task EnsureCanViewNotesAsync_AllowsAnExternalUserWhoHoldsARoleOnTheProfile()
    {
        GivenVersion(isPublished: true);
        GivenAnExternalUserWithARoleOnTheProfile();

        var act = () => CreateGuard().EnsureCanViewNotesAsync(VersionId, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureCanViewNotesAsync_AllowsAPolicyProfileUser()
    {
        GivenVersion(isPublished: false);
        userContext.SetupGet(context => context.IsPolicyProfileUser).Returns(true);

        var act = () => CreateGuard().EnsureCanViewNotesAsync(VersionId, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureCanViewNotesAsync_AllowsAVersionThatDoesNotExist()
    {
        repository
            .Setup(repo => repo.GetProfileVersionHeaderAsync(VersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileVersionHeader?)null);

        var act = () => CreateGuard().EnsureCanViewNotesAsync(VersionId, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}

public class ProfileVersionFactsTests
{
    private static readonly Guid ProfileId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, false)]
    public void IsCurrentDraft_IsTrueOnlyForADraftWithNoEffectiveToDate(bool isPublished, bool isCurrent, bool expected) =>
        new ProfileVersionFacts(ProfileId, isPublished, isCurrent).IsCurrentDraft.Should().Be(expected);

    [Fact]
    public void From_ASummary_TakesTheProfileFromTheCaller_AndIsCurrentWhenThereIsNoEffectiveToDate()
    {
        var current = new ProfileVersionSummary { VersionMajor = 1, VersionMinor = 0, IsPublished = false };
        var superseded = new ProfileVersionSummary { VersionMajor = 1, VersionMinor = 0, IsPublished = true, EffectiveDateTo = new DateTime(2026, 1, 1) };

        ProfileVersionFacts.From(ProfileId, current).Should().Be(new ProfileVersionFacts(ProfileId, false, true));
        ProfileVersionFacts.From(ProfileId, superseded).Should().Be(new ProfileVersionFacts(ProfileId, true, false));
    }

    [Fact]
    public void From_RejectsANullHeaderOrSummary()
    {
        var header = () => ProfileVersionFacts.From((ProfileVersionHeader)null!);
        var summary = () => ProfileVersionFacts.From(ProfileId, null!);

        header.Should().Throw<ArgumentNullException>();
        summary.Should().Throw<ArgumentNullException>();
    }
}

public class ProfileRoleCatalogTests
{
    [Theory]
    [InlineData("Technical author", ReviewType.Author)]
    [InlineData("Technical reviewer", ReviewType.Author)]
    [InlineData("Tech author", ReviewType.Author)]
    [InlineData("Tech reviewer", ReviewType.Author)]
    [InlineData("Policy author", ReviewType.Policy)]
    [InlineData("Policy reviewer", ReviewType.Policy)]
    [InlineData("VA author", ReviewType.Va)]
    [InlineData("VA reviewer", ReviewType.Va)]
    [InlineData("  va REVIEWER ", ReviewType.Va)]
    public void ReviewTypeOf_ReadsTheReviewFromTheRoleName(string roleName, ReviewType expected) =>
        ProfileRoleCatalog.ReviewTypeOf(roleName).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Profile editor")]
    public void ReviewTypeOf_ReturnsNull_ForANameThatIsNotAProfileRole(string? roleName) =>
        ProfileRoleCatalog.ReviewTypeOf(roleName).Should().BeNull();
}
