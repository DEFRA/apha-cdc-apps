using CDC.Web.Pages.SurveillanceProfiles;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

// CloneNewProfile and DeleteCurrentVersion have no corresponding .cshtml view (unlike the other
// "minimal placeholder" pages in this folder), so they aren't reachable through the smoke tests in
// Integration/LandingPageSmokeTests.cs - a direct unit test is the only way to exercise them.
public class PlaceholderPageModelTests
{
    [Fact]
    public void CloneNewProfileModel_OnGet_DoesNotThrow()
    {
        var pageModel = new CloneNewProfileModel();

        pageModel.OnGet();
    }

    [Fact]
    public void DeleteCurrentVersionModel_OnGet_DoesNotThrow()
    {
        var pageModel = new DeleteCurrentVersionModel();

        pageModel.OnGet();
    }
}
