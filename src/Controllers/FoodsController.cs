using Microsoft.AspNetCore.Mvc;
using MomentumGroves.Service;

namespace MomentumGroves.Controllers;

[ApiController]
[Route("api/foods")]
public sealed class FoodsController : ControllerBase
{
    private readonly FoodService _foodService;

    /// <summary>
    /// Initializes the foods controller.
    /// </summary>
    /// <param name="foodService">The service that searches the local Frida dataset.</param>
    public FoodsController(FoodService foodService)
    {
        _foodService = foodService;
    }

    /// <summary>
    /// Searches for foods and handles validation and search errors.
    /// </summary>
    /// <param name="q">The optional name fragment provided by the caller.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The matching Frida foods or a response describing a storage failure.</returns>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length is < 2 or > 80)
        {
            return BadRequest(new
            {
                message = "Søgetekst skal være 2–80 tegn."
            });
        }

        var result = await _foodService.SearchAsync(q.Trim(), cancellationToken);
        if (!result.Success)
        {
            return StatusCode(503, new
            {
                message = result.Error
            });
        }

        return Ok(result.Results);
    }
}
