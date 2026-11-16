using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
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
    /// <param name="library"></param>
    public FoodLibraryController(FoodLibraryService library)
    {
        _library = library;
    }

    /// <summary>
    /// Displays the saved foods page.
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public IActionResult Foods()
    {
        return View();
    }

    /// <summary>
    /// Displays the saved recipes page.
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public IActionResult Recipes()
    {
        return View();
    }

    /// <summary>
    /// Returns saved foods matching the search query.
    /// </summary>
    /// <param name="q"></param>
    /// <returns></returns>
    [HttpGet("api/library/foods")]
    public IActionResult ListFoods([FromQuery] string? q)
    {
        return Ok(_library.Foods(q));
    }

    /// <summary>
    /// Returns a paginated list for the saved foods screen.
    /// </summary>
    /// <param name="q"></param>
    /// <param name="page"></param>
    /// <param name="pageSize"></param>
    /// <returns></returns>
    [HttpGet("api/library/foods/paged")]
    public IActionResult ListPagedFoods([FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        return Ok(_library.PagedFoods(q, page, pageSize));
    }

    /// <summary>
    /// Creates a food in the personal library.
    /// </summary>
    /// <param name="food"></param>
    /// <returns></returns>
    [HttpPost("api/library/foods")]
    public IActionResult CreateFood([FromBody] SaveLibraryFood food)
    {
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
    /// <param name="id"></param>
    /// <param name="food"></param>
    /// <returns></returns>
    [HttpPut("api/library/foods/{id:long}")]
    public IActionResult UpdateFood(long id, [FromBody] SaveLibraryFood food)
    {
        try
        {
            return Ok(new { id = _library.SaveFood(id, food) });
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
    /// Deletes a food that is not used by a recipe.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpDelete("api/library/foods/{id:long}")]
    public IActionResult RemoveFood(long id)
    {
        try
        {
            _library.DeleteFood(id);
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
    /// <returns></returns>
    [HttpGet("api/library/recipes")]
    public IActionResult ListRecipes()
    {
        return Ok(_library.Recipes());
    }

    /// <summary>
    /// Creates a recipe from saved foods.
    /// </summary>
    /// <param name="recipe"></param>
    /// <returns></returns>
    [HttpPost("api/library/recipes")]
    public IActionResult CreateRecipe([FromBody] SaveRecipe recipe)
    {
        return Save(null, recipe);
    }

    /// <summary>
    /// Updates an existing recipe.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="recipe"></param>
    /// <returns></returns>
    [HttpPut("api/library/recipes/{id:long}")]
    public IActionResult UpdateRecipe(long id, [FromBody] SaveRecipe recipe)
    {
        return Save(id, recipe);
    }

    /// <summary>
    /// Saves a recipe and handles validation errors.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="recipe"></param>
    /// <returns></returns>
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
        catch (SqliteException)
        {
            return BadRequest(new { message = "Kontrollér ingredienserne i opskriften." });
        }
    }

    /// <summary>
    /// Deletes an existing recipe.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpDelete("api/library/recipes/{id:long}")]
    public IActionResult RemoveRecipe(long id)
    {
        _library.DeleteRecipe(id);
        return NoContent();
    }
}