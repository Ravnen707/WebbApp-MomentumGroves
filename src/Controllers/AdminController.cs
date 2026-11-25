using Microsoft.AspNetCore.Mvc;
using MomentumGroves.Models;
using MomentumGroves.Service;

namespace MomentumGroves.Controllers;

[Route("Admin")]
public sealed class AdminController : Controller
{
    private readonly FridaUpdateService _updates;
    private readonly AdminTechnicalStatusService _technicalStatus;

    /// <summary>
    /// Initializes the administration controller.
    /// </summary>
    /// <param name="updates">The service that checks and installs Frida releases.</param>
    /// <param name="technicalStatus">The service that reports the configured storage providers.</param>
    public AdminController(FridaUpdateService updates, AdminTechnicalStatusService technicalStatus)
    {
        _updates = updates;
        _technicalStatus = technicalStatus;
    }

    /// <summary>
    /// Displays the administration page for local requests.
    /// </summary>
    /// <returns>The administration page or a forbidden response.</returns>
    [HttpGet("")]
    public IActionResult Index()
    {
        if (!IsLocalRequest())
        {
            return Forbid();
        }

        return View(_technicalStatus.GetStatus());
    }

    /// <summary>
    /// Returns the installed food database version and food count for local requests.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The installed Frida version and food count, or a forbidden response.</returns>
    [HttpGet("api/status")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        if (!IsLocalRequest())
        {
            return Forbid();
        }

        var installed = await _updates.GetInstalledAsync(cancellationToken);
        return Ok(new { version = installed.Version, foodCount = installed.Count });
    }

    /// <summary>
    /// Checks for food database updates and returns diagnostic results for local requests.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The update check diagnostics or a forbidden response.</returns>
    [HttpGet("api/test")]
    public async Task<IActionResult> Test(CancellationToken cancellationToken)
    {
        if (!IsLocalRequest())
        {
            return Forbid();
        }

        var result = await _updates.CheckAsync(cancellationToken);
        return Ok(new
        {
            result.Success, result.Message, result.InstalledVersion, result.FoodCount, result.Latest,
            result.UpdateAvailable
        });
    }

    /// <summary>
    /// Checks whether a newer Frida release is available for local installation.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The Frida update status or a forbidden response.</returns>
    [HttpGet("api/check")]
    public async Task<IActionResult> Check(CancellationToken cancellationToken)
    {
        if (!IsLocalRequest())
        {
            return Forbid();
        }

        return Ok(await _updates.CheckAsync(cancellationToken));
    }

    /// <summary>
    /// Installs a food database update for local requests.
    /// </summary>
    /// <param name="request">The request containing the selected Frida release.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The installation outcome or a forbidden response.</returns>
    [HttpPost("api/install")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Install([FromBody] FridaInstallRequest? request,
        CancellationToken cancellationToken)
    {
        if (!IsLocalRequest())
        {
            return Forbid();
        }

        if (!ModelState.IsValid || request is null)
        {
            return BadRequest();
        }

        return Ok(await _updates.InstallAsync(request, cancellationToken));
    }

    /// <summary>
    /// Determines whether the request originates from a loopback address.
    /// </summary>
    /// <returns>True when the request came from a loopback address.</returns>
    private bool IsLocalRequest()
    {
        var address = HttpContext.Connection.RemoteIpAddress;
        return address is not null && System.Net.IPAddress.IsLoopback(address);
    }
}
