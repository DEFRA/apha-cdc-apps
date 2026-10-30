using System.Reflection;
using CDC.Api.Application;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.DiseaseRanking;
using CDC.Api.Features.DiseaseRankingFilters;
using CDC.Api.Features.ExternalUsers;
using CDC.Api.Features.GlobalUsers;
using CDC.Api.Features.PrioritisationVariables;
using CDC.Api.Features.ProfileContributors;
using CDC.Api.Features.ProfileManagement;
using CDC.Api.Features.Species;
using CDC.Api.Features.StaticReports;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace CDC.Api.Tests.Application;

public class RequireGlobalRoleTests
{
    [Fact]
    public void TheAttribute_CreatesTheGlobalRoleFilter_WithTheNamedRoles()
    {
        var attribute = new RequireGlobalRoleAttribute(GlobalRole.ProfileEditor, GlobalRole.PolicyProfileUser);

        attribute.ImplementationType.Should().Be<GlobalRoleFilter>();
        attribute.Arguments.Should().ContainSingle().Which.Should().BeEquivalentTo(new[] { GlobalRole.ProfileEditor, GlobalRole.PolicyProfileUser });
    }

    [Fact]
    public void TheAttribute_RejectsNullRoles()
    {
        var act = () => new RequireGlobalRoleAttribute(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(true, false, GlobalRole.ProfileEditor, true)]
    [InlineData(false, true, GlobalRole.ProfileEditor, false)]
    [InlineData(false, false, GlobalRole.ProfileEditor, false)]
    [InlineData(false, true, GlobalRole.PolicyProfileUser, true)]
    [InlineData(true, false, GlobalRole.PolicyProfileUser, false)]
    public void TheFilter_AllowsAUserHoldingTheRole_AndRefusesEveryoneElse(bool isEditor, bool isPolicyUser, GlobalRole role, bool allowed)
    {
        var filter = new GlobalRoleFilter([role], User(isEditor, isPolicyUser));

        var act = () => filter.OnAuthorization(AuthorizationContext());

        if (allowed)
        {
            act.Should().NotThrow();
        }
        else
        {
            act.Should().Throw<UnauthorizedAccessException>();
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void TheFilter_AllowsAUserHoldingAnyOneOfSeveralRoles(bool isEditor, bool isPolicyUser)
    {
        var filter = new GlobalRoleFilter([GlobalRole.ProfileEditor, GlobalRole.PolicyProfileUser], User(isEditor, isPolicyUser));

        var act = () => filter.OnAuthorization(AuthorizationContext());

        act.Should().NotThrow();
    }

    [Fact]
    public void TheFilter_RefusesAnAnonymousRequest()
    {
        var filter = new GlobalRoleFilter([GlobalRole.ProfileEditor, GlobalRole.PolicyProfileUser], new RequestUserContext());

        var act = () => filter.OnAuthorization(AuthorizationContext());

        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void TheFilter_RefusesARoleItDoesNotKnow()
    {
        var filter = new GlobalRoleFilter([(GlobalRole)99], User(isEditor: true, isPolicyUser: true));

        var act = () => filter.OnAuthorization(AuthorizationContext());

        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Theory]
    [InlineData(typeof(GlobalUsersController))]
    [InlineData(typeof(ExternalUsersController))]
    [InlineData(typeof(ProfileContributorsController))]
    [InlineData(typeof(PrioritisationVariablesController))]
    [InlineData(typeof(DiseaseRankingFiltersController))]
    public void TheAdministrationControllers_AreRestrictedToTheProfileEditor(Type controller) =>
        RolesOn(controller).Should().Equal(GlobalRole.ProfileEditor);

    [Fact]
    public void TheDiseaseRankingReports_AreRestrictedToTheProfileEditor() =>
        RolesOn(typeof(DiseaseRankingController)).Should().Equal(GlobalRole.ProfileEditor);

    [Theory]
    [InlineData(typeof(ProfileManagementController), nameof(ProfileManagementController.CreateProfile))]
    [InlineData(typeof(ProfileManagementController), nameof(ProfileManagementController.CreateNewProfileVersion))]
    [InlineData(typeof(ProfileManagementController), nameof(ProfileManagementController.DeleteProfileVersion))]
    [InlineData(typeof(ProfileManagementController), nameof(ProfileManagementController.SetProfileVersionPublicAccess))]
    [InlineData(typeof(ProfileManagementController), nameof(ProfileManagementController.UpdateProfileStatus))]
    [InlineData(typeof(SpeciesController), nameof(SpeciesController.UpdateSpeciesAnswerData))]
    [InlineData(typeof(SpeciesController), nameof(SpeciesController.UpdateSpeciesNameParent))]
    [InlineData(typeof(SpeciesController), nameof(SpeciesController.AddSpecies))]
    [InlineData(typeof(SpeciesController), nameof(SpeciesController.ChangeSpeciesPosition))]
    [InlineData(typeof(SpeciesController), nameof(SpeciesController.InactivateSpecies))]
    [InlineData(typeof(SpeciesController), nameof(SpeciesController.DeleteSpecies))]
    [InlineData(typeof(StaticReportsController), nameof(StaticReportsController.SetStaticReportPublicAccess))]
    public void TheChangesToProfilesSpeciesAndStaticReports_AreRestrictedToTheProfileEditor(Type controller, string action)
    {
        var method = controller.GetMethod(action);

        method.Should().NotBeNull();
        method!.GetCustomAttribute<RequireGlobalRoleAttribute>().Should().NotBeNull();
        RolesOn(method).Should().Equal(GlobalRole.ProfileEditor);
    }

    private static IEnumerable<GlobalRole> RolesOn(MemberInfo member)
    {
        var attribute = member.GetCustomAttribute<RequireGlobalRoleAttribute>();

        attribute.Should().NotBeNull($"{member.Name} must name the global roles allowed to call it");

        return (GlobalRole[])attribute!.Arguments![0];
    }

    private static RequestUserContext User(bool isEditor, bool isPolicyUser)
    {
        var context = new RequestUserContext();
        context.SetActingUser(new ActingUser
        {
            Id = Guid.NewGuid(),
            IsProfileEditor = isEditor,
            IsPolicyProfileUser = isPolicyUser,
            SsoUserIdInt = Guid.NewGuid()
        });

        return context;
    }

    private static AuthorizationFilterContext AuthorizationContext() =>
        new(new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()), []);
}
