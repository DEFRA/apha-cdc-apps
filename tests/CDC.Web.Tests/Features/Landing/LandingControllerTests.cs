using CDC.Web.Features.Landing;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Tests.Features.Landing;

public class LandingControllerTests
{
    [Fact]
    public void Index_ReturnsView()
    {
        var controller = new LandingController(new FakeApiClient());

        var result = controller.Index();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void Internal_ReturnsView()
    {
        var controller = new LandingController(new FakeApiClient());

        var result = controller.Internal();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void External_ReturnsView()
    {
        var controller = new LandingController(new FakeApiClient());

        var result = controller.External();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task ApiStatus_ReturnsJsonFromApiClient()
    {
        var expected = new ApiHealthResponse("Healthy", 42, DateTime.UtcNow);
        var controller = new LandingController(new FakeApiClient(expected));

        var result = await controller.ApiStatus(CancellationToken.None);

        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.Equal(expected, jsonResult.Value);
    }

    [Fact]
    public void Error_ReturnsViewWithRequestId()
    {
        var controller = new LandingController(new FakeApiClient())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
            }
        };

        var result = controller.Error();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ErrorViewModel>(viewResult.Model);
        Assert.True(model.ShowRequestId);
    }
}
