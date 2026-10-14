using System.Security.Claims;
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

    private static ClaimsPrincipal CreatePrincipal(params (string Type, string Value)[] claims)
    {
        var identity = new ClaimsIdentity(
            claims.Select(claim => new Claim(claim.Type, claim.Value)),
            authenticationType: "Test");

        return new ClaimsPrincipal(identity);
    }
}
