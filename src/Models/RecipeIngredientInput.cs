namespace MomentumGroves.Models;

/// <summary>
/// Specifies a food and its amount in a recipe.
/// </summary>
public sealed record RecipeIngredientInput(long FoodId, double Amount);
