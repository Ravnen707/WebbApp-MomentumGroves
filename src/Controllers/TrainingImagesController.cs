using Microsoft.AspNetCore.Mvc;
using MomentumGroves.Service;

namespace MomentumGroves.Controllers;

[ApiController]
[Route("api/training-images")]
public sealed class TrainingImagesController : ControllerBase
{
    private readonly TrainingImageService _service;

    /// <summary>
    /// Initializes the training images controller.
    /// </summary>
    /// <param name="service">The training image service.</param>
    public TrainingImagesController(TrainingImageService service)
    {
        _service = service;
    }

    /// <summary>
    /// Retrieves an exercise image from SQLite.
    /// </summary>
    /// <param name="key">The exercise image key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The image file or a not-found response.</returns>
    [HttpGet("{key}")]
    public async Task<IActionResult> Get(string key, CancellationToken cancellationToken)
    {
        var image = await _service.ReadAsync(key, cancellationToken);
        if (image is null)
        {
            return NotFound();
        }

        return File(image.Value.Data, image.Value.Type);
    }

    /// <summary>
    /// Creates or updates an uploaded exercise image in SQLite.
    /// </summary>
    /// <param name="key">The exercise image key.</param>
    /// <param name="file">The uploaded image file.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    [HttpPost("{key}")]
    [RequestSizeLimit(8_500_000)]
    public async Task<IActionResult> Upload(string key, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length is 0 or > 8_000_000)
        {
            return BadRequest();
        }

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        try
        {
            await _service.SaveAsync(key, file.ContentType, stream.ToArray(), cancellationToken);
        }
        catch (ArgumentException)
        {
            return BadRequest();
        }

        return NoContent();
    }
}