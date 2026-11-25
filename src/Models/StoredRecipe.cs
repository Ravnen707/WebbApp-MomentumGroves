namespace MomentumGroves.Models;

internal sealed record StoredRecipe(long Id, string Name, double? FinishedWeight,
    DateTimeOffset CreatedUtc, List<RecipeIngredientDetail> Ingredients);
