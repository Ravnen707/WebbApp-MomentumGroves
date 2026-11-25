namespace MomentumGroves.Models;

internal sealed record FridaFoodRow(
    string Danish,
    string? English,
    double? Kcal,
    double? Protein,
    double? Carbs,
    double? Fat);
