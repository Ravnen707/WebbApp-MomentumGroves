using Microsoft.AspNetCore.Mvc;
using MomentumGroves.Models;
using MomentumGroves.Service;

namespace MomentumGroves.Controllers;

/// <summary>
/// Provides pages and API endpoints for the food and recipe library.
/// </summary>
public sealed class FoodLibraryController : Controller
{
    private readonly FoodLibraryService _library;

    /// <summary>
    /// Initializes the food library controller.
    /// </summary>
    /// <param name="library">The service that manages saved foods and recipes.</param>
    public FoodLibraryController(FoodLibraryService library)
    {
        _library = library;
    }

    /// <summary>
    /// Displays the saved foods page.
    /// </summary>
    /// <returns>The saved foods page.</returns>
    [HttpGet]
    public IActionResult Foods()
    {
        return View();
    }

    /// <summary>
    /// Displays the saved recipes page.
    /// </summary>
    /// <returns>The saved recipes page.</returns>
    [HttpGet]
    public IActionResult Recipes()
    {
        return View();
    }

    /// <summary>
    /// Returns saved foods matching the search query.
    /// </summary>
    /// <param name="q">The optional name fragment provided by the caller.</param>
    /// <returns>The matching foods in name order.</returns>
    [HttpGet("api/library/foods")]
    public IActionResult ListFoods([FromQuery] string? q)
    {
        return Ok(_library.Foods(q));
    }

    /// <summary>
    /// Returns a paginated list for the saved foods screen.
    /// </summary>
    /// <param name="q">The optional name fragment provided by the caller.</param>
    /// <param name="page">The requested one-based page number.</param>
    /// <param name="pageSize">The requested number of items per page.</param>
    /// <returns>The matching page and pagination details.</returns>
    [HttpGet("api/library/foods/paged")]
    public IActionResult ListPagedFoods([FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        return Ok(_library.PagedFoods(q, page, pageSize));
    }

    /// <summary>
    /// Creates a food in the personal library.
    /// </summary>
    /// <param name="food">The submitted food and nutrition values.</param>
    /// <returns>The new food ID or a validation error.</returns>
    [HttpPost("api/library/foods")]
    public IActionResult CreateFood([FromBody] SaveLibraryFood? food)
    {
        if (!ModelState.IsValid || food is null)
        {
            return BadRequest(new { message = "Invalid food details." });
        }

        try
        {
            return Ok(new { id = _library.SaveFood(null, food) });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    /// <summary>
    /// Updates an existing library food.
    /// </summary>
    /// <param name="id">The identifier of the existing item.</param>
    /// <param name="food">The submitted food and nutrition values.</param>
    /// <param name="version">The revision submitted by the client.</param>
    /// <returns>The saved food ID or the relevant validation, missing-item, or conflict response.</returns>
    [HttpPut("api/library/foods/{id:long}")]
    public IActionResult UpdateFood(long id, [FromBody] SaveLibraryFood? food, [FromQuery] string? version)
    {
        if (!ModelState.IsValid || food is null)
        {
            return BadRequest(new { message = "Invalid food details." });
        }

        try
        {
            return Ok(new { id = _library.SaveFood(id, food, version) });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    /// <summary>
    /// Deletes a food that is not used by a recipe.
    /// </summary>
    /// <param name="id">The identifier of the existing item.</param>
    /// <param name="version">The revision submitted by the client.</param>
    /// <returns>No content on success or a conflict response.</returns>
    [HttpDelete("api/library/foods/{id:long}")]
    public IActionResult RemoveFood(long id, [FromQuery] string? version)
    {
        try
        {
            _library.DeleteFood(id, version);
            return NoContent();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    /// <summary>
    /// Returns saved recipes and their nutrition values.
    /// </summary>
    /// <returns>The saved recipes with their ingredient nutrition totals.</returns>
    [HttpGet("api/library/recipes")]
    public IActionResult ListRecipes()
    {
        return Ok(_library.Recipes());
    }

    /// <summary>
    /// Creates a recipe from saved foods.
    /// </summary>
    /// <param name="recipe">The submitted recipe details and ingredients.</param>
    /// <returns>The new recipe ID or a request error.</returns>
    [HttpPost("api/library/recipes")]
    public IActionResult CreateRecipe([FromBody] SaveRecipe? recipe)
    {
        if (!ModelState.IsValid || recipe is null)
        {
            return BadRequest(new { message = "The recipe does not contain valid details." });
        }

        return Save(null, recipe);
    }

    /// <summary>
    /// Updates an existing recipe.
    /// </summary>
    /// <param name="id">The identifier of the existing item.</param>
    /// <param name="recipe">The submitted recipe details and ingredients.</param>
    /// <returns>The saved recipe ID or a request error.</returns>
    [HttpPut("api/library/recipes/{id:long}")]
    public IActionResult UpdateRecipe(long id, [FromBody] SaveRecipe? recipe)
    {
        if (!ModelState.IsValid || recipe is null)
        {
            return BadRequest(new { message = "The recipe does not contain valid details." });
        }

        return Save(id, recipe);
    }

    /// <summary>
    /// Saves a recipe and handles validation errors.
    /// </summary>
    /// <param name="id">The identifier of the existing item.</param>
    /// <param name="recipe">The submitted recipe details and ingredients.</param>
    /// <returns>The saved recipe ID or a request error.</returns>
    private IActionResult Save(long? id, SaveRecipe recipe)
    {
        try
        {
            return Ok(new { id = _library.SaveRecipe(id, recipe) });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Deletes an existing recipe.
    /// </summary>
    /// <param name="id">The identifier of the existing item.</param>
    /// <returns>No content after the delete operation.</returns>
    [HttpDelete("api/library/recipes/{id:long}")]
    public IActionResult RemoveRecipe(long id)
    {
        _library.DeleteRecipe(id);
        return NoContent();
    }
}
