using MomentumGroves.Data.TrainingImages;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using MomentumGroves.Service;

namespace MomentumGroves.Controllers;

[ApiController]
[Route("api/training-images")]
public sealed class TrainingImagesController(TrainingImageService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TrainingPicture>>> List(CancellationToken token)
    {
        if (service.UsesCloud && !IsLocal())
            return NotFound();
        try
        {
            return Ok(await service.ListAsync(token));
        }
        catch (TrainingImageStorageException) when (service.UsesCloud)
        {
            return Ok(service.List());
        }
    }

    [HttpGet("/TrainingImages/{filename}")]
    public async Task<IActionResult> Read(string filename, [FromServices] MobileFoodSyncService sync,
        CancellationToken token)
    {
        if (!service.UsesCloud || (!IsLocal() && (!Request.IsHttps ||
            !sync.IsAuthorized(Request.Headers["X-Momentum-Sync-Key"].ToString()))))
            return NotFound();

        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
        try
        {
            var image = await service.ReadCloudAsync(filename, token);
            return image is null ? NotFound() : File(image.Bytes, image.MediaType);
        }
        catch (TrainingImageStorageException)
        {
            return StatusCode(503);
        }
    }

    private bool IsLocal() => HttpContext.Connection.RemoteIpAddress is { } ip &&
        IPAddress.IsLoopback(ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip) &&
        (string.Equals(Request.Host.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
         (IPAddress.TryParse(Request.Host.Host.Trim('[', ']'), out var host) && IPAddress.IsLoopback(host))) &&
        !Request.Headers.ContainsKey("Forwarded") && !Request.Headers.ContainsKey("X-Forwarded-For") &&
        Request.Headers["Sec-Fetch-Site"].ToString() is not ("cross-site" or "same-site");

    [HttpPost]
    [RequestSizeLimit(8_500_000)]
    public async Task<ActionResult<TrainingPicture>> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (service.UsesCloud && !IsLocal())
            return NotFound();

        if (file.Length is 0 or > TrainingImageService.MaxUploadBytes)
            return BadRequest("Choose a JPG, PNG or WebP image no larger than 8 MB.");

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        try
        {
            var image = await service.SaveAsync(file.FileName, stream.ToArray(), cancellationToken);
            return Created(image.Url, image);
        }
        catch (TrainingImageStorageException exception)
        {
            return StatusCode(503, exception.Message);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }
}
