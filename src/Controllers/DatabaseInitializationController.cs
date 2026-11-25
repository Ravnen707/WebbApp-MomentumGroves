using Microsoft.AspNetCore.Mvc;
using MomentumGroves.Service;

namespace MomentumGroves.Controllers;

/// <summary>
/// Initializes the controller that guides local database setup.
/// </summary>
/// <param name="initialization">The service that checks and prepares local databases.</param>
/// <param name="logger">The logger for diagnostics and operation failures.</param>
[Route("api/database-initialization")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DatabaseInitializationController(
    DatabaseInitializationService initialization,
    ILogger<DatabaseInitializationController> logger) : Controller
{

    /// <summary>
    /// Displays local database setup when an application database is missing.
    /// </summary>
    /// <returns>The setup page, a redirect to the dashboard, or a forbidden response.</returns>
    [HttpGet("/database-setup")]
    public IActionResult Index()
    {
        if (!IsLocalRequest())
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var status = initialization.GetStatus();
        return status.RequiresLocalSetup ? View(status) : RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// Reports which local application databases still need to be created.
    /// </summary>
    /// <returns>The resulting database setup status.</returns>
    [HttpGet("status")]
    public IActionResult Status()
    {
        return IsLocalRequest()
            ? Ok(initialization.GetStatus())
            : StatusCode(StatusCodes.Status403Forbidden);
    }

    /// <summary>
    /// Creates missing local databases and reports the resulting setup state.
    /// </summary>
    /// <param name="showSetupModal">Whether to render the setup screen in the response.</param>
    /// <returns>The resulting setup state or an HTTP error response.</returns>
    [HttpPost("initialize")]
    [HttpPost("/database-setup", Name = "SubmitDatabaseSetup")]
    [ValidateAntiForgeryToken]
    public IActionResult Initialize([FromForm] bool showSetupModal)
    {
        if (!IsLocalRequest())
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

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
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message });
            }

            ModelState.AddModelError(string.Empty, message);
            return View(nameof(Index), initialization.GetStatus());
        }
    }

    /// <summary>
    /// Checks whether the request originated from a loopback address.
    /// </summary>
    /// <returns>True when the request came from a loopback address.</returns>
    private bool IsLocalRequest()
    {
        return HttpContext.Connection.RemoteIpAddress is { } address && System.Net.IPAddress.IsLoopback(address);
    }
}
