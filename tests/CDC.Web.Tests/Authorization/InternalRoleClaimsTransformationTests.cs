using System.Security.Claims;
using CDC.Auth.Cidm;
using CDC.Auth.Entra;
using CDC.Web.Authorization;
using CDC.Web.Authorization.Constants;
using CDC.Web.Features.Account;

namespace CDC.Web.Tests.Authorization;

public class InternalRoleClaimsTransformationTests
{
    private static readonly InternalRoleClaimsTransformation Transformation = new();

    [Fact]
    public async Task TransformAsync_AddsProfileEditorRole_WhenIsProfileEditorClaimIsTrue()
    {
        var principal = CreatePrincipal((ExternalUserClaimTypes.IsProfileEditor, "True"));

        var result = await Transformation.TransformAsync(principal);

        Assert.True(result.IsInRole(AuthorizationRoles.ProfileEditor));
    }

    [Fact]
    public async Task TransformAsync_AddsPolicyProfileUserRole_WhenIsPolicyProfileUserClaimIsTrue()
    {
        var principal = CreatePrincipal((ExternalUserClaimTypes.IsPolicyProfileUser, "True"));

        var result = await Transformation.TransformAsync(principal);

        Assert.True(result.IsInRole(AuthorizationRoles.PolicyProfileUser));
    }

    [Fact]
    public async Task TransformAsync_AddsNoRoles_WhenThePermissionClaimsAreFalse()
    {
        var principal = CreatePrincipal(
            (ExternalUserClaimTypes.IsProfileEditor, "False"),
            (ExternalUserClaimTypes.IsPolicyProfileUser, "False"));

        var result = await Transformation.TransformAsync(principal);

        Assert.False(result.IsInRole(AuthorizationRoles.ProfileEditor));
        Assert.False(result.IsInRole(AuthorizationRoles.PolicyProfileUser));
    }

    [Fact]
    public async Task TransformAsync_DoesNotDuplicateTheRoleClaim_WhenRunMoreThanOnce()
    {
        var principal = CreatePrincipal((ExternalUserClaimTypes.IsProfileEditor, "True"));

        await Transformation.TransformAsync(principal);
        var result = await Transformation.TransformAsync(principal);

        Assert.Single(result.FindAll(ClaimTypes.Role), claim => claim.Value == AuthorizationRoles.ProfileEditor);
    }

    [Fact]
    public async Task TransformAsync_IsANoOp_ForAnUnauthenticatedPrincipal()
    {
        var identity = new ClaimsIdentity();
        var principal = new ClaimsPrincipal(identity);

        var result = await Transformation.TransformAsync(principal);

        Assert.False(result.IsInRole(AuthorizationRoles.ProfileEditor));
    }

    // An external user holds no global role: the role claims in a CIDM token describe their organisation and are not ours to grant.
    [Theory]
    [InlineData(AuthorizationRoles.ProfileEditor)]
    [InlineData(AuthorizationRoles.PolicyProfileUser)]
    [InlineData(AuthorizationRoles.UserManagementSystem)]
    [InlineData("Reviewer")]
    [InlineData("Employee")]
    public async Task TransformAsync_RemovesTheRoleClaimsOfAnExternalUser(string cidmRole)
    {
        var principal = CreatePrincipal(
            (ExternalUserClaimTypes.AuthenticationProvider, CidmAuthenticationDefaults.AuthenticationScheme),
            (ClaimTypes.Role, cidmRole));

        var result = await Transformation.TransformAsync(principal);

        Assert.Empty(result.FindAll(ClaimTypes.Role));
        Assert.False(result.IsInRole(cidmRole));
    }

    [Fact]
    public async Task TransformAsync_GrantsNoRoleToAnExternalUser_EvenWhenTheClaimsCarryThePermissionFlags()
    {
        var principal = CreatePrincipal(
            (ExternalUserClaimTypes.AuthenticationProvider, CidmAuthenticationDefaults.AuthenticationScheme),
            (ExternalUserClaimTypes.IsProfileEditor, "True"),
            (ExternalUserClaimTypes.IsPolicyProfileUser, "True"));

        var result = await Transformation.TransformAsync(principal);

        Assert.False(result.IsInRole(AuthorizationRoles.ProfileEditor));
        Assert.False(result.IsInRole(AuthorizationRoles.PolicyProfileUser));
    }

    [Fact]
    public async Task TransformAsync_KeepsTheOtherClaimsOfAnExternalUser_AndIsRepeatable()
    {
        var principal = CreatePrincipal(
            (ExternalUserClaimTypes.AuthenticationProvider, CidmAuthenticationDefaults.AuthenticationScheme),
            (ExternalUserClaimTypes.InternalUserId, "6f1c2a3b-0000-4000-8000-000000000001"),
            (ClaimTypes.Role, "Employee"));

        await Transformation.TransformAsync(principal);
        var result = await Transformation.TransformAsync(principal);

        Assert.Equal("6f1c2a3b-0000-4000-8000-000000000001", result.FindFirst(ExternalUserClaimTypes.InternalUserId)?.Value);
        Assert.Equal(CidmAuthenticationDefaults.AuthenticationScheme, result.FindFirst(ExternalUserClaimTypes.AuthenticationProvider)?.Value);
        Assert.Empty(result.FindAll(ClaimTypes.Role));
    }

    [Fact]
    public async Task TransformAsync_StillGrantsAnInternalUserTheirRoles_WhateverRolesTheirTokenCarried()
    {
        var principal = CreatePrincipal(
            (ExternalUserClaimTypes.AuthenticationProvider, EntraAuthenticationDefaults.AuthenticationScheme),
            (ExternalUserClaimTypes.IsProfileEditor, "True"));

        var result = await Transformation.TransformAsync(principal);

        Assert.True(result.IsInRole(AuthorizationRoles.ProfileEditor));
        Assert.False(result.IsInRole(AuthorizationRoles.PolicyProfileUser));
    }

    private static ClaimsPrincipal CreatePrincipal(params (string Type, string Value)[] claims)
    {
        var identity = new ClaimsIdentity(
            claims.Select(claim => new Claim(claim.Type, claim.Value)),
            authenticationType: "Test");

        return new ClaimsPrincipal(identity);
    }
}
