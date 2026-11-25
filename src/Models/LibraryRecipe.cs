namespace MomentumGroves.Models;

/// <summary>
/// Represents a recipe with total nutrition calculated from its ingredients.
/// </summary>
public sealed record LibraryRecipe(
    long Id,
    string Name,
    double? FinishedWeight,
    List<RecipeIngredientDetail> Ingredients,
    double? Kcal,
    double? Protein,
    double? Carbs,
    double? Fat,
    double? Fiber,
    DateTimeOffset CreatedUtc);
