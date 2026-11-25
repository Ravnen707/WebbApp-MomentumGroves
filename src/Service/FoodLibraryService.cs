using MomentumGroves.Data;
using MomentumGroves.Models;
using MomentumGroves.Repositories;

namespace MomentumGroves.Service;

/// <summary>
/// Manages stored foods, recipes, and nutrition calculations.
/// </summary>
public sealed class FoodLibraryService
{
    private readonly FoodLibraryRepository _repository;

    /// <summary>
    /// Initializes the food and recipe database.
    /// </summary>
    /// <param name="environment">The environment containing the local database directory.</param>
    /// <param name="logger">The logger for library operations.</param>
    public FoodLibraryService(IWebHostEnvironment environment, ILogger<FoodLibraryService> logger)
        : this(PersonalDatabase.Local(environment), logger)
    {
    }

    /// <summary>
    /// Initializes the library with the configured SQLite or PostgreSQL database.
    /// </summary>
    /// <param name="database">The personal database for this application instance.</param>
    /// <param name="logger">The logger for library operations.</param>
    public FoodLibraryService(PersonalDatabase database, ILogger<FoodLibraryService> logger)
    {
        _repository = new FoodLibraryRepository(database, logger);
        try
        {
            database.InitializeLibrary();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not initialize FoodLibraryService storage");
            throw;
        }
    }

    /// <summary>
    /// Finds saved foods by name.
    /// </summary>
    /// <param name="query">The optional food name fragment.</param>
    /// <returns>The matching foods in name order.</returns>
    public List<LibraryFood> Foods(string? query = null)
    {
        return _repository.Foods(query);
    }

    /// <summary>
    /// Returns a page of saved foods, newest first, matching the entire library.
    /// </summary>
    /// <param name="query">The optional food name fragment.</param>
    /// <param name="page">The requested one-based page number.</param>
    /// <param name="pageSize">The requested items per page.</param>
    /// <returns>The matching page and pagination metadata.</returns>
    public object PagedFoods(string? query, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var (items, total) = _repository.PagedFoods(query, page, pageSize);
        return new { items, total, page, pageSize, hasMore = (long)page * pageSize < total };
    }

    /// <summary>
    /// Validates and saves a library food.
    /// </summary>
    /// <param name="id">The existing food ID or null to create a food.</param>
    /// <param name="food">The food details and nutrition values.</param>
    /// <param name="version">The version of the food originally loaded by the editor.</param>
    /// <returns>The ID of the saved food.</returns>
    public long SaveFood(long? id, SaveLibraryFood food, string? version = null)
    {
        if (string.IsNullOrWhiteSpace(food.Name) || food.Name.Length > 200 || food.Unit is not ("g" or "ml") ||
            new[] { food.Kcal, food.Protein, food.Carbs, food.Fat, food.Fiber ?? 0 }.Any(x =>
                !double.IsFinite(x) || x < 0))
        {
            throw new ArgumentException("Invalid food details.");
        }

        return _repository.SaveFood(id, food, version);
    }

    /// <summary>
    /// Deletes a food that is unused by recipes.
    /// </summary>
    /// <param name="id">The ID of the food to delete.</param>
    /// <param name="version">The version of the food originally loaded by the editor.</param>
    public void DeleteFood(long id, string? version = null)
    {
        _repository.DeleteFood(id, version);
    }

    /// <summary>
    /// Returns saved recipes with nutrition totals calculated from their ingredients.
    /// </summary>
    /// <returns>The recipes in name order with nutrition totals.</returns>
    public List<LibraryRecipe> Recipes()
    {
        var recipes = _repository.Recipes();
        return recipes.Select(recipe =>
        {
            var (id, name, weight, createdUtc, items) = recipe;

            /// <summary>
            /// Calculates the known total for one nutrient across the recipe ingredients.
            /// </summary>
            /// <param name="selector">The function that selects one nutrient amount per ingredient.</param>
            /// <returns>The sum of known amounts for the nutrient, or null when all amounts are unknown.</returns>
            double? Total(Func<RecipeIngredientDetail, double?> selector)
            {
                return items.Any(x => selector(x).HasValue)
                    ? items.Sum(x => (selector(x) ?? 0) * x.Amount / 100)
                    : null;
            }

            return new LibraryRecipe(id, name, weight, items, Total(x => x.Kcal), Total(x => x.Protein),
                Total(x => x.Carbs), Total(x => x.Fat), Total(x => x.Fiber), createdUtc);
        }).ToList();
    }

    /// <summary>
    /// Validates and saves a recipe and its ingredients.
    /// </summary>
    /// <param name="id">The existing recipe ID or null to create a recipe.</param>
    /// <param name="recipe">The recipe details and ingredients.</param>
    /// <returns>The ID of the saved recipe.</returns>
    public long SaveRecipe(long? id, SaveRecipe recipe)
    {
        if (string.IsNullOrWhiteSpace(recipe.Name) || recipe.Name.Length > 200 ||
            recipe.FinishedWeight is double w && (!double.IsFinite(w) || w <= 0) ||
            recipe.Ingredients is null || recipe.Ingredients.Count == 0 ||
            recipe.Ingredients.Any(x => x is null || x.FoodId <= 0 || !double.IsFinite(x.Amount) || x.Amount <= 0) ||
            recipe.Ingredients.Select(x => x.FoodId).Distinct().Count() != recipe.Ingredients.Count)
        {
            throw new ArgumentException("The recipe does not contain valid details.");
        }

        return _repository.SaveRecipe(id, recipe);
    }

    /// <summary>
    /// Deletes a recipe and its ingredients.
    /// </summary>
    /// <param name="id">The ID of the recipe to delete.</param>
    public void DeleteRecipe(long id)
    {
        _repository.DeleteRecipe(id);
    }
}
