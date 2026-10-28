using CDC.Api.Application;
using CDC.Api.Domain.Entities;
using FluentAssertions;

namespace CDC.Api.Tests.Application;

public class RequestUserContextTests
{
    private static readonly Guid UserId = Guid.Parse("88d1e0c6-7d10-427d-a82f-33fc2c1cef28");
    private static readonly Guid ProfileId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid OtherProfileId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid SectionId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid OtherSectionId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    [Fact]
    public void ARequestWithNoActingUser_IsAnonymous_WithNoRole()
    {
        var context = new RequestUserContext();

        context.UserId.Should().BeNull();
        context.Kind.Should().Be(UserKind.Unclassified);
        context.IsProfileEditor.Should().BeFalse();
        context.IsPolicyProfileUser.Should().BeFalse();
        context.IsUserManagementSystem.Should().BeFalse();
        context.GetProfileRole(ProfileId).Should().BeNull();
        context.IsAssignedToSection(ProfileId, SectionId).Should().BeFalse();
        context.GetAssignedSectionIds(ProfileId).Should().BeEmpty();
    }

    [Fact]
    public void SetActingUser_ExposesTheIdAndTheRoleFlags_OfAnInternalUser()
    {
        var context = new RequestUserContext();

        context.SetActingUser(Internal(isProfileEditor: true, isPolicyProfileUser: true));

        context.UserId.Should().Be(UserId);
        context.Kind.Should().Be(UserKind.Internal);
        context.IsProfileEditor.Should().BeTrue();
        context.IsPolicyProfileUser.Should().BeTrue();
        context.IsUserManagementSystem.Should().BeFalse();
    }

    [Fact]
    public void AnInternalUser_HoldsNoProfileLevelRole_EvenWhenTheirRecordHasProfileGrants()
    {
        var context = new RequestUserContext();

        context.SetActingUser(Internal(isProfileEditor: false, isPolicyProfileUser: true) with
        {
            ProfileRoles = [new ProfileRoleAssignment(ProfileId, Guid.NewGuid(), "Technical author", true)],
            SectionAssignments = [new ProfileSectionAssignment(ProfileId, SectionId)]
        });

        context.GetProfileRole(ProfileId).Should().BeNull();
        context.IsAssignedToSection(ProfileId, SectionId).Should().BeFalse();
        context.GetAssignedSectionIds(ProfileId).Should().BeEmpty();
    }

    [Fact]
    public void AnExternalUser_HoldsNoGlobalRole_EvenWhenTheirRecordHasTheFlags()
    {
        var context = new RequestUserContext();

        context.SetActingUser(External() with { IsProfileEditor = true, IsPolicyProfileUser = true });

        context.Kind.Should().Be(UserKind.External);
        context.IsProfileEditor.Should().BeFalse();
        context.IsPolicyProfileUser.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void AUserWhoIsNeitherOrBothInternalAndExternal_HoldsNoRoleAtAll(bool hasInternalId, bool hasExternalId)
    {
        var context = new RequestUserContext();

        context.SetActingUser(new ActingUser
        {
            Id = UserId,
            IsProfileEditor = true,
            IsPolicyProfileUser = true,
            SsoUserIdInt = hasInternalId ? Guid.NewGuid() : null,
            SsoUserIdExt = hasExternalId ? Guid.NewGuid() : null,
            ProfileRoles = [new ProfileRoleAssignment(ProfileId, Guid.NewGuid(), "Technical author", true)],
            SectionAssignments = [new ProfileSectionAssignment(ProfileId, SectionId)]
        });

        context.Kind.Should().Be(UserKind.Unclassified);
        context.IsProfileEditor.Should().BeFalse();
        context.IsPolicyProfileUser.Should().BeFalse();
        context.GetProfileRole(ProfileId).Should().BeNull();
        context.GetAssignedSectionIds(ProfileId).Should().BeEmpty();
    }

    [Fact]
    public void AnExternalAuthor_HoldsTheirRoleOnlyOnTheProfilesTheyAreGrantedAndIsAssignedOnlyToTheirSections()
    {
        var role = new ProfileRoleAssignment(ProfileId, Guid.NewGuid(), "VA author", true);
        var context = new RequestUserContext();

        context.SetActingUser(External() with
        {
            ProfileRoles = [role],
            SectionAssignments =
            [
                new ProfileSectionAssignment(ProfileId, SectionId),
                new ProfileSectionAssignment(OtherProfileId, OtherSectionId)
            ]
        });

        context.GetProfileRole(ProfileId).Should().Be(role);
        context.GetProfileRole(OtherProfileId).Should().BeNull();
        context.IsAssignedToSection(ProfileId, SectionId).Should().BeTrue();
        context.IsAssignedToSection(ProfileId, OtherSectionId).Should().BeFalse();
        context.GetAssignedSectionIds(ProfileId).Should().Equal(SectionId);
        context.GetAssignedSectionIds(OtherProfileId).Should().BeEmpty();
    }

    [Fact]
    public void AnExternalReviewer_IsAssignedToNoSection_EvenWhenTheirRecordHasAnAssignment()
    {
        var role = new ProfileRoleAssignment(ProfileId, Guid.NewGuid(), "Policy reviewer", false);
        var context = new RequestUserContext();

        context.SetActingUser(External() with
        {
            ProfileRoles = [role],
            SectionAssignments = [new ProfileSectionAssignment(ProfileId, SectionId)]
        });

        context.GetProfileRole(ProfileId).Should().Be(role);
        context.IsAssignedToSection(ProfileId, SectionId).Should().BeFalse();
        context.GetAssignedSectionIds(ProfileId).Should().BeEmpty();
    }

    [Fact]
    public void RequireUserId_ReturnsTheActingUsersId()
    {
        var context = new RequestUserContext();
        context.SetActingUser(Internal(isProfileEditor: false, isPolicyProfileUser: false));

        context.RequireUserId().Should().Be(UserId);
    }

    [Fact]
    public void RequireUserId_Throws_WhenTheRequestIsAnonymous()
    {
        var act = () => new RequestUserContext().RequireUserId();

        act.Should().Throw<UnauthorizedAccessException>();
    }

    private static ActingUser Internal(bool isProfileEditor, bool isPolicyProfileUser) => new()
    {
        Id = UserId,
        IsProfileEditor = isProfileEditor,
        IsPolicyProfileUser = isPolicyProfileUser,
        SsoUserIdInt = Guid.NewGuid()
    };

    private static ActingUser External() => new()
    {
        Id = UserId,
        IsProfileEditor = false,
        IsPolicyProfileUser = false,
        SsoUserIdExt = Guid.NewGuid()
    };
}
