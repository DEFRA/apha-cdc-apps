using CDC.Web.Features.Account;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Http;

namespace CDC.Web.Tests.Features.Account;

public class OtherSignInSessionTests
{
    [Fact]
    public async Task EndAsync_SignsTheRequestOutOfTheOtherProvidersCookieScheme()
    {
        var (accessor, authService) = FakeAuthenticationService.CreateAccessor();

        await OtherSignInSession.EndAsync(accessor, "Other.Cookie");

        Assert.Equal(["Other.Cookie"], authService.SignOutSchemes);
    }

    [Fact]
    public void EndAsync_DoesNothing_WithoutARequest()
    {
        Assert.True(OtherSignInSession.EndAsync(null, "Other.Cookie").IsCompletedSuccessfully);
        Assert.True(OtherSignInSession.EndAsync(new HttpContextAccessor(), "Other.Cookie").IsCompletedSuccessfully);
    }
}
