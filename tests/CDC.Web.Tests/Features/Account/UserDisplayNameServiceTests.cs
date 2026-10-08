using System.Security.Claims;
using CDC.Auth.Cidm;
using CDC.Auth.Entra;
using CDC.Web.Features.Account;

namespace CDC.Web.Tests.Features.Account;

public class UserDisplayNameServiceTests
{
    private readonly UserDisplayNameService service = new();

    [Fact]
    public void GetDisplayName_ReturnsFallback_WhenUserIsNull() =>
        Assert.Equal(UserDisplayNameService.FallbackDisplayName, service.GetDisplayName(null));

    [Fact]
    public void GetDisplayName_ReturnsFallback_WhenNotAuthenticated()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.Equal(UserDisplayNameService.FallbackDisplayName, service.GetDisplayName(principal));
    }

    [Fact]
    public void GetDisplayName_Internal_PrefersNameClaim()
    {
        var principal = CreateInternalPrincipal(name: "Jane Internal", givenName: "Jane", surname: "Internal", email: "jane@defra.gov.uk");

        Assert.Equal("Jane Internal", service.GetDisplayName(principal));
    }

    [Fact]
    public void GetDisplayName_Internal_FallsBackToGivenNameAndSurname_WhenNameMissing()
    {
        var principal = CreateInternalPrincipal(name: null, givenName: "Jane", surname: "Internal", email: "jane@defra.gov.uk");

        Assert.Equal("Jane Internal", service.GetDisplayName(principal));
    }

    [Fact]
    public void GetDisplayName_Internal_FallsBackToGivenNameOnly_WhenSurnameMissing()
    {
        var principal = CreateInternalPrincipal(name: null, givenName: "Jane", surname: null, email: "jane@defra.gov.uk");

        Assert.Equal("Jane", service.GetDisplayName(principal));
    }

    [Fact]
    public void GetDisplayName_Internal_FallsBackToEmail_WhenOnlyEmailPresent()
    {
        var principal = CreateInternalPrincipal(name: null, givenName: null, surname: null, email: "jane@defra.gov.uk");

        Assert.Equal("jane@defra.gov.uk", service.GetDisplayName(principal));
    }

    [Fact]
    public void GetDisplayName_Internal_ReturnsFallback_WhenNoUsableClaims()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ExternalUserClaimTypes.AuthenticationProvider, EntraAuthenticationDefaults.AuthenticationScheme)
        ], "TestAuthType"));

        Assert.Equal(UserDisplayNameService.FallbackDisplayName, service.GetDisplayName(principal));
    }

    [Fact]
    public void GetDisplayName_External_ReturnsResolvedFullNameClaim()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ExternalUserClaimTypes.AuthenticationProvider, CidmAuthenticationDefaults.AuthenticationScheme),
            new Claim(ExternalUserClaimTypes.FullName, "Jane External")
        ], "TestAuthType"));

        Assert.Equal("Jane External", service.GetDisplayName(principal));
    }

    [Fact]
    public void GetDisplayName_External_ReturnsFallback_WhenFullNameClaimMissing()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ExternalUserClaimTypes.AuthenticationProvider, CidmAuthenticationDefaults.AuthenticationScheme)
        ], "TestAuthType"));

        Assert.Equal(UserDisplayNameService.FallbackDisplayName, service.GetDisplayName(principal));
    }

    private static ClaimsPrincipal CreateInternalPrincipal(string? name, string? givenName, string? surname, string? email)
    {
        var claims = new List<Claim>
        {
            new(ExternalUserClaimTypes.AuthenticationProvider, EntraAuthenticationDefaults.AuthenticationScheme)
        };
        if (name is not null)
        {
            claims.Add(new Claim(ClaimTypes.Name, name));
        }
        if (givenName is not null)
        {
            claims.Add(new Claim(ClaimTypes.GivenName, givenName));
        }
        if (surname is not null)
        {
            claims.Add(new Claim(ClaimTypes.Surname, surname));
        }
        if (email is not null)
        {
            claims.Add(new Claim(ClaimTypes.Email, email));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthType"));
    }
}
