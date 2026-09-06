using CDC.Web.Features.Landing;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Tests.Features.Landing;

public class LandingControllerTests
{
    [Fact]
    public void Index_ReturnsView()
    {
        var controller = new LandingController();

        var result = controller.Index();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void Internal_ReturnsView()
    {
        var controller = new LandingController();

        var result = controller.Internal();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void External_ReturnsView()
    {
        var controller = new LandingController();

        var result = controller.External();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void Error_ReturnsViewWithRequestId()
    {
        var controller = new LandingController
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
