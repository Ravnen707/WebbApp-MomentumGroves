namespace MomentumGroves.Models;

public sealed record DrinkResult(string Name, string Source, double? Kcal, double? Protein, double? Carbs, double? Fat,
    bool Danish);