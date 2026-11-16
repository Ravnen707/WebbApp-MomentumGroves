using Microsoft.AspNetCore.Mvc;
using MomentumGroves.Models;
using MomentumGroves.Service;

namespace MomentumGroves.Controllers;

[Route("Admin")]
public sealed class AdminController : Controller
{
    private readonly FridaUpdateService _updates;

    /// <summary>
    /// Initializes the administration controller.
    /// </summary>
    /// <param name="updates"></param>
    public AdminController(FridaUpdateService updates)
    {
        _updates = updates;
    }

    /// <summary>
    /// Displays the administration page for local requests.
    /// </summary>
    /// <returns></returns>
    [HttpGet("")]
    public IActionResult Index()
    {
        if (!IsLocalRequest())
        {
            return Forbid();
        }

        return View();
    }

    /// <summary>
    /// Returns the installed food database version and food count for local requests.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
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
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
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
    /// Explaining the check api
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
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
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost("api/install")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Install([FromBody] FridaInstallRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsLocalRequest())
        {
            return Forbid();
        }

        return Ok(await _updates.InstallAsync(request, cancellationToken));
    }

    /// <summary>
    /// Determines whether the request originates from a loopback address.
    /// </summary>
    /// <returns></returns>
    private bool IsLocalRequest()
    {
        var address = HttpContext.Connection.RemoteIpAddress;
        return address is not null && System.Net.IPAddress.IsLoopback(address);
    }
}