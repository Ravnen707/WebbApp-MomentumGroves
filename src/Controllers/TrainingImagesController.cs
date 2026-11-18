using Microsoft.AspNetCore.Mvc;
using MomentumGroves.Service;

namespace MomentumGroves.Controllers;

[ApiController]
[Route("api/training-images")]
public sealed class TrainingImagesController(TrainingImageService service) : ControllerBase
{
    [HttpGet]
    public IReadOnlyList<TrainingPicture> List() => service.List();

    [HttpPost]
    [RequestSizeLimit(8_500_000)]
    public async Task<ActionResult<TrainingPicture>> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length is 0 or > TrainingImageService.MaxUploadBytes)
            return BadRequest("Choose a JPG, PNG or WebP image no larger than 8 MB.");

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        try
        {
            var image = await service.SaveAsync(file.FileName, stream.ToArray(), cancellationToken);
            return Created(image.Url, image);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }
}
