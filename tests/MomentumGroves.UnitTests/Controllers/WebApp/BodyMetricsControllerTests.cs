using Microsoft.AspNetCore.Mvc;
using MomentumGroves.Controllers;

namespace MomentumGroves.Tests.Controllers;

[TestFixture]
public class BodyMetricsControllerTests
{
    /// <summary>
    /// Checks that the body metrics action returns its default MVC view.
    /// </summary>
    [Test]
    public void Index_ReturnsDefaultView()
    {
        using var controller = new BodyMetricsController();

        var result = controller.Index();

        Assert.That(result, Is.TypeOf<ViewResult>());
        Assert.That(((ViewResult)result).ViewName, Is.Null);
    }
}
