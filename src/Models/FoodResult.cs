namespace MomentumGroves.Models;

public sealed record FoodResult(string Name, string Source, double? Kcal, double? Protein, double? Carbs, double? Fat,
    double? Fiber);