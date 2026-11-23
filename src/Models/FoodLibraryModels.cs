namespace MomentumGroves.Models;

/// <summary>
/// Represents a saved food with nutrition per 100 units.
/// </summary>
public sealed record LibraryFood(
    long Id,
    string Name,
    string Unit,
    double Kcal,
    double Protein,
    double Carbs,
    double Fat,
    double? Fiber);

/// <summary>
/// Contains the data required to save a food.
/// </summary>
public sealed record SaveLibraryFood(
    string Name,
    string Unit,
    double Kcal,
    double Protein,
    double Carbs,
    double Fat,
    double? Fiber);

/// <summary>
/// Specifies a food and its amount in a recipe.
/// </summary>
public sealed record RecipeIngredientInput(long FoodId, double Amount);

/// <summary>
/// Contains the data required to save a recipe.
/// </summary>
public sealed record SaveRecipe(string Name, double? FinishedWeight, List<RecipeIngredientInput> Ingredients);

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
