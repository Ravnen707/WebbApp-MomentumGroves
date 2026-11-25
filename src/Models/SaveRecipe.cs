namespace MomentumGroves.Models;

/// <summary>
/// Contains the data required to save a recipe.
/// </summary>
public sealed record SaveRecipe(string Name, double? FinishedWeight, List<RecipeIngredientInput> Ingredients);
