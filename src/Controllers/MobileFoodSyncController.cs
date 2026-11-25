using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using MomentumGroves.Models;
using MomentumGroves.Service;

namespace MomentumGroves.Controllers;

/// <summary>Receives food imports from the private Android application.</summary>
[ApiController]
[Route("api/mobile/foods")]
public sealed class MobileFoodSyncController : ControllerBase
{
    private readonly MobileFoodSyncService _service;

    /// <summary>
    /// Initializes the mobile food synchronization controller.
    /// </summary>
    /// <param name="service">The application service that performs the requested operation.</param>
    public MobileFoodSyncController(MobileFoodSyncService service)
    {
        _service = service;
    }

    /// <summary>
    /// Imports new foods and acknowledges existing names without modifying them.
    /// </summary>
    /// <param name="payload">The raw JSON food import batch.</param>
    /// <returns>The import acknowledgements or a rejected request.</returns>
    [HttpPost("import")]
    public ActionResult<MobileFoodImportResult> Import([FromBody] JsonElement payload)
    {
        var suppliedKey = Request.Headers["X-Momentum-Sync-Key"].ToString();
        if (!_service.IsAuthorized(suppliedKey))
        {
            return Unauthorized();
        }

        if (!_service.TryDeserializeBatch(payload, out var foods))
        {
            return BadRequest("Invalid food import batch.");
        }

        if (!_service.IsValidBatch(foods))
        {
            return BadRequest("Invalid food import batch.");
        }

        return Ok(_service.Import(foods!));
    }
}
