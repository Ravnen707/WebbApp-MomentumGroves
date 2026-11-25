using Microsoft.AspNetCore.Mvc;
using MomentumGroves.Service;

namespace MomentumGroves.Controllers;

[ApiController]
[Route("api/drinks")]
public sealed class DrinksController : ControllerBase
{
    private readonly DrinkService _drinkService;

    /// <summary>
    /// Initializes the drinks controller.
    /// </summary>
    /// <param name="drinkService">The service that searches available beverages.</param>
    public DrinksController(DrinkService drinkService)
    {
        _drinkService = drinkService;
    }

    /// <summary>
    /// Searches for drinks and handles validation and search errors.
    /// </summary>
    /// <param name="q">The optional name fragment provided by the caller.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The matching drinks or a response describing the provider failure.</returns>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length is < 2 or > 80)
        {
            return BadRequest(new { message = "Søgetekst skal være 2–80 tegn." });
        }

        var result = await _drinkService.SearchAsync(q, cancellationToken);
        if (!result.Success)
        {
            return StatusCode(503, new { message = result.Error ?? "Drikkevaresøgningen er ikke tilgængelig." });
        }

        return Ok(result.Drinks);
    }
}
