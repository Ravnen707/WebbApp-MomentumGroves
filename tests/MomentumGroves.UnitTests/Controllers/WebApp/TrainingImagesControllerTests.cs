using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MomentumGroves.Controllers;
using MomentumGroves.Data.TrainingImages;
using MomentumGroves.Service;
using MomentumGroves.Tests.Services;

namespace MomentumGroves.Tests.Controllers.WebApp;

[TestFixture]
public sealed class TrainingImagesControllerTests : DatabaseServiceTests
{
    /// <summary>
    /// Checks that an unavailable cloud gallery returns HTTP 503 even when local images exist.
    /// </summary>
    /// <returns>A task that completes after the controller response and provider request are checked.</returns>
    [Test]
    public async Task List_CloudStorageUnavailable_ReturnsServiceUnavailable()
    {
        var gallery = Path.Combine(Environment.ContentRootPath, "wwwroot", "TrainingImages");
        Directory.CreateDirectory(gallery);
        await File.WriteAllBytesAsync(Path.Combine(gallery, "local.png"), []);

        using var handler = new TestHttp(_ => TestHttp.Response("", HttpStatusCode.ServiceUnavailable));
        using var client = handler.CreateClient("test-storage");
        var configuration = TestHttp.Configuration(
            ("TrainingImages:SupabaseUrl", "https://storage.example.test"),
            ("TrainingImages:ServerKey", "unused-test-value"));
        var store = new SupabaseTrainingImageStore(client, configuration);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.Request.Host = new HostString("localhost");
        var controller = new TrainingImagesController(new TrainingImageService(Environment, store))
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };

        var result = await controller.List(default);

        Assert.That(result.Result, Is.TypeOf<StatusCodeResult>()
            .With.Property(nameof(StatusCodeResult.StatusCode)).EqualTo(StatusCodes.Status503ServiceUnavailable));
        Assert.That(handler.Requests, Has.Count.EqualTo(1));
    }
}
