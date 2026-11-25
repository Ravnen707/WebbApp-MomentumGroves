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
    /// <param name="service">The application service that performs the requested operation.</param>
    public MomentumDataController(MomentumDataService service)
    {
        _service = service;
    }

    /// <summary>
    /// Returns all saved momentum data.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The saved application values as JSON.</returns>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        return Ok(await _service.ReadAllAsync(cancellationToken));
    }

    /// <summary>
    /// Saves a momentum data value and handles validation errors.
    /// </summary>
    /// <param name="key">The application data key to validate or update.</param>
    /// <param name="value">The supplied value to process.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>No content after saving or a validation error.</returns>
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
    /// <param name="values">The submitted values keyed by application data key.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The number of values imported or a validation error.</returns>
    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] Dictionary<string, JsonElement> values,
        CancellationToken cancellationToken)
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
