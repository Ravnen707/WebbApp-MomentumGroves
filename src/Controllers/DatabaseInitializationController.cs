using Microsoft.AspNetCore.Mvc;
using MomentumGroves.Service;

namespace MomentumGroves.Controllers;

[Route("api/database-initialization")]
public sealed class DatabaseInitializationController : Controller
{
    private readonly DatabaseInitializationService _initialization;

    /// <summary>
    /// Creates the controller responsible for the local-only database setup endpoints.
    /// </summary>
    /// <param name="initialization">The database initialization service.</param>
    public DatabaseInitializationController(DatabaseInitializationService initialization)
    {
        _initialization = initialization;
    }

    /// <summary>
    /// Returns the database setup status without opening or changing any databases.
    /// </summary>
    /// <returns>The current setup status, or forbidden for non-loopback requests.</returns>
    [HttpGet("status")]
    public IActionResult Status()
    {
        if (!IsLocalRequest())
        {
            return Forbid();
        }

        return Ok(_initialization.GetStatus());
    }

    /// <summary>
    /// Initializes missing Momentum and food-library databases, leaving Frida to the Admin installer.
    /// </summary>
    /// <returns>The resulting database setup status.</returns>
    [HttpPost("initialize")]
    [ValidateAntiForgeryToken]
    public IActionResult Initialize()
    {
        if (!IsLocalRequest())
        {
            return Forbid();
        }

        try
        {
            return Ok(_initialization.InitializeLocalDatabases(HttpContext.RequestServices));
        }
        catch (Exception)
        {
            return StatusCode(500,
                new { message = "Could not initialize the local databases. Check file permissions and try again." });
        }
    }

    /// <summary>
    /// Checks whether the caller is the same computer running the application.
    /// </summary>
    /// <returns>True for loopback requests; otherwise false.</returns>
    private bool IsLocalRequest()
    {
        var address = HttpContext.Connection.RemoteIpAddress;
        return address is not null && System.Net.IPAddress.IsLoopback(address);
    }
}