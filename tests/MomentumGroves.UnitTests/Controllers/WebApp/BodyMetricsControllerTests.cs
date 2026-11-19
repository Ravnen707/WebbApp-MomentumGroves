using Microsoft.AspNetCore.Mvc;
using MomentumGroves.Controllers;

namespace MomentumGroves.Tests.Controllers;

[TestFixture]
public class BodyMetricsControllerTests
{
    [Test]
    public void Index_ReturnsDefaultView()
    {
        using var controller = new BodyMetricsController();

        var result = controller.Index();

        Assert.That(result, Is.TypeOf<ViewResult>());
        Assert.That(((ViewResult)result).ViewName, Is.Null);
    }
}
