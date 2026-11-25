namespace MomentumGroves.Models;

public sealed record FoodSearchResult(bool Success, IReadOnlyList<FoodResult> Results, string? Error = null);