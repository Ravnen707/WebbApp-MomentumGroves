using Microsoft.AspNetCore.Mvc;
using MomentumGroves.Service;
using MomentumGroves.Models;

namespace MomentumGroves.Controllers;

public class TrainingController : Controller
{
    /// <summary>
    /// Displays the training page.
    /// </summary>
    /// <returns>The training view.</returns>
    public IActionResult Index()
    {
        return View();
    }

    /// <summary>
    /// Displays the training page in its phone layout.
    /// </summary>
    /// <returns>The training view with the phone layout enabled.</returns>
    [HttpGet("/Training/Phone")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Phone()
    {
        ViewData["TrainingPhoneView"] = true;
        return View(nameof(Index));
    }

    /// <summary>
    /// Retrieves the training programmes for the paired phone.
    /// </summary>
    /// <param name="data">The application service that manages stored training data.</param>
    /// <param name="authentication">The service that verifies the mobile synchronization key.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The programme snapshot, or a not-modified response for the current version.</returns>
    [HttpGet("/api/mobile/training/sync")]
    public async Task<IActionResult> Snapshot([FromServices] MomentumDataService data,
        [FromServices] MobileFoodSyncService authentication, CancellationToken cancellationToken)
    {
        if (!Request.IsHttps)
        {
            return BadRequest();
        }

        if (!authentication.IsAuthorized(Request.Headers["X-Momentum-Sync-Key"].ToString()))
        {
            return Unauthorized();
        }

        var result = await data.SyncTrainingAsync([], cancellationToken);
        var version = $"\"{result.Version()}\"";
        Response.Headers.ETag = version;
        Response.Headers.CacheControl = "no-store";
        if (Request.Headers.IfNoneMatch.ToString() == version)
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        return Ok(result);
    }

    /// <summary>
    /// Validates and saves the programme document submitted by the browser.
    /// </summary>
    /// <param name="data">The application service that manages stored training data.</param>
    /// <param name="save">The programme document and its original exercise weights.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>No content on success or a bad request for invalid programme data.</returns>
    [HttpPut("/api/training/programmes")]
    public async Task<IActionResult> Save([FromServices] MomentumDataService data,
        [FromBody] TrainingSave save, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || save is null ||
            save.Programmes.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return BadRequest();
        }

        try
        {
            await data.SaveTrainingAsync(save, cancellationToken);
            return NoContent();
        }
        catch (TrainingConflictException)
        {
            return Conflict(new { message = "A weight changed since this editor loaded. Copy any unsaved edits, then reload programmes and review the latest weight." });
        }
        catch (ArgumentException)
        {
            return BadRequest();
        }
    }

    /// <summary>
    /// Validates and applies mobile exercise weight edits over an authenticated HTTPS connection.
    /// </summary>
    /// <param name="data">The application service that manages stored training data.</param>
    /// <param name="authentication">The service that verifies the mobile synchronization key.</param>
    /// <param name="edits">The exercise weight changes submitted by the mobile client.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The updated snapshot and edit outcomes, or a request rejection.</returns>
    [HttpPost("/api/mobile/training/sync")]
    public async Task<IActionResult> Sync([FromServices] MomentumDataService data,
        [FromServices] MobileFoodSyncService authentication, [FromBody] List<TrainingWeightEdit> edits,
        CancellationToken cancellationToken)
    {
        if (!Request.IsHttps)
        {
            return BadRequest();
        }

        if (!authentication.IsAuthorized(Request.Headers["X-Momentum-Sync-Key"].ToString()))
        {
            return Unauthorized();
        }

        if (!ModelState.IsValid || !data.IsValidTrainingEditBatch(edits))
        {
            return BadRequest();
        }

        var result = await data.SyncTrainingAsync(edits!, cancellationToken);
        Response.Headers.ETag = $"\"{result.Version()}\"";
        Response.Headers.CacheControl = "no-store";
        return Ok(result);
    }
}
