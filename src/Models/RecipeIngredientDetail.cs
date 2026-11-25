namespace MomentumGroves.Models;

/// <summary>
/// Represents a recipe ingredient with nutrition values.
/// </summary>
public sealed record RecipeIngredientDetail(
    long FoodId,
    string Name,
    string Unit,
    double Amount,
    double Kcal,
    double Protein,
    double Carbs,
    double Fat,
    double? Fiber);
