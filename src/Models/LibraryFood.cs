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
    double? Fiber,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? Version = null);
