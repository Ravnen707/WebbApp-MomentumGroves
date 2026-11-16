namespace MomentumGroves.Models;

public sealed record DrinkSearchResult(
    bool Success,
    IReadOnlyList<DrinkResult> Drinks,
    string? Error = null);