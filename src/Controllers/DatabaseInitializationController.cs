using Microsoft.AspNetCore.Mvc;
using MomentumGroves.Service;

namespace MomentumGroves.Controllers;

[Route("api/database-initialization")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DatabaseInitializationController(
    DatabaseInitializationService initialization,
    ILogger<DatabaseInitializationController> logger) : Controller
{

    [HttpGet("/database-setup")]
    public IActionResult Index()
    {
        if (!IsLocalRequest())
            return StatusCode(StatusCodes.Status403Forbidden);

        var status = initialization.GetStatus();
        return status.RequiresLocalSetup ? View(status) : RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// Initializes missing Momentum and food-library databases, leaving Frida to the Admin installer.
    /// </summary>
    /// <returns>The resulting database setup status.</returns>
    [HttpGet("status")]
    public IActionResult Status() => IsLocalRequest()
        ? Ok(initialization.GetStatus())
        : StatusCode(StatusCodes.Status403Forbidden);

    [HttpPost("initialize")]
    [HttpPost("/database-setup", Name = "SubmitDatabaseSetup")]
    [ValidateAntiForgeryToken]
    public IActionResult Initialize([FromForm] bool showSetupModal)
    {
        if (!IsLocalRequest())
            return StatusCode(StatusCodes.Status403Forbidden);

        try
        {
            initialization.InitializeLocalDatabases();
            var status = initialization.GetStatus();
            return showSetupModal ? View(nameof(Index), status) : Ok(status);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not initialize local databases");
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            const string message = "Could not create the local databases. Check file permissions and try again.";
            if (!showSetupModal)
                return StatusCode(StatusCodes.Status500InternalServerError, new { message });

            ModelState.AddModelError(string.Empty, message);
            return View(nameof(Index), initialization.GetStatus());
        }
    }

    private bool IsLocalRequest() =>
        HttpContext.Connection.RemoteIpAddress is { } address && System.Net.IPAddress.IsLoopback(address);
}