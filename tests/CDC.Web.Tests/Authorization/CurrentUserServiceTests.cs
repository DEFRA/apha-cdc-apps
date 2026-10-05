using CDC.Web.Authorization.Models;
using CDC.Web.Authorization.Services;
using Microsoft.Extensions.Options;

namespace CDC.Web.Tests.Authorization;

public class CurrentUserServiceTests
{
    [Fact]
    public void UserIdAndRoles_ComeFromConfiguredSettings()
    {
        var settings = new AuthorizationSettings { PlaceholderUserId = "test-user", PlaceholderRoles = ["ProfileEditor", "Reviewer"] };
        var service = new CurrentUserService(Options.Create(settings));

        Assert.Equal("test-user", service.UserId);
        Assert.Equal(["ProfileEditor", "Reviewer"], service.Roles);
    }

    [Fact]
    public void Roles_IsEmpty_WhenNotConfigured()
    {
        var service = new CurrentUserService(Options.Create(new AuthorizationSettings()));

        Assert.Empty(service.Roles);
        Assert.Empty(service.UserId);
    }
}
