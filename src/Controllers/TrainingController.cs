using Microsoft.AspNetCore.Mvc;
using MomentumGroves.Service;

namespace MomentumGroves.Controllers;

public class TrainingController : Controller
{
    /// <summary>
    /// Displays the training page.
    /// </summary>
    /// <returns>The training view.</returns>
    public IActionResult Index() => View();

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
    /// <returns>No content on success or a bad request for invalid programme data.</returns>
    [HttpPut("/api/training/programmes")]
    public async Task<IActionResult> Save([FromServices] MomentumDataService data,
        [FromBody] MomentumDataService.TrainingSave save, CancellationToken cancellationToken)
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
        catch (ArgumentException)
        {
            return BadRequest();
        }
    }

    /// <summary>
    /// Validates and applies mobile exercise weight edits over an authenticated HTTPS connection.
    /// </summary>
    /// <returns>The updated snapshot and edit outcomes, or a request rejection.</returns>
    [HttpPost("/api/mobile/training/sync")]
    public async Task<IActionResult> Sync([FromServices] MomentumDataService data,
        [FromServices] MobileFoodSyncService authentication, [FromBody] List<MomentumDataService.WeightEdit> edits,
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

        if (!ModelState.IsValid || edits is null || edits.Count > 100 ||
            edits.Any(edit => edit is null || edit.ChangeId == Guid.Empty ||
                string.IsNullOrWhiteSpace(edit.ExerciseId) || edit.ExerciseId.Length > 300 ||
                edit.Kg < 0 || edit.BaseKg < 0))
        {
            return BadRequest();
        }

        var result = await data.SyncTrainingAsync(edits, cancellationToken);
        Response.Headers.ETag = $"\"{result.Version()}\"";
        Response.Headers.CacheControl = "no-store";
        return Ok(result);
    }
}
