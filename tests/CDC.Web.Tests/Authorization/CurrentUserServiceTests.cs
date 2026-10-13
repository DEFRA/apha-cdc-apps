using CDC.Web.Authorization.Constants;
using CDC.Web.Authorization.Services;

namespace CDC.Web.Tests.Authorization;

public class CurrentUserServiceTests
{
    [Fact]
    public void UserIdAndRoles_AreTheHardcodedPlaceholderProfileEditor()
    {
        var service = new CurrentUserService();

        Assert.Equal(CurrentUserService.PlaceholderUserId, service.UserId);
        Assert.Equal([AuthorizationRoles.ProfileEditor], service.Roles);
    }
}
