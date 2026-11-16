using Microsoft.AspNetCore.Mvc;
using MomentumGroves.Service;
using System.Text.Json;

namespace MomentumGroves.Controllers;

[ApiController]
[Route("api/momentum-data")]
public sealed class MomentumDataController : ControllerBase
{
    private readonly MomentumDataService _service;
    /// <summary>
    /// Initializes the momentum data controller.
    /// </summary>
    /// <param name="service"></param>
    public MomentumDataController(MomentumDataService service)
    {
        _service = service;
    }

    /// <summary>
    /// Returns all saved momentum data.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        return Ok(await _service.ReadAllAsync(cancellationToken));
    }

    /// <summary>
    /// Saves a momentum data value and handles validation errors.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPut("{key}")]
    public async Task<IActionResult> Put(string key, [FromBody] JsonElement value, CancellationToken cancellationToken)
    {
        try
        {
            await _service.SaveAsync(key, value, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException error)
        {
            return BadRequest(new { message = error.Message });
        }
    }

    /// <summary>
    /// Imports missing momentum data values and handles validation errors.
    /// </summary>
    /// <param name="values"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] Dictionary<string, JsonElement> values, CancellationToken cancellationToken)
    {
        try
        {
            var imported = await _service.ImportMissingAsync(values, cancellationToken);
            return Ok(new { imported });
        }
        catch (ArgumentException error)
        {
            return BadRequest(new { message = error.Message });
        }
    }
}
