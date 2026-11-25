using MomentumGroves.Models;
using MomentumGroves.Data.TrainingImages;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using MomentumGroves.Service;

namespace MomentumGroves.Controllers;

/// <summary>
/// Initializes the controller for exercise pictures.
/// </summary>
/// <param name="service">The application service that performs the requested operation.</param>
[ApiController]
[Route("api/training-images")]
public sealed class TrainingImagesController(TrainingImageService service) : ControllerBase
{
    /// <summary>
    /// Lists the exercise pictures available from the configured store.
    /// </summary>
    /// <param name="token">The token used to cancel the operation.</param>
    /// <returns>The exercise picture list or an error response.</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TrainingPicture>>> List(CancellationToken token)
    {
        if (service.UsesCloud && !IsLocal())
        {
            return NotFound();
        }
        try
        {
            return Ok(await service.ListAsync(token));
        }
        catch (TrainingImageStorageException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    /// <summary>
    /// Serves a stored exercise picture to an authorized request.
    /// </summary>
    /// <param name="filename">The requested filename.</param>
    /// <param name="sync">The service that verifies access using the mobile synchronization key.</param>
    /// <param name="token">The token used to cancel the operation.</param>
    /// <returns>The picture file, a missing-item response, or a rejected request.</returns>
    [HttpGet("/TrainingImages/{filename}")]
    public async Task<IActionResult> Read(string filename, [FromServices] MobileFoodSyncService sync,
        CancellationToken token)
    {
        if (!service.UsesCloud || (!IsLocal() && (!Request.IsHttps ||
            !sync.IsAuthorized(Request.Headers["X-Momentum-Sync-Key"].ToString()))))
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
        try
        {
            var image = await service.ReadCloudAsync(filename, token);
            return image is null ? NotFound() : File(image.Bytes, image.MediaType);
        }
        catch (TrainingImageStorageException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    /// <summary>
    /// Checks whether the request originated on the local machine.
    /// </summary>
    /// <returns>True when the request came from a loopback address.</returns>
    private bool IsLocal()
    {
        return HttpContext.Connection.RemoteIpAddress is { } ip &&
            IPAddress.IsLoopback(ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip) &&
            (string.Equals(Request.Host.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
             (IPAddress.TryParse(Request.Host.Host.Trim('[', ']'), out var host) && IPAddress.IsLoopback(host))) &&
            !Request.Headers.ContainsKey("Forwarded") && !Request.Headers.ContainsKey("X-Forwarded-For") &&
            Request.Headers["Sec-Fetch-Site"].ToString() is not ("cross-site" or "same-site");
    }

    /// <summary>
    /// Validates and stores a picture submitted by the local browser.
    /// </summary>
    /// <param name="file">The uploaded exercise picture.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The saved picture details or a request error.</returns>
    [HttpPost]
    [RequestSizeLimit(8_500_000)]
    public async Task<ActionResult<TrainingPicture>> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (service.UsesCloud && !IsLocal())
        {
            return NotFound();
        }

        if (file.Length is 0 or > TrainingImageService.MaxUploadBytes)
        {
            return BadRequest("Choose a JPG, PNG or WebP image no larger than 8 MB.");
        }

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        try
        {
            var image = await service.SaveAsync(file.FileName, stream.ToArray(), cancellationToken);
            return Created(image.Url, image);
        }
        catch (TrainingImageStorageException exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, exception.Message);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }
}
