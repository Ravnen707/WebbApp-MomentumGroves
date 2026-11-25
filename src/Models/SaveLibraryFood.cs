namespace MomentumGroves.Models;

/// <summary>
/// Contains the data required to save a food.
/// </summary>
public sealed record SaveLibraryFood(string Name, string Unit, double Kcal, double Protein, double Carbs, double Fat,
    double? Fiber);
